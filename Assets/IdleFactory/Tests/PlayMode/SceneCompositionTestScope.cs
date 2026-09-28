using System;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>Scopes the authored scene to deterministic clocks and an in-memory checkpoint store.</summary>
    internal sealed class SceneCompositionTestScope : IDisposable
    {
        private readonly IDisposable compositionOverride;
        private bool disposed;

        private SceneCompositionTestScope(bool seedEmptyCheckpoint, Func<DefinitionCatalog, FactoryState> seedFactory = null)
        {
            Store = new IsolatedCheckpointStore();
            Clock = new SceneCompositionTestClock();
#if UNITY_EDITOR
            compositionOverride = CoreGameFoundationBootstrap.OverrideCompositionDependenciesForTests(
                catalog =>
                {
                    if (seedFactory != null)
                    {
                        Store.SeedCheckpoint(seedFactory(catalog), Clock.UtcNow);
                    }
                    else if (seedEmptyCheckpoint)
                    {
                        Store.SeedEmptyCheckpoint(catalog, Clock.UtcNow);
                    }

                    return Store;
                },
                () => Clock.UtcNow,
                () => Clock.CumulativeActiveSeconds);
#else
            throw new PlatformNotSupportedException("Scene composition overrides are available only in the Editor.");
#endif
        }

        public IsolatedCheckpointStore Store { get; }
        public SceneCompositionTestClock Clock { get; }

        public static SceneCompositionTestScope WithNoCheckpoint() => new SceneCompositionTestScope(false);

        /// <summary>Preserves empty-world assumptions while preventing a scene test from reaching persistentDataPath.</summary>
        public static SceneCompositionTestScope WithSeededEmptyCheckpoint() => new SceneCompositionTestScope(true);

        /// <summary>Seeds the active checkpoint with a factory built against the scene's own catalog.</summary>
        public static SceneCompositionTestScope WithSeededCheckpoint(Func<DefinitionCatalog, FactoryState> seedFactory) =>
            new SceneCompositionTestScope(false, seedFactory ?? throw new ArgumentNullException(nameof(seedFactory)));

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            compositionOverride.Dispose();
        }
    }

    internal sealed class SceneCompositionTestClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        public double CumulativeActiveSeconds { get; set; }
    }

    internal sealed class IsolatedCheckpointStore : ICheckpointStore
    {
        public Checkpoint Active { get; private set; }
        public Checkpoint Previous { get; private set; }
        public int ActiveReadCount { get; private set; }
        public int PromotionCount { get; private set; }
        public bool FailNextPromotion { get; set; }

        public void SeedEmptyCheckpoint(DefinitionCatalog catalog, DateTimeOffset departureUtc)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (Active != null)
            {
                throw new InvalidOperationException("The isolated scene store already has an active checkpoint.");
            }

            SeedCheckpoint(
                new FactoryState(
                    new FactoryBounds(new GridPosition(0, 0), 16, 16),
                    catalog,
                    Array.Empty<BuildingInstanceState>()),
                departureUtc);
        }

        public void SeedCheckpoint(FactoryState factory, DateTimeOffset departureUtc)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (Active != null)
            {
                throw new InvalidOperationException("The isolated scene store already has an active checkpoint.");
            }

            Active = new Checkpoint(
                new CheckpointId(Guid.NewGuid()),
                1L,
                departureUtc,
                new OfflinePolicyRevision(DefinitionCatalog.SupportedOfflinePolicyRevision),
                null,
                factory);
        }

        public CheckpointReadResult ReadActive()
        {
            ActiveReadCount++;
            return Active == null
                ? CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No isolated scene checkpoint exists.")
                : CheckpointReadResult.Success(Clone(Active));
        }

        public CheckpointReadResult ReadPrevious() => Previous == null
            ? CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "No previous isolated scene checkpoint exists.")
            : CheckpointReadResult.Success(Clone(Previous));

        public CheckpointReadResult Read(CheckpointId checkpointId)
        {
            if (Active != null && Active.Id == checkpointId) return CheckpointReadResult.Success(Clone(Active));
            if (Previous != null && Previous.Id == checkpointId) return CheckpointReadResult.Success(Clone(Previous));
            return CheckpointReadResult.Rejected(CommandOutcome.NoCheckpoint, "The isolated scene checkpoint was not found.");
        }

        public CommandResult<CheckpointId> Promote(Checkpoint checkpoint)
        {
            PromotionCount++;
            if (FailNextPromotion)
            {
                FailNextPromotion = false;
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.PersistenceFailed, "The isolated scene store rejected the promotion.");
            }

            if (Active != null && Active.Id == checkpoint.Id && Active.Generation == checkpoint.Generation)
            {
                return CommandResult<CheckpointId>.Success(checkpoint.Id);
            }

            if (Active != null && checkpoint.Generation <= Active.Generation)
            {
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.InvalidCheckpoint, "The isolated scene generation did not advance.");
            }

            Previous = Active == null ? null : Clone(Active);
            Active = Clone(checkpoint);
            return CommandResult<CheckpointId>.Success(checkpoint.Id);
        }

        public CommandResult<CheckpointId> RecoverPrevious(Checkpoint recoveryCandidate)
        {
            if (Previous == null)
            {
                return CommandResult<CheckpointId>.Rejected(CommandOutcome.NoCheckpoint, "No isolated previous generation is available.");
            }

            if (Active != null && Active.Id == recoveryCandidate.Id && Active.Generation == recoveryCandidate.Generation)
            {
                return CommandResult<CheckpointId>.Success(recoveryCandidate.Id);
            }

            var oldActive = Active;
            Active = Clone(recoveryCandidate);
            Previous = oldActive == null ? null : Clone(oldActive);
            return CommandResult<CheckpointId>.Success(recoveryCandidate.Id);
        }

        private static Checkpoint Clone(Checkpoint source) => new Checkpoint(
            source.Id,
            source.Generation,
            source.DepartureUtc,
            source.OfflinePolicyRevision,
            source.ReconciledFromCheckpointId,
            source.CreateFactoryCopy());
    }
}
