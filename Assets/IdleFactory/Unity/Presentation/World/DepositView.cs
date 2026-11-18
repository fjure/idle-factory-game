using System;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>What the ore vein shows (R23): the resting vein of the stage, the 6-frame chip loop over it, or the static highlight.</summary>
    public enum DepositMode
    {
        /// <summary>No manual mining: the stage's rest sprite.</summary>
        Rest = 0,

        /// <summary>Manual mining in progress, normal motion: the rest sprite with the chip loop over it, on the shared clock.</summary>
        Chip = 1,

        /// <summary>Manual mining in progress under reduced motion: the static highlight replaces the loop (no chip, nothing moves).</summary>
        Highlight = 2,
    }

    /// <summary>
    /// The ore-vein deposit of the world (T528, FR-010a, research R23, contracts/world-presentation.md section 2a): scenery on <see cref="FactoryWorldLayout.DepositCell"/>,
    /// a margin cell outside every hit shape and keep-out. It shows the rest sprite of the development stage (S1 to S3), and while manual mining is in progress the
    /// 6-frame chip loop on the shared clock; under reduced motion the static highlight shows instead and nothing animates.
    /// <para>
    /// <b>The vein is not a hit target.</b> The view has no collider and no hit test: a tap on it reaches nothing (the mining control stays the input, FR-010).
    /// It decides nothing and changes no economy value: <see cref="Present"/> only reads whether mining is in progress. Allocation-free once warm.
    /// </para>
    /// </summary>
    public sealed class DepositView : MonoBehaviour
    {
        public const string DepositSortingLayer = "WorldLinks";
        public const string EffectsSortingLayer = "Effects";

        /// <summary>The rest sprite draws above the link Tilemap and below the depot on the links layer.</summary>
        public const int RestOrder = 20;

        /// <summary>The chip loop draws on the Effects layer below the pips and the effect pool.</summary>
        public const int ChipOrder = -80;

        private PresentationSpriteCatalog sprites;
        private FactoryWorldLayout layout;
        private SlotGridView bound;
        private SpriteRenderer rest;
        private SpriteRenderer chip;
        private PresentationSpriteEntry chipEntry;
        private int stage;
        private DepositMode mode = DepositMode.Rest;
        private string shownRestKey;
        private bool placed;

        /// <summary>The mode the last <see cref="Present"/> showed.</summary>
        public DepositMode Mode => mode;

        public int Stage => stage;

        public SpriteRenderer RestRenderer => rest;
        public SpriteRenderer ChipRenderer => chip;

        /// <summary>The catalog key of the sprite the rest renderer shows (rest or highlight), or null.</summary>
        public string ShownRestKey => shownRestKey;

        /// <summary>The frame of the chip loop now shown (0 when it is not playing).</summary>
        public int ChipFrame { get; private set; }

        public void Configure(PresentationSpriteCatalog catalog)
        {
            sprites = catalog ?? throw new ArgumentNullException(nameof(catalog));
            EnsureRenderers();
            chipEntry = sprites.TryGetEntry(DepotLinkGeometry.VeinChipKey, out var entry) && entry.FrameCount > 0 ? entry : null;
            shownRestKey = null;
            placed = false;
        }

        public void Bind(SlotGridView slotGrid)
        {
            Unbind();
            bound = slotGrid ?? throw new ArgumentNullException(nameof(slotGrid));
            bound.LayoutChanged += SetLayout;
            SetLayout(bound.Layout);
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

        public void SetLayout(FactoryWorldLayout newLayout)
        {
            if (newLayout == null)
            {
                throw new ArgumentNullException(nameof(newLayout));
            }

            if (ReferenceEquals(layout, newLayout))
            {
                return;
            }

            layout = newLayout;
            placed = false;
        }

        /// <summary>Sets the development stage (0 to 2): it picks the rest sprite S1 to S3. T540 wires the stage model.</summary>
        public void SetStage(int newStage)
        {
            newStage = Mathf.Clamp(newStage, 0, WorldCompositionSettings.StageCount - 1);
            if (newStage == stage)
            {
                return;
            }

            stage = newStage;
            shownRestKey = null;
        }

        /// <summary>
        /// One frame. <paramref name="mining"/> is true while manual mining is in progress (the mining hold). <paramref name="milliseconds"/> is the shared clock.
        /// </summary>
        public void Present(bool mining, long milliseconds, bool reducedMotion)
        {
            if (sprites == null || layout == null || rest == null)
            {
                return;
            }

            if (!placed)
            {
                var position = new Vector3(layout.DepositAnchor.x, layout.DepositAnchor.y, 0f);
                rest.transform.localPosition = position;
                chip.transform.localPosition = position;
                placed = true;
            }

            mode = !mining ? DepositMode.Rest : reducedMotion ? DepositMode.Highlight : DepositMode.Chip;
            var restKey = mode == DepositMode.Highlight ? DepotLinkGeometry.DepositHighlightKey : DepotLinkGeometry.DepositRestKey(stage);
            if (!ReferenceEquals(restKey, shownRestKey))
            {
                shownRestKey = restKey;
                var hasRest = sprites.TryGetEntry(restKey, out var entry) && entry.Sprite != null;
                rest.sprite = hasRest ? entry.Sprite : null;
                rest.enabled = hasRest;
            }

            if (mode == DepositMode.Chip && chipEntry != null)
            {
                var fps = chipEntry.FramesPerSecond;
                ChipFrame = PresentationAnimationClock.FrameAt(milliseconds, fps.HasValue ? fps.Value : 0f, chipEntry.FrameCount);
                chip.sprite = chipEntry.GetFrame(ChipFrame);
                chip.enabled = true;
            }
            else
            {
                ChipFrame = 0;
                chip.enabled = false;
            }
        }

        private void EnsureRenderers()
        {
            if (rest != null)
            {
                return;
            }

            var material = FactoryDeckView.ProbeSlotMaterial();
            rest = NewRenderer("Vein", DepositSortingLayer, RestOrder, material);
            chip = NewRenderer("Vein Chip", EffectsSortingLayer, ChipOrder, material);
        }

        private SpriteRenderer NewRenderer(string name, string layer, int order, Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = SortingLayer.NameToID(layer);
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }

        private void OnDestroy() => Unbind();
    }
}
