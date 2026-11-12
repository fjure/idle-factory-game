namespace IdleFactory.Unity.UI
{
    /// <summary>
    /// The sheet state of the run shell (R11, T511): whether the one sheet is open and whether it shows Settings. It is plain
    /// state with no UI Toolkit dependency, so the back policy can be unit-tested. <see cref="RunShellDocument"/> owns the
    /// instance and keeps all panel and focus work.
    /// </summary>
    internal sealed class ShellSheetHost
    {
        /// <summary>Whether the sheet is open.</summary>
        public bool IsOpen { get; set; }

        /// <summary>Whether the open sheet is the Settings sheet.</summary>
        public bool IsSettingsOpen { get; set; }

        /// <summary>
        /// Whether Back closes the sheet: it must be open and show a slot sheet or a non-Factory tab. An open sheet without
        /// either is the always-present side panel, which Back leaves alone.
        /// </summary>
        public bool IsBackClosable(bool slotSelected, bool nonFactoryTabActive) => IsOpen && (slotSelected || nonFactoryTabActive);

        /// <summary>Clears all state, as the shell does when it is torn down.</summary>
        public void Reset()
        {
            IsOpen = false;
            IsSettingsOpen = false;
        }
    }
}
