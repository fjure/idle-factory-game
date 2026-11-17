using System.Collections;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// T527 (research R06, contracts/world-presentation.md section 1): the Tilemap module check in the player. The Tilemap, TilemapRenderer and Tile types
    /// of the deck must survive the player build and IL2CPP stripping: a rectangle grid with a tile on the lattice cell draws that tile's colour at the cell
    /// centre in a real render, and the deck view builds its Tilemaps from a catalog. Tagged for the Windows test player (the parent runs the suite per batch);
    /// the Android IL2CPP build check is recorded in the T527 report.
    /// </summary>
    public sealed class TilemapSmokeTests
    {
        [Category("StandalonePlayer")]
        [UnityTest]
        public IEnumerator ATileOnARectangleGridDrawsItsColourAtTheCellCentreInThePlayer()
        {
            var rig = new DeckTestRig();
            var target = new RenderTexture(210, 162, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, filterMode = FilterMode.Point };
            target.Create();
            try
            {
                var colour = new Color32(200, 40, 40, 255);
                var texture = new Texture2D(70, 54, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point };
                var pixels = new Color32[70 * 54];
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = colour;
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                rig.Owned.Add(texture);
                var sprite = Sprite.Create(texture, new Rect(0, 0, 70, 54), new Vector2(0.5f, 0.5f), 24, 0, SpriteMeshType.FullRect);
                rig.Owned.Add(sprite);
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                rig.Owned.Add(tile);

                var gridObject = new GameObject("smoke-grid");
                rig.Owned.Add(gridObject);
                var grid = gridObject.AddComponent<Grid>();
                grid.cellLayout = GridLayout.CellLayout.Rectangle;
                grid.cellSize = FactoryDeckView.CellSize;
                gridObject.transform.position = FactoryDeckView.GridOrigin;
                var tilemapObject = new GameObject("smoke-tilemap");
                tilemapObject.transform.SetParent(gridObject.transform, false);
                var tilemap = tilemapObject.AddComponent<Tilemap>();
                var renderer = tilemapObject.AddComponent<TilemapRenderer>();
                renderer.sortingLayerName = FactoryDeckView.BaseSortingLayer;
                tilemap.SetTile(new Vector3Int(1, 1, 0), tile);
                Assert.That(DeckTestRig.TileCount(tilemap), Is.EqualTo(1));

                var cameraObject = new GameObject("smoke-camera");
                rig.Owned.Add(cameraObject);
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 162f / (2f * 24f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.allowMSAA = false;
                camera.targetTexture = target;
                camera.enabled = false;
                var centre = FactoryWorldLayout.PivotOfCell(new Vector2Int(1, 1));
                cameraObject.transform.position = new Vector3(centre.x, centre.y, -10f);

                for (var i = 0; i < 3; i++)
                {
                    camera.Render();
                    yield return null;
                }

                camera.Render();
                var previous = RenderTexture.active;
                var readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, false);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
                readback.Apply(false);
                RenderTexture.active = previous;
                var middle = readback.GetPixel(target.width / 2, target.height / 2);
                var corner = readback.GetPixel(2, 2);
                Object.DestroyImmediate(readback);
                Assert.That(middle.r, Is.GreaterThan(0.6f), "the tile colour at the cell centre");
                Assert.That(middle.g, Is.LessThan(0.3f));
                Assert.That(corner.r, Is.LessThan(0.1f), "the background away from the tile (the tile spans 70 x 54 px around the centre of a 210 x 162 view)");
            }
            finally
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(target);
                rig.Dispose();
            }
        }

        [Category("StandalonePlayer")]
        [Test]
        public void TheDeckViewBuildsItsTilemapsFromACatalogInThePlayer()
        {
            var rig = new DeckTestRig();
            try
            {
                var layout = LatticeLayoutBuilder.ShippedForColumns(3);
                var catalog = rig.Catalog();
                var grid = rig.NewGrid(catalog, layout);
                var deck = rig.NewDeck(catalog, rig.Settings(), grid);
                grid.Render(DeckTestRig.Frame(12, i => i < 3 ? SlotCondition.Occupied : SlotCondition.Locked));
                Assert.That(deck.BaseTilemap, Is.Not.Null);
                Assert.That(DeckTestRig.TileCount(deck.DeckTilemap), Is.EqualTo(12));
                Assert.That(DeckTestRig.TileCount(deck.BaseTilemap), Is.EqualTo(deck.Plan.BaseCells.Count));
            }
            finally
            {
                rig.Dispose();
            }
        }
    }
}
