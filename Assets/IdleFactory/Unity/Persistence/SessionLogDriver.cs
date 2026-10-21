using IdleFactory.Run.Application;
using UnityEngine;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>
    /// The per-frame and lifecycle hook of the session log, created at run time by <see cref="SessionLogRuntime.Attach"/> (never serialized into a
    /// scene). It does nothing that can throw: the writer, the sampler and the lifecycle calls are all isolated. It reads no save, changes no
    /// simulation state and takes no part in checkpointing.
    /// </summary>
    public sealed class SessionLogDriver : MonoBehaviour
    {
        private SessionLogRuntime runtime;
        private RunSession session;
        private bool departureLogged;

        internal void Configure(SessionLogRuntime owner, RunSession liveSession)
        {
            runtime = owner;
            session = liveSession;
        }

        private void Update()
        {
            if (runtime == null) return;
            runtime.Writer.Tick();
            runtime.Sampler.Poll(session);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) departureLogged = false;
            else LogDeparture(RunLifecycleKind.FocusLossCheckpoint);
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) departureLogged = false;
            else LogDeparture(RunLifecycleKind.FocusLossCheckpoint);
        }

        private void OnApplicationQuit()
        {
            if (runtime == null) return;
            session?.RaiseBottlenecks(); // the open window, before the lifecycle record flushes
            runtime.Writer.RecordLifecycle(RunLifecycleKind.Quit, session == null ? 0 : session.DroppedEvents);
        }

        private void LogDeparture(RunLifecycleKind kind)
        {
            if (runtime == null || departureLogged) return;
            departureLogged = true;
            session?.RaiseBottlenecks();
            runtime.Writer.RecordLifecycle(kind, session == null ? 0 : session.DroppedEvents);
        }
    }
}
