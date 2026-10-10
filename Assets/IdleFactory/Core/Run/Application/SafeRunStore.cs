using System;
using System.IO;
using System.Security;
using IdleFactory.Core.Application;
using IdleFactory.Core.Numerics;
using IdleFactory.Domain;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// Store access of <see cref="RunPersistence"/>: an infrastructure exception becomes a typed rejection. Moved out of
    /// <see cref="RunPersistence"/> unchanged (T302); it holds no state beyond the store it wraps.
    /// </summary>
    internal sealed class SafeRunStore
    {
        internal const string StorageMessage = "Checkpoint storage is unavailable.";

        private readonly IRunCheckpointStore store;

        public SafeRunStore(IRunCheckpointStore store) => this.store = store;

        public RunCheckpointReadResult ReadActive() => SafeRead(() => store.ReadActive());

        public RunCheckpointReadResult ReadPrevious() => SafeRead(() => store.ReadPrevious());

        public RunCheckpointReadResult Read(CheckpointId id) => SafeRead(() => store.Read(id));

        private static RunCheckpointReadResult SafeRead(Func<RunCheckpointReadResult> read)
        {
            try
            {
                return read() ?? RunCheckpointReadResult.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return RunCheckpointReadResult.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
        }

        public CommandResult<CheckpointId> Promote(RunCheckpoint checkpoint)
        {
            try
            {
                return store.Promote(checkpoint) ?? CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
        }

        public CommandResult<long> NextSupersedeGeneration()
        {
            try
            {
                return store.NextSupersedeGeneration() ?? CommandResult<long>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return CommandResult<long>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
        }

        public CommandResult<CheckpointId> SupersedeDamaged(RunCheckpoint checkpoint)
        {
            try
            {
                return store.SupersedeDamaged(checkpoint) ??
                       CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
        }

        public CommandResult<CheckpointId> RecoverPrevious(RunCheckpoint checkpoint)
        {
            try
            {
                return store.RecoverPrevious(checkpoint) ??
                       CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
        }

        public CommandResult<RunCheckpointPresence> Probe()
        {
            try
            {
                return store.ProbeFiles() ??
                       CommandResult<RunCheckpointPresence>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return CommandResult<RunCheckpointPresence>.Rejected(CommandOutcome.PersistenceFailed, StorageMessage);
            }
        }

        internal static bool IsExpectedInfrastructureFailure(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException ||
                   exception is InvalidOperationException || exception is NotSupportedException ||
                   exception is PlatformNotSupportedException || exception is ArgumentException || exception is OverflowException;
        }
    }
}
