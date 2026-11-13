namespace IdleFactory.Unity.UI
{
    /// <summary>What <see cref="ShellNavigationStack"/> asks of the shell: which back frames exist and how to pop each one.</summary>
    internal interface IShellBackFrames
    {
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
    /// Toolkit dependency. The tab bar and <c>ShellTab</c> stay in <see cref="RunShellDocument"/> as a shim.
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
            if (frames.HasDialog)
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
