using System;
using IdleFactory.Domain;

namespace IdleFactory.Core.Application
{
    public interface IUtcClock
    {
        DateTimeOffset GetUtcNow();
    }

    public sealed class Checkpoint
    {
        private readonly FactoryState factory;

        public Checkpoint(
            CheckpointId id,
            long generation,
            DateTimeOffset departureUtc,
            OfflinePolicyRevision offlinePolicyRevision,
            CheckpointId? reconciledFromCheckpointId,
            FactoryState factory)
        {
            _ = id.Value;
            _ = offlinePolicyRevision.Value;
            if (generation < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(generation));
            }

            Id = id;
            Generation = generation;
            DepartureUtc = departureUtc.ToUniversalTime();
            OfflinePolicyRevision = offlinePolicyRevision;
            ReconciledFromCheckpointId = reconciledFromCheckpointId;
            this.factory = (factory ?? throw new ArgumentNullException(nameof(factory))).Copy();
        }

        public CheckpointId Id { get; }
        public long Generation { get; }
        public DateTimeOffset DepartureUtc { get; }
        public OfflinePolicyRevision OfflinePolicyRevision { get; }
        public CheckpointId? ReconciledFromCheckpointId { get; }

        public FactoryState CreateFactoryCopy() => factory.Copy();
    }

    public sealed class CheckpointReadResult
    {
        private CheckpointReadResult(CommandOutcome outcome, Checkpoint checkpoint, string message)
        {
            Outcome = outcome;
            Checkpoint = checkpoint;
            Message = message ?? string.Empty;
        }

        public CommandOutcome Outcome { get; }
        public Checkpoint Checkpoint { get; }
        public string Message { get; }
        public bool Succeeded => Outcome == CommandOutcome.Success;

        public static CheckpointReadResult Success(Checkpoint checkpoint) =>
            new CheckpointReadResult(
                CommandOutcome.Success,
                checkpoint ?? throw new ArgumentNullException(nameof(checkpoint)),
                string.Empty);

        public static CheckpointReadResult Rejected(CommandOutcome outcome, string message)
        {
            if (outcome == CommandOutcome.Success)
            {
                throw new ArgumentException("A rejected read requires a non-success outcome.", nameof(outcome));
            }

            return new CheckpointReadResult(outcome, null, message);
        }
    }

    public interface ICheckpointStore
    {
        CheckpointReadResult ReadActive();
        CheckpointReadResult ReadPrevious();
        CheckpointReadResult Read(CheckpointId checkpointId);
        CommandResult<CheckpointId> Promote(Checkpoint checkpoint);
        CommandResult<CheckpointId> RecoverPrevious(Checkpoint recoveryCandidate);
    }
}
