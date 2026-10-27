using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>The input a player mainly uses: a touch screen, or a pointer (mouse) with a keyboard.</summary>
    public enum ShellPrimaryInput
    {
        Touch = 0,
        Pointer = 1,
    }

    /// <summary>The screen orientation of a device profile, derived from its pixel size.</summary>
    public enum ShellOrientation
    {
        Portrait = 0,
        Landscape = 1,
    }

    /// <summary>
    /// What the run shell knows about the device (UI surfaces contract, Global > Device profile; T191, owner decision
    /// 2026-10-07): the primary input, the screen size in pixels, the DPI where Unity reports one, and the orientation. It is a
    /// plain value computed at the presentation boundary (<see cref="Probe"/>), with <see cref="Create"/> and
    /// <see cref="Detect"/> as the test seam. The layout class is still chosen by panel-point size
    /// (<see cref="FactoryHudDocument.Classify"/>); the profile, not the layout class, decides touch behaviour (tap-to-pin and
    /// long press for exact values, hover only with a pointer) and the physical size of a 44 pt target.
    /// </summary>
    public readonly struct ShellDeviceProfile : IEquatable<ShellDeviceProfile>
    {
        /// <summary>The smallest physical size of a touch target on a touch device with a known DPI.</summary>
        public const float MinimumTouchTargetMillimeters = 7f;

        /// <summary>The points of a minimum control (the 44 pt rule of the UI surfaces contract).</summary>
        public const float MinimumTargetPoints = 44f;

        private const float MillimetersPerInch = 25.4f;

        private ShellDeviceProfile(ShellPrimaryInput input, int widthPx, int heightPx, float dpi)
        {
            PrimaryInput = input;
            ScreenWidthPx = Math.Max(0, widthPx);
            ScreenHeightPx = Math.Max(0, heightPx);
            Dpi = float.IsNaN(dpi) || float.IsInfinity(dpi) || dpi <= 0f ? 0f : dpi;
        }

        public ShellPrimaryInput PrimaryInput { get; }
        public int ScreenWidthPx { get; }
        public int ScreenHeightPx { get; }

        /// <summary>The reported DPI, or 0 when Unity does not know it (<see cref="Screen.dpi"/> is 0).</summary>
        public float Dpi { get; }

        public bool IsTouch => PrimaryInput == ShellPrimaryInput.Touch;
        public bool HasKnownDpi => Dpi > 0f;
        public ShellOrientation Orientation => ScreenWidthPx > ScreenHeightPx ? ShellOrientation.Landscape : ShellOrientation.Portrait;

        /// <summary>Hover shows the exact value only with a pointer; a touch device has no hover.</summary>
        public bool ShowsHover => PrimaryInput == ShellPrimaryInput.Pointer;

        /// <summary>A tap pins the exact-value detail line and a 500 ms press shows it, on a touch device.</summary>
        public bool PinsOnTapAndLongPress => PrimaryInput == ShellPrimaryInput.Touch;

        /// <summary>The physical screen width in millimetres, or 0 when the DPI is unknown.</summary>
        public float PhysicalWidthMillimeters => HasKnownDpi ? ScreenWidthPx / Dpi * MillimetersPerInch : 0f;

        /// <summary>The physical screen height in millimetres, or 0 when the DPI is unknown.</summary>
        public float PhysicalHeightMillimeters => HasKnownDpi ? ScreenHeightPx / Dpi * MillimetersPerInch : 0f;

        /// <summary>
        /// The panel scale (pixels per point) at which a 44 pt control is at least 7 mm: only for a touch device with a known
        /// DPI, otherwise 0 (no requirement; the authored scale stays).
        /// </summary>
        public float RequiredPixelsPerPoint => IsTouch && HasKnownDpi
            ? Dpi * MinimumTouchTargetMillimeters / MillimetersPerInch / MinimumTargetPoints * 1.0001f
            : 0f;

        /// <summary>The physical size in millimetres of a length in points at a panel scale; 0 when the DPI is unknown.</summary>
        public float MillimetersFor(float points, float pixelsPerPoint) =>
            HasKnownDpi ? points * pixelsPerPoint / Dpi * MillimetersPerInch : 0f;

        /// <summary>The profile used before a device is probed: touch, no known DPI, no size (the pre-T191 shell behaviour).</summary>
        public static ShellDeviceProfile Default => new ShellDeviceProfile(ShellPrimaryInput.Touch, 0, 0, 0f);

        public static ShellDeviceProfile Create(ShellPrimaryInput input, int widthPx, int heightPx, float dpi) =>
            new ShellDeviceProfile(input, widthPx, heightPx, dpi);

        public static ShellDeviceProfile Touch(int widthPx, int heightPx, float dpi) =>
            new ShellDeviceProfile(ShellPrimaryInput.Touch, widthPx, heightPx, dpi);

        public static ShellDeviceProfile Pointer(int widthPx, int heightPx, float dpi) =>
            new ShellDeviceProfile(ShellPrimaryInput.Pointer, widthPx, heightPx, dpi);

        /// <summary>
        /// Chooses the primary input: a mobile platform is touch; otherwise a touch screen without any mouse or keyboard is
        /// touch; everything else (a desktop, a laptop with a touch screen and a mouse) is a pointer device.
        /// </summary>
        public static ShellPrimaryInput Detect(bool mobilePlatform, bool hasTouchscreen, bool hasMouseOrKeyboard)
        {
            if (mobilePlatform)
            {
                return ShellPrimaryInput.Touch;
            }

            return hasTouchscreen && !hasMouseOrKeyboard ? ShellPrimaryInput.Touch : ShellPrimaryInput.Pointer;
        }

        /// <summary>Reads the profile of the running device from Unity screen, DPI and input-device data.</summary>
        public static ShellDeviceProfile Probe()
        {
            var input = Detect(
                Application.isMobilePlatform,
                Touchscreen.current != null,
                Mouse.current != null || Keyboard.current != null);
            return new ShellDeviceProfile(input, Screen.width, Screen.height, Screen.dpi);
        }

        public bool Equals(ShellDeviceProfile other) =>
            PrimaryInput == other.PrimaryInput && ScreenWidthPx == other.ScreenWidthPx &&
            ScreenHeightPx == other.ScreenHeightPx && Mathf.Approximately(Dpi, other.Dpi);

        public override bool Equals(object obj) => obj is ShellDeviceProfile other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(PrimaryInput, ScreenWidthPx, ScreenHeightPx, Dpi);

        public override string ToString() =>
            $"{PrimaryInput} {ScreenWidthPx}x{ScreenHeightPx}px {Orientation} " + (HasKnownDpi ? $"{Dpi:0.#} dpi" : "dpi unknown");
    }
}
