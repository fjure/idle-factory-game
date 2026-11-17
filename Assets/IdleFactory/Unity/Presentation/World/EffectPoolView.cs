using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// Draws an <see cref="EffectPool"/> (T529, research R12): exactly <see cref="EffectPool.RendererCount"/> sprite renderers on the <c>Effects</c>
    /// sorting layer, created once, so the world-layer budget of 8 FX renderers (contracts/world-presentation.md section 5) holds by construction.
    /// A renderer shows the pool slot of its index: the frame the pool computed, at the plate of the slot's location, with the pool's opacity (a
    /// draining tail fades). The view decides nothing: spawning, priority, coalescing and reduced motion live in the pool, so a pool under reduced
    /// motion has no occupied slot and the view hides every renderer. <see cref="Present"/> allocates nothing.
    /// </summary>
    public sealed class EffectPoolView : MonoBehaviour
    {
        public const string EffectsSortingLayer = "Effects";

        /// <summary>The sorting order of a decorative effect; a CompletionPop draws <see cref="PopOrderOffset"/> above it, and slot index breaks ties.</summary>
        public const int OrderBase = 0;

        public const int PopOrderOffset = 50;

        private readonly SpriteRenderer[] renderers = new SpriteRenderer[EffectPool.RendererCount];
        private readonly PresentationSpriteEntry[] entries = new PresentationSpriteEntry[EffectPool.KindCount];
        private PresentationSpriteCatalog sprites;
        private EffectPool pool;
        private FactoryWorldLayout layout;
        private SlotGridView bound;

        /// <summary>The pool this view draws.</summary>
        public EffectPool Pool => pool;

        /// <summary>The 8 renderers; one is enabled per occupied pool slot.</summary>
        public IReadOnlyList<SpriteRenderer> Renderers => renderers;

        /// <summary>True when the catalog carries the art of at least one effect kind (a smaller development catalog draws nothing, without error).</summary>
        public bool HasArt { get; private set; }

        /// <summary>Gives the view its catalog and pool, declares the art of each kind to the pool and creates the renderers once.</summary>
        public void Configure(PresentationSpriteCatalog catalog, EffectPool effectPool)
        {
            sprites = catalog ?? throw new ArgumentNullException(nameof(catalog));
            pool = effectPool ?? throw new ArgumentNullException(nameof(effectPool));
            HasArt = false;
            for (var kind = 0; kind < EffectPool.KindCount; kind++)
            {
                var key = EffectPool.SpriteKeyOf((EffectKind)kind);
                if (sprites.TryGetEntry(key, out var entry) && entry.FrameCount > 0)
                {
                    entries[kind] = entry;
                    var fps = entry.FramesPerSecond;
                    pool.SetKindArt((EffectKind)kind, entry.FrameCount, fps.HasValue ? Mathf.RoundToInt(fps.Value) : 0);
                    HasArt = true;
                }
                else
                {
                    entries[kind] = null;
                    pool.SetKindArt((EffectKind)kind, 0, 0);
                }
            }

            EnsureRenderers();
        }

        /// <summary>Follows a slot grid's layout now and later (the plate of each location is where an effect is drawn).</summary>
        public void Bind(SlotGridView slotGrid)
        {
            Unbind();
            bound = slotGrid ?? throw new ArgumentNullException(nameof(slotGrid));
            bound.LayoutChanged += SetLayout;
            SetLayout(bound.Layout);
        }

        public void SetLayout(FactoryWorldLayout newLayout)
        {
            layout = newLayout;
            if (pool != null && layout != null)
            {
                pool.EnsureLocations(layout.LocationCount);
            }
        }

        /// <summary>Shows the pool's slots: the frame, the position, the opacity and the draw order of each occupied one. Call once per frame after <see cref="EffectPool.Advance"/>.</summary>
        public void Present()
        {
            if (pool == null || renderers[0] == null)
            {
                return;
            }

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                var state = pool.StateOf(i);
                if (state == EffectSlotState.Free || layout == null || pool.LocationOf(i) >= layout.LocationCount)
                {
                    renderer.enabled = false;
                    continue;
                }

                var entry = entries[(int)pool.KindOf(i)];
                if (entry == null)
                {
                    renderer.enabled = false;
                    continue;
                }

                var frame = pool.FrameOf(i);
                if (frame >= entry.FrameCount)
                {
                    frame = entry.FrameCount - 1;
                }

                var centre = layout.PlateCentreOf(new BuildingLocationId(pool.LocationOf(i)));
                var colour = renderer.color;
                colour.a = pool.AlphaOf(i);
                renderer.color = colour;
                renderer.sprite = entry.GetFrame(frame);
                renderer.sortingOrder = OrderBase + (EffectPool.PriorityOf(pool.KindOf(i)) > 0 ? PopOrderOffset : 0) + i;
                renderer.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
                renderer.enabled = true;
            }
        }

        private void EnsureRenderers()
        {
            if (renderers[0] != null)
            {
                return;
            }

            var layerId = SortingLayer.NameToID(EffectsSortingLayer);
            for (var i = 0; i < renderers.Length; i++)
            {
                var child = new GameObject("Effect " + i);
                child.transform.SetParent(transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sortingLayerID = layerId;
                renderer.enabled = false;
                renderers[i] = renderer;
            }
        }

        private void Unbind()
        {
            if (bound == null)
            {
                return;
            }

            bound.LayoutChanged -= SetLayout;
            bound = null;
        }

        private void OnDestroy() => Unbind();
    }
}
