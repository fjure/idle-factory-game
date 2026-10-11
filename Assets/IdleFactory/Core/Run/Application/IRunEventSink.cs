using System;

namespace IdleFactory.Run.Application
{
    /// <summary>
    /// The optional session-log port (data-model "Session log", contracts/session-log.md, research F5-R12).
    /// The core raises allowlisted, immutable event records through it; the Unity adapter (T338) writes them.
    ///
    /// Structural guarantees: every method takes a copy (a readonly struct) and returns nothing, so nothing a sink
    /// does can feed back into the simulation, the save or the timing. A <see cref="RunSession"/> never calls an
    /// implementation directly: it wraps it in a guard (<see cref="RunSession.Events"/>) that swallows every
    /// exception a sink throws, so raise sites need no null check and no try/catch.
    /// </summary>
    public interface IRunEventSink
    {
        void OnHeader(in RunHeaderEvent e);
        void OnSample(in RunSampleEvent e);
        void OnMilestone(in RunMilestoneEvent e);
        void OnSale(in RunSaleEvent e);
        void OnContract(in RunContractEvent e);
        void OnPrestige(in RunPrestigeEvent e);
        void OnOfflineReturn(in RunOfflineReturnEvent e);
        void OnBottleneck(in RunBottleneckEvent e);
        void OnPurchase(in RunPurchaseEvent e);
        void OnClock(in RunClockEvent e);
        void OnLifecycle(in RunLifecycleEvent e);
    }

    /// <summary>The default sink: does nothing. Shared, stateless and safe from any thread.</summary>
    public sealed class NoOpRunEventSink : IRunEventSink
    {
        public static readonly NoOpRunEventSink Instance = new NoOpRunEventSink();

        private NoOpRunEventSink() { }

        public void OnHeader(in RunHeaderEvent e) { }
        public void OnSample(in RunSampleEvent e) { }
        public void OnMilestone(in RunMilestoneEvent e) { }
        public void OnSale(in RunSaleEvent e) { }
        public void OnContract(in RunContractEvent e) { }
        public void OnPrestige(in RunPrestigeEvent e) { }
        public void OnOfflineReturn(in RunOfflineReturnEvent e) { }
        public void OnBottleneck(in RunBottleneckEvent e) { }
        public void OnPurchase(in RunPurchaseEvent e) { }
        public void OnClock(in RunClockEvent e) { }
        public void OnLifecycle(in RunLifecycleEvent e) { }
    }

    /// <summary>
    /// Forwards to an inner sink and swallows everything it throws, counting the events it lost
    /// (<see cref="DroppedEvents"/>, the Lifecycle record's <c>droppedEvents</c>). Exception text is never kept.
    /// </summary>
    internal sealed class GuardedRunEventSink : IRunEventSink
    {
        private readonly IRunEventSink _inner;

        public GuardedRunEventSink(IRunEventSink inner)
        {
            _inner = inner ?? NoOpRunEventSink.Instance;
        }

        public int DroppedEvents { get; private set; }

        public void OnHeader(in RunHeaderEvent e) { try { _inner.OnHeader(e); } catch (Exception ex) when (!(ex is OutOfMemoryException)) { DroppedEvents++; } }
        public void OnSample(in RunSampleEvent e) { try { _inner.OnSample(e); } catch (Exception ex) when (!(ex is OutOfMemoryException)) { DroppedEvents++; } }
        public void OnMilestone(in RunMilestoneEvent e) { try { _inner.OnMilestone(e); } catch (Exception ex) when (!(ex is OutOfMemoryException)) { DroppedEvents++; } }
        public void OnSale(in RunSaleEvent e) { try { _inner.OnSale(e); } catch (Exception ex) when (!(ex is OutOfMemoryException)) { DroppedEvents++; } }
        public void OnContract(in RunContractEvent e) { try { _inner.OnContract(e); } catch (Exception ex) when (!(ex is OutOfMemoryException)) { DroppedEvents++; } }
        public void OnPrestige(in RunPrestigeEvent e) { try { _inner.OnPrestige(e); } catch (Exception ex) when (!(ex is OutOfMemoryException)) { DroppedEvents++; } }
        public void OnOfflineReturn(in RunOfflineReturnEvent e) { try { _inner.OnOfflineReturn(e); } catch (Exception ex) when (!(ex is OutOfMemoryException)) { DroppedEvents++; } }
        public void OnBottleneck(in RunBottleneckEvent e) { try { _inner.OnBottleneck(e); } catch (Exception ex) when (!(ex is OutOfMemoryException)) { DroppedEvents++; } }
        public void OnPurchase(in RunPurchaseEvent e) { try { _inner.OnPurchase(e); } catch (Exception ex) when (!(ex is OutOfMemoryException)) { DroppedEvents++; } }
        public void OnClock(in RunClockEvent e) { try { _inner.OnClock(e); } catch (Exception ex) when (!(ex is OutOfMemoryException)) { DroppedEvents++; } }
        public void OnLifecycle(in RunLifecycleEvent e) { try { _inner.OnLifecycle(e); } catch (Exception ex) when (!(ex is OutOfMemoryException)) { DroppedEvents++; } }
    }
}
