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
    /// The seam T105 fills with the offline simulation. <see cref="Reconcile"/> turns the source checkpoint of a return
    /// into the candidate checkpoint that is promoted and only then published (R09, exactly once by
    /// <c>reconciledFromCheckpointId</c>). A reconciler must be a pure function of its arguments, so that a retry with
    /// the same captured return instant builds the same candidate, and it must return state that is not shared with
    /// the live session. <see cref="RunPersistence"/> never installs a candidate's own objects: it installs a fresh
    /// read of the durable generation, so a failed install cannot mutate a candidate that a retry re-promotes.
    /// </summary>
    public interface IRunReturnReconciler
    {
        RunCommandResult<RunCheckpoint> Reconcile(RunCheckpoint source, long returnUtcMs, CheckpointId resultId, long resultGeneration);
    }

    /// <summary>
    /// Optional companion of an <see cref="IRunReturnReconciler"/> (T105): the summary of the last reconcile and the last
    /// completions its simulation recorded, which <see cref="RunPersistence"/> hands to the session after the install.
    /// </summary>
    public interface IRunReturnSummarySource
    {
        /// <summary>The summary of the last successful reconcile, or null.</summary>
        RunOfflineSummary LastSummary { get; }

        /// <summary>The last completion the offline advance saw of a machine (RatesSnapshot, OD-9).</summary>
        bool TryGetLastCompletion(Guid instanceId, out long units, out long batchFactor, out int limit);
    }

    /// <summary>
    /// The offline return of the run (T105, R05, R09, FR-025): the candidate checkpoint of a return is the source state
    /// advanced by <see cref="OfflineSegmenter"/> from the PERSISTED departure <c>d</c> to the captured return instant
    /// <c>r</c>, under a new identity with <c>reconciledFromCheckpointId</c> = the source (the exactly-once key, so
    /// it is never applied to one absence twice) and the departure moved to <c>r</c>.
    ///
    /// Rules: (1) the credit is <c>max(0, r - d)</c>: a departure in the future (a forged or stale value) credits
    /// nothing, leaves the state as it was, and the departure is never rewritten earlier; (2) a simulator rejection
    /// (<c>value.limit</c>) forfeits the credit, the state stays as before the absence and the departure advances to
    /// <c>r</c>, so a retry or restart never attempts the credit again (owner decision 2026-10-01; the summary
    /// carries <see cref="RunOfflineSummary.ValueLimitCode"/>); (3) the reconcile mutates the source state, so
    /// <see cref="RunPersistence"/> hands it a private fresh read of the source on every call; with the same source
    /// bytes and the same <c>r</c> the candidate bytes are identical, which the store's idempotent promotion relies on;
    /// (4) the segmenter's simulator rescales every machine to its effective cycle before it produces anything
    /// (load rule 2), and a return that credits nothing is rescaled by the settle of the install.
    /// </summary>
    public sealed class OfflineRunReturnReconciler : IRunReturnReconciler, IRunReturnSummarySource
    {
        private readonly RunCatalog catalog;
        private readonly OfflineSegmenter segmenter;

        public OfflineRunReturnReconciler(RunCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            segmenter = new OfflineSegmenter(catalog);
        }

        public RunOfflineSummary LastSummary { get; private set; }

        public bool TryGetLastCompletion(Guid instanceId, out long units, out long batchFactor, out int limit) =>
            segmenter.Simulator.TryGetLastCompletion(instanceId, out units, out batchFactor, out limit);

        public RunCommandResult<RunCheckpoint> Reconcile(RunCheckpoint source, long returnUtcMs, CheckpointId resultId, long resultGeneration)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            LastSummary = null;
            if (!ReferenceEquals(source.Run.Catalog, catalog)) return RunCommandResult.Reject<RunCheckpoint>(RunPersistenceReasons.InvalidCheckpoint);

            RunOfflineSummary summary = segmenter.Reconcile(source.Run, source.Permanent, source.DepartureUtcMs, returnUtcMs);
            long departure;
            switch (summary.Outcome)
            {
                case RunOfflineOutcome.Applied:
                    departure = returnUtcMs;
                    break;
                case RunOfflineOutcome.BackwardClock:
                    // Zero credit, nothing processed; the departure is never rewritten earlier.
                    departure = source.DepartureUtcMs;
                    break;
                case RunOfflineOutcome.SimulationRejected when summary.CreditForfeited:
                    departure = returnUtcMs > source.DepartureUtcMs ? returnUtcMs : source.DepartureUtcMs;
                    break;
                case RunOfflineOutcome.SimulationRejected:
                    return RunCommandResult.Reject<RunCheckpoint>(RunPersistenceReasons.InvalidCheckpoint);
                default:
                    return RunCommandResult.Reject<RunCheckpoint>(RunPersistenceReasons.PersistenceFailed);
            }

            try
            {
                var candidate = new RunCheckpoint(resultId, resultGeneration, departure, source.OfflinePolicyRevision, source.Id,
                    source.ContentRevision, null, source.Run, source.Permanent);
                LastSummary = summary;
                return RunCommandResult.Success(candidate);
            }
            catch (ArgumentException)
            {
                return RunCommandResult.Reject<RunCheckpoint>(RunPersistenceReasons.InvalidCheckpoint);
            }
        }
    }

    /// <summary>
    /// A reconciler that credits no offline time (T138): the candidate is the source state under a new identity,
    /// <c>reconciledFromCheckpointId</c> = the source, and the departure moved forward to the return instant (never
    /// back). T105 replaced it as the default by <see cref="OfflineRunReturnReconciler"/>; it stays for tests and
    /// callers that explicitly want no credit.
    /// </summary>
    public sealed class NoCreditRunReturnReconciler : IRunReturnReconciler
    {
        public RunCommandResult<RunCheckpoint> Reconcile(RunCheckpoint source, long returnUtcMs, CheckpointId resultId, long resultGeneration)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            try
            {
                long departure = returnUtcMs > source.DepartureUtcMs ? returnUtcMs : source.DepartureUtcMs;
                return RunCommandResult.Success(new RunCheckpoint(resultId, resultGeneration, departure, source.OfflinePolicyRevision,
                    source.Id, source.ContentRevision, null, source.Run, source.Permanent));
            }
            catch (ArgumentException)
            {
                return RunCommandResult.Reject<RunCheckpoint>(RunPersistenceReasons.InvalidCheckpoint);
            }
        }
    }
}
