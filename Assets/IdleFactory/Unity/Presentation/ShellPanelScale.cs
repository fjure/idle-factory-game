using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>Why the shell kept or raised the panel scale for a device profile (recorded; unknown DPI is the fallback case).</summary>
    public enum ShellScaleReason
    {
        /// <summary>A pointer device keeps today's authored scale.</summary>
        PointerDevice = 0,

        /// <summary>A touch device whose DPI is unknown keeps the authored scale (the fallback, recorded).</summary>
        UnknownDpiFallback = 1,

        /// <summary>A touch device whose authored scale already makes a 44 pt target at least 7 mm.</summary>
        AuthoredScaleLargeEnough = 2,

        /// <summary>A touch device whose scale was raised so that a 44 pt target is at least 7 mm.</summary>
        RaisedForTouchTarget = 3,
    }

    /// <summary>The outcome of <see cref="ShellPanelScale.Decide"/>.</summary>
    public readonly struct ShellScaleDecision
    {
        public ShellScaleDecision(ShellScaleReason reason, float authoredPixelsPerPoint, float requiredPixelsPerPoint, float effectivePixelsPerPoint)
        {
            Reason = reason;
            AuthoredPixelsPerPoint = authoredPixelsPerPoint;
            RequiredPixelsPerPoint = requiredPixelsPerPoint;
            EffectivePixelsPerPoint = effectivePixelsPerPoint;
        }

        public ShellScaleReason Reason { get; }
        public float AuthoredPixelsPerPoint { get; }
        public float RequiredPixelsPerPoint { get; }

        /// <summary>The panel scale in force: the raised scale, or the authored one.</summary>
        public float EffectivePixelsPerPoint { get; }

        public bool Raised => Reason == ShellScaleReason.RaisedForTouchTarget;

        public override string ToString() =>
            $"{Reason}: authored {AuthoredPixelsPerPoint:0.###} px/pt, required {RequiredPixelsPerPoint:0.###}, effective {EffectivePixelsPerPoint:0.###}";
    }

    /// <summary>
    /// The panel scale rule of the device profile (T191): on a touch device with a known DPI a 44 pt control is at least 7 mm
    /// physically, so the authored scale of <c>CoreGameFoundationPanelSettings</c> is raised to a constant pixel scale when it
    /// is lower. A pointer device or an unknown DPI keeps the authored scale. The raise is applied to the live
    /// <see cref="PanelSettings"/> at runtime and restored with <see cref="Restore"/>, so the authored asset is never changed.
    /// </summary>
    public static class ShellPanelScale
    {
        /// <summary>The scale (pixels per point) the panel settings produce for a screen size without a runtime override.</summary>
        public static float AuthoredPixelsPerPoint(PanelSettings settings, float screenWidthPx, float screenHeightPx, float dpi)
        {
            if (settings == null || screenWidthPx <= 0f || screenHeightPx <= 0f)
            {
                return 1f;
            }

            switch (settings.scaleMode)
            {
                case PanelScaleMode.ConstantPixelSize:
                    return Positive(settings.scale);
                case PanelScaleMode.ConstantPhysicalSize:
                    var referenceDpi = settings.referenceDpi > 0f ? settings.referenceDpi : 96f;
                    var actualDpi = dpi > 0f ? dpi : (settings.fallbackDpi > 0f ? settings.fallbackDpi : 96f);
                    return Positive(actualDpi / referenceDpi);
                default:
                    var reference = settings.referenceResolution;
                    var widthRatio = screenWidthPx / Mathf.Max(1f, reference.x);
                    var heightRatio = screenHeightPx / Mathf.Max(1f, reference.y);
                    switch (settings.screenMatchMode)
                    {
                        case PanelScreenMatchMode.Expand:
                            return Positive(Mathf.Min(widthRatio, heightRatio));
                        case PanelScreenMatchMode.Shrink:
                            return Positive(Mathf.Max(widthRatio, heightRatio));
                        default:
                            // UI Toolkit interpolates the two ratios linearly (measured: 1080x2400 gives 2.09 in portrait and
                            // 1.69 in landscape with the authored settings), unlike the logarithmic blend of uGUI.
                            return Positive(Mathf.Lerp(widthRatio, heightRatio, Mathf.Clamp01(settings.match)));
                    }
            }
        }

        /// <summary>Decides the panel scale for a profile and the authored scale (pure; no Unity object needed).</summary>
        public static ShellScaleDecision Decide(ShellDeviceProfile profile, float authoredPixelsPerPoint)
        {
            var authored = Positive(authoredPixelsPerPoint);
            if (!profile.IsTouch)
            {
                return new ShellScaleDecision(ShellScaleReason.PointerDevice, authored, 0f, authored);
            }

            if (!profile.HasKnownDpi)
            {
                return new ShellScaleDecision(ShellScaleReason.UnknownDpiFallback, authored, 0f, authored);
            }

            var required = profile.RequiredPixelsPerPoint;
            return authored >= required
                ? new ShellScaleDecision(ShellScaleReason.AuthoredScaleLargeEnough, authored, required, authored)
                : new ShellScaleDecision(ShellScaleReason.RaisedForTouchTarget, authored, required, required);
        }

        /// <summary>
        /// Applies a decision to the live panel settings: a raise switches them to a constant pixel scale. Returns the values
        /// to hand to <see cref="Restore"/>.
        /// </summary>
        public static SavedScale Apply(PanelSettings settings, ShellScaleDecision decision)
        {
            var saved = new SavedScale(settings);
            if (settings != null && decision.Raised)
            {
                settings.scaleMode = PanelScaleMode.ConstantPixelSize;
                settings.scale = decision.EffectivePixelsPerPoint;
            }

            return saved;
        }

        /// <summary>Puts the authored scale mode and scale back.</summary>
        public static void Restore(SavedScale saved)
        {
            if (saved.Settings != null)
            {
                saved.Settings.scaleMode = saved.ScaleMode;
                saved.Settings.scale = saved.Scale;
            }
        }

        private static float Positive(float value) =>
            value <= 0f || float.IsNaN(value) || float.IsInfinity(value) ? 1f : value;

        /// <summary>The authored scale mode and scale of a panel settings object, for <see cref="Restore"/>.</summary>
        public readonly struct SavedScale
        {
            public SavedScale(PanelSettings settings)
            {
                Settings = settings;
                ScaleMode = settings != null ? settings.scaleMode : PanelScaleMode.ConstantPixelSize;
                Scale = settings != null ? settings.scale : 1f;
            }

            public PanelSettings Settings { get; }
            public PanelScaleMode ScaleMode { get; }
            public float Scale { get; }
        }
    }
}
