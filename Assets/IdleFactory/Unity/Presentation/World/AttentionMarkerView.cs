using System;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// Draws the production attention markers of <see cref="AttentionModel"/> in the world (F006 T532). It owns three pre-built renderers
    /// (<see cref="AttentionModel.MaxMarkers"/>) on the <see cref="SlotGridView.OverlaySortingLayer"/>, above every slot overlay, and no text: a marker is
    /// the resource icon and nothing else, so no number appears in the world (FR-030). It decides nothing; the model supplies positions, icons and the
    /// location a tap selects. A marker is static (no loop, no pulse), so reduced motion needs no extra fallback.
    /// <para>
    /// Touch: a marker is hit inside a circle that is at least <see cref="MinimumHitDiameterPoints"/> across on screen (UI/UX C4), even though its icon
    /// is smaller. The marker boxes never overlap a keep-out of the lattice (verified against <see cref="FactoryWorldLayout"/> in the tests).
    /// </para>
    /// </summary>
    public sealed class AttentionMarkerView : MonoBehaviour
    {
        /// <summary>The smallest tap target in points (UI/UX C4, 48 dp).</summary>
        public const float MinimumHitDiameterPoints = 48f;

        /// <summary>The marker draws after every slot overlay: the slot overlays use orders up to <c>OverlayOrder + 4 * rows + 3</c>.</summary>
        public const int MarkerOrderOffset = 200;

        private PresentationSpriteCatalog sprites;
        private Func<string, string> resourceKeyOf;
        private SpriteRenderer[] renderers;
        private Vector2[] centres = new Vector2[AttentionModel.MaxMarkers];
        private BuildingLocationId[] targets = new BuildingLocationId[AttentionModel.MaxMarkers];
        private int visibleCount;
        private float pointsPerCell = 48f;

        /// <summary>The number of markers drawn now.</summary>
        public int VisibleMarkerCount => visibleCount;

        /// <summary>The marker renderer of index <paramref name="index"/> (enabled only while the marker is shown).</summary>
        public SpriteRenderer RendererOf(int index) => renderers[index];

        /// <summary>
        /// Builds the renderers. <paramref name="resourceKeyOf"/> maps a resource id to its presentation key (<c>ResourceDef.PresentationKey</c>); the
        /// store-full marker uses the storage-full cue sprite.
        /// </summary>
        public void Configure(PresentationSpriteCatalog catalog, Func<string, string> resourceKeyOf)
        {
            sprites = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.resourceKeyOf = resourceKeyOf ?? throw new ArgumentNullException(nameof(resourceKeyOf));
            if (renderers != null)
            {
                foreach (var renderer in renderers)
                {
                    if (renderer != null)
                    {
                        Destroy(renderer.gameObject);
                    }
                }
            }

            var layerId = SortingLayer.NameToID(SlotGridView.OverlaySortingLayer);
            renderers = new SpriteRenderer[AttentionModel.MaxMarkers];
            for (var i = 0; i < renderers.Length; i++)
            {
                var child = new GameObject("AttentionMarker" + i);
                child.transform.SetParent(transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sortingLayerID = layerId;
                renderer.sortingOrder = SlotGridView.OverlayOrder + MarkerOrderOffset + i;
                renderer.enabled = false;
                renderers[i] = renderer;
            }

            visibleCount = 0;
        }

        /// <summary>The projected pixels per cell of the camera, for the hit radius. It changes no drawing.</summary>
        public void SetProjectedPixelsPerCell(float value) => pointsPerCell = Mathf.Max(1f, value);

        /// <summary>Draws the model's markers. Allocation-free once configured.</summary>
        public void Render(AttentionModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (renderers == null) throw new InvalidOperationException("Configure the view first.");
            visibleCount = 0;
            var box = AttentionModel.MarkerSizePixels / (float)SlotGridLayout.Density;
            for (var i = 0; i < model.MarkerCount && i < renderers.Length; i++)
            {
                var marker = model.GetMarker(i);
                var key = marker.Kind == AttentionKind.StoreFull ? SlotGridModel.BadgeStorageFullKey : resourceKeyOf(marker.ResourceId);
                var renderer = renderers[visibleCount];
                if (key == null || !sprites.TryGetEntry(key, out var entry) || entry.Sprite == null)
                {
                    continue; // a missing icon draws nothing rather than a placeholder with text
                }

                renderer.sprite = entry.Sprite;
                var bounds = entry.Sprite.bounds.size;
                var largest = Mathf.Max(bounds.x, bounds.y);
                var scale = largest > 0f ? box / largest : 1f;
                renderer.transform.localPosition = new Vector3(marker.Position.x, marker.Position.y, 0f);
                renderer.transform.localScale = new Vector3(scale, scale, 1f);
                renderer.enabled = true;
                centres[visibleCount] = marker.Position;
                targets[visibleCount] = marker.FirstAffected;
                visibleCount++;
            }

            for (var i = visibleCount; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
                renderers[i].sprite = null;
            }
        }

        /// <summary>The hit radius in world units: half of 48 points at the current scale, never below half the icon box.</summary>
        public float HitRadiusWorld => Mathf.Max(MinimumHitDiameterPoints * 0.5f / pointsPerCell, AttentionModel.MarkerSizePixels * 0.5f / SlotGridLayout.Density);

        /// <summary>
        /// Resolves a world point to the location a marker selects: the first affected location of the nearest marker within the hit radius.
        /// Allocation-free.
        /// </summary>
        public bool TryHit(Vector2 world, out BuildingLocationId target)
        {
            var best = -1;
            var bestDistance = HitRadiusWorld * HitRadiusWorld;
            for (var i = 0; i < visibleCount; i++)
            {
                var distance = (centres[i] - world).sqrMagnitude;
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            target = best >= 0 ? targets[best] : default;
            return best >= 0;
        }
    }
}
