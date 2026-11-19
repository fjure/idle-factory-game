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
        private readonly Action beforeAccess;

        /// <param name="beforeAccess">
        /// Runs before every access from the owning thread (T559): <see cref="RunPersistence"/> joins an autosave in flight there, so
        /// no command reads or writes the store with a background write of the same process still outstanding and unapplied.
        /// </param>
        public SafeRunStore(IRunCheckpointStore store, Action beforeAccess = null)
        {
            this.store = store;
            this.beforeAccess = beforeAccess;
        }

        /// <summary>The background capability of the wrapped store, or null (T559).</summary>
        public bool SupportsBackgroundPromotion => store is IRunCheckpointBackgroundPromoter;

        /// <summary>Serializes the checkpoint for a background publication, or null when the store cannot or the serialization failed (T559).</summary>
        public IPreparedPromotion TryPreparePromotion(RunCheckpoint checkpoint)
        {
            try
            {
                return (store as IRunCheckpointBackgroundPromoter)?.PreparePromotion(checkpoint);
            }
            catch (Exception exception) when (IsExpectedInfrastructureFailure(exception))
            {
                return null;
            }
        }

        public RunCheckpointReadResult ReadActive() => SafeRead(() => { beforeAccess?.Invoke(); return store.ReadActive(); });

        public RunCheckpointReadResult ReadPrevious() => SafeRead(() => { beforeAccess?.Invoke(); return store.ReadPrevious(); });

        public RunCheckpointReadResult Read(CheckpointId id) => SafeRead(() => { beforeAccess?.Invoke(); return store.Read(id); });

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
                beforeAccess?.Invoke();
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
                beforeAccess?.Invoke();
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
                beforeAccess?.Invoke();
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
                beforeAccess?.Invoke();
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
                beforeAccess?.Invoke();
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
