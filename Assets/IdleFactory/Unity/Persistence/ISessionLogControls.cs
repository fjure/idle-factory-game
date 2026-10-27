namespace IdleFactory.Unity.Persistence
{
    /// <summary>
    /// What the Market session-log section may do with the session log (T340): read and switch recording, clear the files and export. It is the
    /// only way the UI reaches the writer; every call goes through the writer's own serialization (<c>_ioGate</c>), so the section never touches
    /// a segment file. No member throws, none changes the run, and the paths in <see cref="LastExport"/> are for the screen only (SEC-2): they are
    /// never logged, never persisted and never sent anywhere.
    /// </summary>
    public interface ISessionLogControls
    {
        /// <summary>Whether recording is on.</summary>
        bool Enabled { get; }

        /// <summary>Switches recording on or off and persists the setting.</summary>
        void SetEnabled(bool enabled);

        /// <summary>Truncates the log. False when the files could not be truncated.</summary>
        bool Clear();

        /// <summary>Exports the log and remembers a successful result as <see cref="LastExport"/>.</summary>
        SessionLogExportResult Export();

        /// <summary>The last successful export of this process, or a default result (null file path) before the first one. Memory only.</summary>
        SessionLogExportResult LastExport { get; }
    }
}
