using IdleFactory.Core.Application;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// Read-only access to the Feature 001 Save V1 active checkpoint, the only input of the V1 import
    /// (FR-028, R10). An implementation never creates, writes, flushes, moves or deletes a file, so the
    /// V1 directory stays byte-identical whatever the import does (SC-005).
    /// </summary>
    public interface ILegacyCheckpointSource
    {
        /// <summary>
        /// A fixed, privacy-safe description of the source for diagnostics. It never contains a file-system
        /// path or text taken from a save file (SEC-5, Feature 002 T084).
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Reads the active V1 checkpoint through the unchanged Save V1 generation protocol and mapper.
        /// Returns true with the checkpoint when one is present and valid. Otherwise returns false and
        /// <paramref name="outcome"/> distinguishes an absent V1 save (<see cref="CommandOutcome.NoCheckpoint"/>)
        /// from an unreadable (<see cref="CommandOutcome.PersistenceFailed"/>) or invalid
        /// (<see cref="CommandOutcome.InvalidCheckpoint"/>) one.
        /// </summary>
        bool TryReadActive(out Checkpoint checkpoint, out CommandOutcome outcome);
    }
}
