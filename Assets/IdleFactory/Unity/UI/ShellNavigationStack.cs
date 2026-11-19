namespace IdleFactory.Unity.UI
{
    /// <summary>What <see cref="ShellNavigationStack"/> asks of the shell: which back frames exist and how to pop each one.</summary>
    internal interface IShellBackFrames
    {
        /// <summary>T540: a milestone banner is showing. It is the lightest frame: Back dismisses it first; a dialog, pin or the settings stay for the next Back, a sheet or card closes with it (C-60). Defaults to false.</summary>
        bool HasBanner => false;
        // C-52 (T560, decision: document): the banner pops before an open confirm dialog. It is a transient overlay that never takes focus, so the first Back
        // clears the lightest thing on screen and leaves the dialog (and its focus) alone; the second Back answers the dialog. Pinned by
        // MilestoneBannerTests.BackDismissesOnlyTheBannerAndLeavesAnOpenDialogAlone.

        void PopBanner()
        {
        }

        bool HasDialog { get; }

        bool HasPinnedValue { get; }

        bool IsSettingsOpen { get; }

        bool HasClosableSheet { get; }

        void PopDialog();

        void PopPinnedValue();

        void PopSettings();

        void PopSheet();

        /// <summary>T534: the machine card is open (docked, no sheet). Defaults to false so existing implementers stay valid.</summary>
        bool HasCard => false;

        void PopCard()
        {
        }
    }

    /// <summary>
    /// The back-frame order of the run shell (R11, T511, T534): dialog, pinned value, settings, sheet (Details), machine card. One back press pops exactly one
    /// frame, and the press is idempotent within a frame so a key event and the input poll never act twice. Plain C#, no UI
    /// Toolkit dependency. <c>ShellTab</c>, <c>SelectTab</c> and <c>OpenTabAtSection</c> stay public on <see cref="RunShellDocument"/> as the shim the hub cards, the Prestige panel and the test navigation use to open a system view (the tab bar is gone, T537); the stack itself does not know them.
    /// </summary>
    internal sealed class ShellNavigationStack
    {
        private int lastBackFrame = -1;

        /// <summary>
        /// Pops one frame. Returns whether the press was consumed (false on an empty stack). With <paramref name="frameGuard"/>
        /// a second call in the frame of a consumed press returns true without acting again.
        /// </summary>
        public bool HandleBack(IShellBackFrames frames, int frame, bool frameGuard)
        {
            if (lastBackFrame == frame && frameGuard)
            {
                return true;
            }

            bool consumed;
            if (frames.HasBanner)
            {
                frames.PopBanner();
                consumed = true;
                // C-60 (T560, U-C8): the banner announces what just happened on the open surface (Expand factory in Market), so the one Back that clears it also
                // takes the sheet or card under it; otherwise the first Back "did nothing" and Market stayed open. A dialog, a pinned value or the settings
                // stay: they hold focus and a decision, so the banner alone pops there (C-52).
                if (!frames.HasDialog && !frames.HasPinnedValue && !frames.IsSettingsOpen)
                {
                    if (frames.HasClosableSheet)
                    {
                        frames.PopSheet();
                    }
                    else if (frames.HasCard)
                    {
                        frames.PopCard();
                    }
                }
            }
            else if (frames.HasDialog)
            {
                frames.PopDialog();
                consumed = true;
            }
            else if (frames.HasPinnedValue)
            {
                frames.PopPinnedValue();
                consumed = true;
            }
            else if (frames.IsSettingsOpen)
            {
                frames.PopSettings();
                consumed = true;
            }
            else if (frames.HasClosableSheet)
            {
                frames.PopSheet();
                consumed = true;
            }
            else if (frames.HasCard)
            {
                frames.PopCard();
                consumed = true;
            }
            else
            {
                consumed = false;
            }

            if (consumed)
            {
                lastBackFrame = frame;
            }

            return consumed;
        }
    }
}
