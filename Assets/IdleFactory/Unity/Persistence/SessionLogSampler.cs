using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>
    /// Raises the session events that need data from the run (Sample, OfflineReturn and its contract outcomes) through <c>session.Events</c>, the guarded
    /// sink, so a failure is counted and never reaches the caller. Each method is safe to call from any lifecycle code and never throws.
    /// </summary>
    public static class SessionLogRaiser
    {
        /// <summary>One Sample record: Factory Value, Cash, machines by type and level, and the expansion levels (data-model "Sample").</summary>
        public static void RaiseSample(RunSession session, long playSeconds)
        {
            if (session == null) return;
            try
            {
                RunState state = session.State;
                long factoryValue = FactoryValuation.TryCompute(session.Catalog, state, session.Permanent, out FactoryValueBreakdown breakdown) ? breakdown.Total : 0;

                var counts = new List<RunMachineCount>();
                IReadOnlyList<MachineInstance> machines = state.Machines;
                for (int i = 0; i < machines.Count; i++)
                {
                    MachineInstance machine = machines[i];
                    int at = -1;
                    for (int j = 0; j < counts.Count && at < 0; j++)
                        if (counts[j].Level == machine.Level && string.Equals(counts[j].MachineId, machine.MachineId, StringComparison.Ordinal)) at = j;
                    if (at < 0) counts.Add(new RunMachineCount(machine.MachineId, machine.Level, 1));
                    else counts[at] = new RunMachineCount(machine.MachineId, machine.Level, counts[at].Count + 1);
                }

                session.Events.OnSample(new RunSampleEvent(playSeconds, factoryValue, state.Cash, counts, state.StorageLevel, state.FactoryLevel));
                session.RaiseBottlenecks(); // T364: the machine-seconds of this sample window
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
            }
        }

        /// <summary>
        /// The OfflineReturn record of a reconcile, and the contract outcome the absence produced (W5, #392): the offline segmenter advances the
        /// simulator directly, so auto-delivery, production completion and expiry never pass the session's before/after comparison; the offline
        /// adapter raises them here from the summary. A backward clock also raises a Clock record. <c>storeFullStallSeconds</c> is 0: the summary does
        /// not measure it. The contract index is the board's completion index of the outcome, 0 when the board no longer holds it. The OfflineReturn
        /// record is raised last, because the writer flushes at it (T364, W1), so the Clock and Contract records of the same return reach the disk with it.
        /// </summary>
        public static void RaiseOfflineReturn(RunSession session, RunOfflineSummary summary, bool reclassifiedLiveGap)
        {
            if (session == null || summary == null) return;
            try
            {
                IRunEventSink events = session.Events;
                if (summary.Outcome == RunOfflineOutcome.BackwardClock && summary.ReturnUtcMs.HasValue)
                    events.OnClock(new RunClockEvent(RunClockKind.Backward, summary.ReturnUtcMs.Value - summary.DepartureUtcMs, false));

                RunOfflineContractOutcome outcome = summary.Contract;
                bool hasOutcome = true;
                RunContractEventKind kind = RunContractEventKind.Expire;
                if (outcome.Kind == RunOfflineContractKind.AutoDelivered) kind = RunContractEventKind.AutoDeliver;
                else if (outcome.Kind == RunOfflineContractKind.ProduceCompleted) kind = RunContractEventKind.ProduceComplete;
                else if (outcome.Kind != RunOfflineContractKind.Expired) hasOutcome = false;

                if (hasOutcome)
                {
                    long ratio = session.Catalog.ContractOffers.TryGetValue(outcome.OfferId, out ContractOfferDef definition) ? definition.RewardRatioPpm : 0;
                    ContractOutcome last = session.State.Contracts.LastOutcome;
                    long completion = last.Serial > 0 && string.Equals(last.OfferId, outcome.OfferId, StringComparison.Ordinal) ? last.CompletionIndex : 0;
                    int index = completion > int.MaxValue ? int.MaxValue : (int)completion;
                    events.OnContract(new RunContractEvent(kind, outcome.OfferId, index, outcome.RequiredUnits, outcome.CashReward, ratio));
                }

                long sold = 0;
                for (int i = 0; i < summary.SoldUnits.Count; i++)
                {
                    long units = summary.SoldUnits[i].Units;
                    sold = units > 0 && sold > long.MaxValue - units ? long.MaxValue : sold + units;
                }

                events.OnOfflineReturn(new RunOfflineReturnEvent(summary.MeasuredMs, summary.CreditedTicks / RunSession.TicksPerMs, summary.CapReached,
                    summary.Completions, 0, sold, summary.CashDelta, reclassifiedLiveGap));
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
            }
        }
    }

    /// <summary>
    /// Decides when the host takes a Sample (F5-R12): at timeline minutes 10, 15, 30, 60, 90 and 120 of a run, and when a milestone was recorded.
    /// Play time is the timeline since this sampler first saw the run (the timeline includes scaled and offline time), so a run resumed from a save is
    /// measured from the resume. A new run (a different run ID after a prestige) restarts the schedule. <see cref="Poll"/> costs a few reads per frame
    /// unless a sample is due.
    /// </summary>
    public sealed class SessionLogSampler
    {
        /// <summary>The timeline minutes of the scheduled samples.</summary>
        public static readonly int[] SampleMinutes = { 10, 15, 30, 60, 90, 120 };

        private readonly SessionLogWriter _writer;
        private bool _hasRun;
        private Guid _runId;
        private long _baselineTimelineMs;
        private int _next;
        private int _milestoneSeen;

        public SessionLogSampler(SessionLogWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        /// <summary>Call once per frame with the live session. Never throws.</summary>
        public void Poll(RunSession session)
        {
            if (session == null) return;
            try
            {
                Guid runId = session.State.RunId;
                long timelineMs = session.TimelineUtcMs;
                if (!_hasRun || runId != _runId)
                {
                    _hasRun = true;
                    _runId = runId;
                    _baselineTimelineMs = timelineMs;
                    _next = 0;
                    _milestoneSeen = _writer.MilestoneSerial;
                    return;
                }

                long playSeconds = (timelineMs - _baselineTimelineMs) / 1000L;
                if (playSeconds < 0) playSeconds = 0;
                bool due = false;
                while (_next < SampleMinutes.Length && playSeconds >= SampleMinutes[_next] * 60L)
                {
                    _next++;
                    due = true;
                }

                int serial = _writer.MilestoneSerial;
                if (serial != _milestoneSeen)
                {
                    _milestoneSeen = serial;
                    due = true;
                }

                if (due) SessionLogRaiser.RaiseSample(session, playSeconds);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
            }
        }
    }
}
