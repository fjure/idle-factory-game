using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    public sealed class PlacementPreviewView : MonoBehaviour
    {
        private F001SpriteCatalog sprites;
        private GridWorldMapper mapper;
        private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();

        public bool IsVisible { get; private set; }
        public bool IsValid { get; private set; }
        public string Feedback { get; private set; } = string.Empty;
        public IReadOnlyList<GridPosition> Cells { get; private set; } = Array.Empty<GridPosition>();
        public IReadOnlyList<SpriteRenderer> Renderers => renderers;

        public void Configure(F001SpriteCatalog spriteCatalog, GridWorldMapper gridMapper)
        {
            sprites = spriteCatalog ?? throw new ArgumentNullException(nameof(spriteCatalog));
            mapper = gridMapper ?? throw new ArgumentNullException(nameof(gridMapper));
            if (!sprites.IsValid)
            {
                Hide(sprites.Error);
            }
        }

        public void Show(BuildingDefinition definition, GridPosition anchor, int orientationQuarterTurns, PlacementPreview preview)
        {
            ClearRenderers();
            if (definition == null || preview == null || sprites == null || mapper == null || !sprites.IsValid)
            {
                Hide(sprites?.Error ?? "Placement preview is unavailable.");
                return;
            }

            IsVisible = true;
            IsValid = preview.IsValid;
            Cells = preview.Cells.ToArray();
            Feedback = Describe(preview.Rejections);

            var alternate = definition.Footprint.Width == 3 && definition.Footprint.Height == 1;
            var ghostKey = alternate
                ? (preview.IsValid ? F001SpriteCatalog.AlternateGhostValid : F001SpriteCatalog.AlternateGhostInvalid)
                : (preview.IsValid ? F001SpriteCatalog.D001GhostValid : F001SpriteCatalog.D001GhostInvalid);
            var ghost = AddSprite("placement-footprint", sprites.Get(ghostKey), FactoryView.PreviewSortingOrder);
            ApplyFootprintTransform(ghost.transform, mapper, definition.Footprint, anchor, orientationQuarterTurns);

            var badgeKey = preview.IsValid
                ? F001SpriteCatalog.BadgePlacementValid
                : F001SpriteCatalog.BadgePlacementInvalid;
            var badge = AddSprite("placement-status", sprites.Get(badgeKey), FactoryView.BadgeSortingOrder);
            var footprintSize = OrientedFootprintSize(definition.Footprint, orientationQuarterTurns);
            badge.transform.position = mapper.CellOrigin(anchor) + new Vector2(
                footprintSize.x * mapper.CellSize * 0.5f,
                footprintSize.y * mapper.CellSize * 0.5f);
        }

        public void Hide(string feedback = "")
        {
            ClearRenderers();
            IsVisible = false;
            IsValid = false;
            Cells = Array.Empty<GridPosition>();
            Feedback = feedback ?? string.Empty;
        }

        private SpriteRenderer AddSprite(string objectName, Sprite sprite, int sortingOrder)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderers.Add(renderer);
            return renderer;
        }

        public static void ApplyFootprintTransform(
            Transform target,
            GridWorldMapper gridMapper,
            Footprint footprint,
            GridPosition anchor,
            int quarterTurns)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (gridMapper == null)
            {
                throw new ArgumentNullException(nameof(gridMapper));
            }

            var origin = gridMapper.CellOrigin(anchor);
            switch (quarterTurns)
            {
                case 0:
                    target.position = origin;
                    target.rotation = Quaternion.identity;
                    break;
                case 1:
                    target.position = origin + new Vector2(footprint.Height * gridMapper.CellSize, 0f);
                    target.rotation = Quaternion.Euler(0f, 0f, 90f);
                    break;
                case 2:
                    target.position = origin + new Vector2(
                        footprint.Width * gridMapper.CellSize,
                        footprint.Height * gridMapper.CellSize);
                    target.rotation = Quaternion.Euler(0f, 0f, 180f);
                    break;
                case 3:
                    target.position = origin + new Vector2(0f, footprint.Width * gridMapper.CellSize);
                    target.rotation = Quaternion.Euler(0f, 0f, 270f);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(quarterTurns));
            }
        }

        public static Vector2Int OrientedFootprintSize(Footprint footprint, int quarterTurns)
        {
            switch (quarterTurns)
            {
                case 0:
                case 2:
                    return new Vector2Int(footprint.Width, footprint.Height);
                case 1:
                case 3:
                    return new Vector2Int(footprint.Height, footprint.Width);
                default:
                    throw new ArgumentOutOfRangeException(nameof(quarterTurns));
            }
        }

        private void ClearRenderers()
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(renderer.gameObject);
                }
                else
                {
                    DestroyImmediate(renderer.gameObject);
                }
            }

            renderers.Clear();
        }

        private static string Describe(IReadOnlyList<PlacementPreviewRejection> rejections)
        {
            if (rejections.Count == 0)
            {
                return "Valid placement. Press Confirm to build.";
            }

            var reasons = rejections.Distinct().Select(rejection =>
            {
                switch (rejection)
                {
                    case PlacementPreviewRejection.OutOfBounds:
                        return "Complete footprint must stay inside the factory";
                    case PlacementPreviewRejection.Occupied:
                        return "Footprint overlaps an existing building";
                    case PlacementPreviewRejection.UnknownDefinition:
                        return "Building definition is unavailable";
                    case PlacementPreviewRejection.UnsupportedOrientation:
                        return "Orientation is not supported";
                    default:
                        return "Placement request is invalid";
                }
            });
            return "Cannot place: " + string.Join("; ", reasons) + ".";
        }
    }
}
