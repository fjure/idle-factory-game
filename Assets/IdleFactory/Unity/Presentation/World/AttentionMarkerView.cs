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

        /// <summary>
        /// C-57 (U-C2, T560): the store-full marker uses the 24 x 24 storage icon the Store pill and the card cause line use, a glyph of the marker's own size, so it is not
        /// mistaken for the per-machine store-full badge (<see cref="SlotGridModel.BadgeStorageFullKey"/>, 72 x 64). No new art; the badge stays the fallback.
        /// </summary>
        /// <summary>The longest a layout change blanks the markers: the feed refreshes within 0.25 s, so a model that never refreshed (same layout) is trusted after this.</summary>
        public const float PendingLayoutSeconds = 0.3f;

        public const string StoreFullMarkerKey = "F003-UI-ICON-STORAGE";

        private PresentationSpriteCatalog sprites;
        private Func<string, string> resourceKeyOf;
        private SpriteRenderer[] renderers;
        private Vector2[] centres = new Vector2[AttentionModel.MaxMarkers];
        private BuildingLocationId[] targets = new BuildingLocationId[AttentionModel.MaxMarkers];
        private int visibleCount;
        private float pointsPerCell = 48f;
        private SlotGridView bound;
        private FactoryWorldLayout pendingLayout;
        private float pendingSince;

        /// <summary>True between a layout change of the bound grid and the first render of a model computed on that layout (C-39): nothing is drawn then.</summary>
        public bool AwaitingModelLayout => pendingLayout != null;

        // The layout id includes the composition identity, so two instances of the same layout (the shell's and the grid's) are the same layout (Architect 1a).
        private static bool SameLayout(FactoryWorldLayout a, FactoryWorldLayout b) =>
            a != null && b != null && (ReferenceEquals(a, b) || string.Equals(a.LayoutId, b.LayoutId, System.StringComparison.Ordinal));

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

        /// <summary>
        /// Follows the grid's layout (T557, like <see cref="DepotLinkView"/>): a layout change clears the markers at once, so a stale position is never drawn
        /// or hit; the attention model recomputes the positions from the new layout and the next <see cref="Render"/> draws them. Binding again replaces the
        /// previous grid.
        /// </summary>
        public void Bind(SlotGridView slotGrid)
        {
            Unbind();
            bound = slotGrid ?? throw new ArgumentNullException(nameof(slotGrid));
            pendingLayout = null;
            bound.LayoutChanged += HandleLayoutChanged;
        }

        private void Unbind()
        {
            if (bound == null)
            {
                return;
            }

            bound.LayoutChanged -= HandleLayoutChanged;
            bound = null;
        }

        private void HandleLayoutChanged(FactoryWorldLayout newLayout)
        {
            // C-39 (T560): the model recomputes its positions only on its next refresh (up to 0.25 s). Until it carries this layout, Render draws nothing, so
            // a stale marker is neither drawn nor hit.
            pendingLayout = newLayout;
            pendingSince = Time.unscaledTime;
            Clear();
        }

        /// <summary>Hides every marker (no hit target remains) until the next <see cref="Render"/>.</summary>
        public void Clear()
        {
            visibleCount = 0;
            if (renderers == null)
            {
                return;
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = false;
                    renderers[i].sprite = null;
                }
            }
        }

        private void OnDestroy() => Unbind();

        /// <summary>The projected size of one cell in panel POINTS at the camera's zoom (<c>CoreGameFoundationBootstrap.CurrentPixelsPerCell</c> already divides by the panel scale), for the hit radius of 24 pt. It changes no drawing.</summary>
        public void SetProjectedPixelsPerCell(float value) => pointsPerCell = Mathf.Max(1f, value);

        /// <summary>Draws the model's markers. Allocation-free once configured.</summary>
        public void Render(AttentionModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (renderers == null) throw new InvalidOperationException("Configure the view first.");
            visibleCount = 0;
            if (pendingLayout != null)
            {
                if (!SameLayout(model.Layout, pendingLayout) && Time.unscaledTime - pendingSince < PendingLayoutSeconds)
                {
                    return;
                }

                pendingLayout = null;
            }

            var box = AttentionModel.MarkerSizePixels / (float)SlotGridLayout.Density;
            for (var i = 0; i < model.MarkerCount && i < renderers.Length; i++)
            {
                var marker = model.GetMarker(i);
                var key = marker.Kind == AttentionKind.StoreFull ? StoreFullMarkerKey : resourceKeyOf(marker.ResourceId);
                if (marker.Kind == AttentionKind.StoreFull && (!sprites.TryGetEntry(key, out var markerEntry) || markerEntry.Sprite == null))
                {
                    // C-57 (T560): without the marker-sized icon the store-full marker falls back to the machine badge sprite (the old behaviour) rather than draw nothing.
                    key = SlotGridModel.BadgeStorageFullKey;
                }

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
        /// Resolves a world point inside the visible icon box of a marker (<see cref="AttentionModel.MarkerSizePixels"/> square) to the location it selects.
        /// The icon wins over a machine shape under it (<see cref="WorldTapResolver"/>). Allocation-free.
        /// </summary>
        public bool TryHitIcon(Vector2 world, out BuildingLocationId target)
        {
            var half = AttentionModel.MarkerSizePixels * 0.5f / SlotGridLayout.Density;
            for (var i = visibleCount - 1; i >= 0; i--)
            {
                if (Mathf.Abs(world.x - centres[i].x) <= half && Mathf.Abs(world.y - centres[i].y) <= half)
                {
                    target = targets[i];
                    return true;
                }
            }

            target = default;
            return false;
        }

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
