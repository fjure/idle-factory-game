using System;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// The per-frame decision of the run screen framing, extracted from the bootstrap so it is testable against a real <see cref="FactoryCameraController"/> (T558):
    /// the first resolved rectangle, a rotation or a layout change fit the world once; later changes re-fit only when the scale no longer fits; a selection keeps its
    /// machine in the band above (or beside) an open card or sheet; and closing it brings the prior view back.
    /// <para>
    /// C-F2: the prior view belongs to one screen. A rotation under an open card fits the world to the card's band of the new screen, so the remembered closed rectangle
    /// (taken before the card opened, on the old screen) and the camera's own prior view are both for a screen that no longer exists. Closing the card then must not restore them:
    /// it re-fits the world to the closed rectangle of the new screen, and keeps doing so on the next frame in case the layout had not settled yet.
    /// </para>
    /// </summary>
    public sealed class RunViewportDriver
    {
        private readonly FactoryCameraController controller;
        private ScaleLayout observedLayout;
        private bool hasObservedLayout;
        private Rect lastClosedRect;
        private ScaleLayout lastClosedLayout;
        private bool hasLastClosed;
        private bool selectionChanged;

        public RunViewportDriver(FactoryCameraController controller)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
        }

        /// <summary>The camera controller this driver was created for (C-64): the owner recreates the driver when its controller is replaced.</summary>
        public FactoryCameraController Controller => controller;

        /// <summary>True while the next <see cref="Apply"/> fits the whole world again (first rectangle, rotation, a new world layout, new settings).</summary>
        public bool FramingPending { get; set; }

        /// <summary>Forgets the observed layout (the camera was configured anew).</summary>
        public void ResetObserved() => hasObservedLayout = false;

        /// <summary>A selection started, moved or ended: the next <see cref="Apply"/> reopens the sheet for it.</summary>
        public void NotifySelectionChanged() => selectionChanged = true;

        /// <summary>Applies one frame. <paramref name="machineRect"/> is the world rectangle kept in the band while <paramref name="wantSheet"/> holds.</summary>
        public void Apply(ScaleLayout layout, Rect visibleArea, bool wantSheet, Rect machineRect)
        {
            var fit = FramingPending || !controller.HasLayout;
            var structural = hasObservedLayout && !SameScreen(observedLayout, layout);
            var changed = !hasObservedLayout || structural || observedLayout.UnobstructedRect != visibleArea;
            var closedNow = false;

            if (fit)
            {
                FramingPending = false;
                controller.Fit(layout);
                changed = true;
                if (wantSheet)
                {
                    // The fit ran on the card's band: the sheet prior view the next OpenSheet stores is band-sized, so the remembered closed view is no longer restorable.
                    hasLastClosed = false;
                }
            }
            else if (!wantSheet && controller.SheetOpen)
            {
                if (hasLastClosed && SameScreen(lastClosedLayout, layout))
                {
                    // The prior view returns at once; the rectangle that is still the band is not remembered as the closed one.
                    controller.CloseSheet(lastClosedRect);
                }
                else
                {
                    // C-F2: the card was opened on another screen (a rotation or resize under it). There is no prior view to restore: fit the world to this
                    // screen now and once more next frame, when the closed rectangle has certainly settled.
                    controller.Fit(layout);
                    FramingPending = true;
                    hasLastClosed = false;
                }

                closedNow = true;
            }
            else if (changed && structural && wantSheet)
            {
                // The model's sheet band carries the old screen size: fit again, the selection then reopens its sheet below.
                controller.Fit(layout);
                hasLastClosed = false;
            }
            else if (changed && !wantSheet)
            {
                controller.UpdateLayout(layout);
            }

            if (wantSheet && (changed || selectionChanged || !controller.SheetOpen))
            {
                // SC-002: the whole machine (plate and badge band) stays in the band the card or sheet leaves, not only its plate.
                controller.OpenSheet(visibleArea, machineRect);
            }

            selectionChanged = false;
            observedLayout = layout;
            hasObservedLayout = true;
            if (!wantSheet && !closedNow && !controller.SheetOpen)
            {
                lastClosedRect = visibleArea;
                lastClosedLayout = layout;
                hasLastClosed = true;
            }
        }

        private static bool SameScreen(ScaleLayout a, ScaleLayout b) =>
            a.PixelWidth == b.PixelWidth && a.PixelHeight == b.PixelHeight && a.LayoutId == b.LayoutId && Mathf.Approximately(a.PixelsPerPoint, b.PixelsPerPoint);
    }
}
