using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleFactory.Unity.Presentation
{
    internal sealed class SlotRenderers
    {
        private readonly Transform root;
        private readonly SortingGroup bodyGroup;
        private readonly SpriteRenderer plate;
        private readonly SpriteRenderer machine;
        private readonly SpriteRenderer accent;
        private readonly SpriteRenderer frame;
        private readonly SpriteRenderer label;
        private readonly SpriteRenderer badge;
        private readonly SpriteRenderer boosted;
        private readonly SpriteRenderer overdrive;
        private readonly SpriteRenderer[] all;
        private bool hasBoosted;
        private bool hasOverdrive;
        private bool hasLabel;
        private LabelPolicy labelPolicy = LabelPolicy.Never;
        private PresentationSpriteEntry machineEntry;
        private PresentationSpriteEntry accentEntry;
        private bool plateCanvas;
        private bool machineCanvas;
        private bool accentCanvas;
        private bool frameCanvas;
        private bool labelCanvas;
        private bool badgeCanvas;
        private bool boostedCanvas;
        private bool overdriveCanvas;
        private bool animated;
        private int machineFrame;
        private int accentFrame;

        public SlotRenderers(Transform parent, int index, int overlayLayerId)
        {
            var slotObject = new GameObject("Slot " + (index + 1));
            slotObject.transform.SetParent(parent, false);
            root = slotObject.transform;

            // The machine's one Sorting Group: plate, machine and accent sort among themselves and, as a unit, by slot row.
            var body = new GameObject("Body");
            body.transform.SetParent(root, false);
            bodyGroup = body.AddComponent<SortingGroup>();
            plate = Create(body.transform, "Plate", SlotGridView.PlateOrder, 0);
            machine = Create(body.transform, "Machine", SlotGridView.MachineOrder, 0);
            accent = Create(body.transform, "Accent", SlotGridView.AccentOrder, 0);

            // Overlays are outside the group, on the overlay layer, so no machine art of any row can cover them.
            frame = Create(root, "Selection", SlotGridView.FrameOrder, overlayLayerId);
            label = Create(root, "Label", SlotGridView.OverlayOrder, overlayLayerId);
            boosted = Create(root, "Boosted", SlotGridView.OverlayOrder + 1, overlayLayerId);
            overdrive = Create(root, "Overdrive", SlotGridView.OverlayOrder + 2, overlayLayerId);
            badge = Create(root, "Badge", SlotGridView.OverlayOrder + 3, overlayLayerId);
            all = new[] { plate, machine, accent, frame, label, badge, boosted, overdrive };
        }

        public IReadOnlyList<SpriteRenderer> All => all;

        public bool AccentActive => accent.enabled;

        public int ActiveCount
        {
            get
            {
                var count = 0;
                foreach (var renderer in all)
                {
                    count += renderer.enabled ? 1 : 0;
                }

                return count;
            }
        }

        public SpriteRenderer Get(SlotGridView.SlotRendererRole role)
        {
            switch (role)
            {
                case SlotGridView.SlotRendererRole.Plate: return plate;
                case SlotGridView.SlotRendererRole.Machine: return machine;
                case SlotGridView.SlotRendererRole.Accent: return accent;
                case SlotGridView.SlotRendererRole.Frame: return frame;
                case SlotGridView.SlotRendererRole.Label: return label;
                case SlotGridView.SlotRendererRole.Badge: return badge;
                case SlotGridView.SlotRendererRole.Boosted: return boosted;
                case SlotGridView.SlotRendererRole.Overdrive: return overdrive;
                default: return null;
            }
        }

        public void SetOrigin(Vector2 pivot, int row)
        {
            // The slot root sits on the footprint pivot; canvas layers and overlay anchors are relative to it.
            root.localPosition = new Vector3(pivot.x, pivot.y, 0f);
            bodyGroup.sortingOrder = row;
            frame.sortingOrder = SlotGridView.FrameOrder + row;
            label.sortingOrder = SlotGridView.OverlayOrder + row * 4;
            boosted.sortingOrder = SlotGridView.OverlayOrder + row * 4 + 1;
            overdrive.sortingOrder = SlotGridView.OverlayOrder + row * 4 + 2;
            badge.sortingOrder = SlotGridView.OverlayOrder + row * 4 + 3;
        }

        public void Show(SlotVisual visual, PresentationSpriteCatalog catalog, StateCueSet cues)
        {
            var hasRow = cues.TryGetRowForBadgeKey(visual.BadgeKey, out var cueRow);
            var running = hasRow && cueRow.State == MachineVisualState.Running;
            labelPolicy = StateCueSet.LabelPolicyFor(visual.Condition);

            // The catalog states per key whether the frames are a canvas layer (the shape of the requirement row they matched), so the
            // placement no longer depends on a sprite-size guess (T440 carry-forward).
            plateCanvas = IsCanvas(Apply(plate, catalog, visual.BaseKey), plate);
            machineEntry = Apply(machine, catalog, visual.MachineKey);
            machineCanvas = IsCanvas(machineEntry, machine);
            accentEntry = running ? Apply(accent, catalog, StateCueSet.AccentKeyFor(visual.MachineKey)) : Apply(accent, catalog, null);
            accentCanvas = IsCanvas(accentEntry, accent);
            animated = running;
            machineFrame = 0;
            accentFrame = 0;

            frameCanvas = IsCanvas(Apply(frame, catalog, visual.Selected ? StateCueSet.SelectionFrameKey : null), frame);
            var labelEntry = Apply(label, catalog, StateCueSet.LabelKeyFor(visual.Condition, visual.UnlockLevel));
            hasLabel = labelEntry != null;
            labelCanvas = IsCanvas(labelEntry, label);

            // One badge per occupied slot, from the cue row of the resolved state. The running row is intentionally empty
            // (owner decision R8): its key resolves to an entry without a sprite and the view draws nothing for it.
            badgeCanvas = IsCanvas(Apply(badge, catalog, hasRow && !cueRow.IntentionallyEmpty ? cueRow.CueIconKey : null), badge);
            var boostedEntry = Apply(boosted, catalog, visual.Boosted ? StateCueSet.MarkerKeys[0] : null);
            hasBoosted = boostedEntry != null;
            boostedCanvas = IsCanvas(boostedEntry, boosted);
            var overdriveEntry = Apply(overdrive, catalog, visual.Overdrive ? StateCueSet.MarkerKeys[1] : null);
            hasOverdrive = overdriveEntry != null;
            overdriveCanvas = IsCanvas(overdriveEntry, overdrive);

            PlaceLayer(plate, plateCanvas);
            PlaceLayer(machine, machineCanvas);
            PlaceLayer(accent, accentCanvas);
            PlaceLayer(frame, frameCanvas);
            PlaceLayer(label, labelCanvas);
        }

        public void Hide()
        {
            foreach (var renderer in all)
            {
                renderer.enabled = false;
            }

            hasBoosted = hasOverdrive = hasLabel = false;
            machineEntry = accentEntry = null;
            animated = false;
        }

        public void Advance(long milliseconds, bool frozen)
        {
            if (!animated)
            {
                return;
            }

            Step(machine, machineEntry, milliseconds, frozen, ref machineFrame);
            Step(accent, accentEntry, milliseconds, frozen, ref accentFrame);
        }

        public void ApplyOverlay(float badgeScale, float markerScale, bool markersVisible, float pointsPerCell)
        {
            // A canvas layer (the F004 art) is authored at its final size and keeps scale 1; only a tight placeholder scales.
            var badgeFactor = badgeCanvas ? 1f : badgeScale;
            badge.transform.localScale = new Vector3(badgeFactor, badgeFactor, 1f);
            badge.transform.localPosition = BadgePosition(badge.sprite, badgeFactor, badgeCanvas);
            PlaceMarker(boosted, boostedCanvas, markerScale, StateCueGeometry.Marker1Offset);
            PlaceMarker(overdrive, overdriveCanvas, markerScale, StateCueGeometry.Marker2Offset);
            boosted.enabled = hasBoosted && markersVisible;
            overdrive.enabled = hasOverdrive && markersVisible;
            label.enabled = hasLabel && labelPolicy.IsShown(pointsPerCell, StateCueGeometry.ArtPixelsPerCell);
        }

        public void Destroy()
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root.gameObject);
            }
        }

        // A canvas layer sits on the footprint pivot (the slot root); the F003 placeholders keep their bottom-left pivot at the former
        // slot corner, 1 cell left of and 15 art px below the pivot.
        private static readonly Vector2 PlaceholderCorner = new Vector2(-1f, -SlotGridLayout.PivotAbovePlateBottomPixels / (float)SlotGridLayout.Density);

        private static bool IsCanvas(PresentationSpriteEntry entry, SpriteRenderer renderer) =>
            entry != null && entry.IsCanvasLayer(renderer.sprite);

        private static void PlaceLayer(SpriteRenderer renderer, bool canvas)
        {
            var pivot = canvas ? Vector2.zero : PlaceholderCorner;
            renderer.transform.localPosition = new Vector3(pivot.x, pivot.y, 0f);
        }

        private static Vector3 BadgePosition(Sprite sprite, float factor, bool canvas)
        {
            var pivot = Vector2.zero;
            if (sprite == null || canvas)
            {
                return new Vector3(pivot.x, pivot.y, 0f);
            }

            // A tight (centre pivot) placeholder: its bottom-centre rests on the Art Bible anchor (59, 33).
            var height = sprite.rect.height / sprite.pixelsPerUnit * factor;
            var anchor = pivot + StateCueGeometry.BadgeAnchorOffset;
            return new Vector3(anchor.x, anchor.y + height * 0.5f, 0f);
        }

        private static void PlaceMarker(SpriteRenderer renderer, bool canvas, float markerScale, Vector2 offset)
        {
            var factor = canvas ? 1f : markerScale;
            renderer.transform.localScale = new Vector3(factor, factor, 1f);
            var center = canvas ? Vector2.zero : offset;
            renderer.transform.localPosition = new Vector3(center.x, center.y, 0f);
        }

        private static void Step(SpriteRenderer renderer, PresentationSpriteEntry entry, long milliseconds, bool frozen, ref int shown)
        {
            if (entry == null || !renderer.enabled)
            {
                return;
            }

            var frameIndex = frozen ? 0 : SlotGridView.FrameAt(milliseconds, entry.FramesPerSecond ?? 0f, entry.FrameCount);
            if (frameIndex == shown)
            {
                return;
            }

            shown = frameIndex;
            renderer.sprite = entry.GetFrame(frameIndex);
        }

        private SpriteRenderer Create(Transform parent, string name, int order, int layerId)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sortingLayerID = layerId;
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }

        private static PresentationSpriteEntry Apply(SpriteRenderer renderer, PresentationSpriteCatalog catalog, string key)
        {
            if (key != null && catalog.TryGetEntry(key, out var entry))
            {
                renderer.sprite = entry.Sprite;
                renderer.enabled = true;
                return entry;
            }

            renderer.sprite = null;
            renderer.enabled = false;
            return null;
        }
    }
}
