using System;
using IdleFactory.Domain;

namespace IdleFactory.Core.Application
{
    /// <summary>Creates a fresh running session for development-only bootstrap scenarios.</summary>
    public static class DevelopmentSessionFactory
    {
        public static CommandResult<FactorySession> CreateRunning(
            FactoryState initialState,
            DefinitionCatalog catalog,
            IUtcClock utcClock,
            ICheckpointStore checkpointStore)
        {
            if (initialState == null || catalog == null || utcClock == null || checkpointStore == null)
            {
                return CommandResult<FactorySession>.Rejected(
                    CommandOutcome.InvalidRequest,
                    "Development session dependencies are required.");
            }

            try
            {
                var session = new FactorySession(initialState, catalog, utcClock, checkpointStore);
                var loading = session.TransitionTo(LifecycleState.Loading);
                if (!loading.Succeeded)
                {
                    return CommandResult<FactorySession>.Rejected(loading.Outcome, loading.Message);
                }

                var running = session.TransitionTo(LifecycleState.Running);
                return running.Succeeded
                    ? CommandResult<FactorySession>.Success(session)
                    : CommandResult<FactorySession>.Rejected(running.Outcome, running.Message);
            }
            catch (ArgumentException exception)
            {
                return CommandResult<FactorySession>.Rejected(CommandOutcome.InvalidRequest, exception.Message);
            }
        }
    }
}
