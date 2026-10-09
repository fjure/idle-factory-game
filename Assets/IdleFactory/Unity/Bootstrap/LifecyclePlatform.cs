using System;
using UnityEngine;

namespace IdleFactory.Unity.Bootstrap
{
    /// <summary>
    /// The runtime platform class that decides what losing application focus means (T250, owner decision 2026-10-09). On a
    /// phone or tablet, focus loss and pause are a departure (Feature 001 FR-037): the checkpoint is written, the simulation
    /// stops and the return reconciles the offline interval. On a desktop player (Windows, macOS, Linux) and in the editor's
    /// Play Mode, losing window focus (Alt-Tab, another window, minimize) is not a departure: the run keeps simulating, and
    /// offline credit applies only after a regular shutdown or an unclean exit and the next start.
    /// </summary>
    /// <remarks>
    /// The class is decided by the runtime platform (<see cref="Application.isMobilePlatform"/>), never by the input-device
    /// profile (<c>ShellDeviceProfile</c>): a touch-screen laptop is a desktop, and a tablet with a keyboard is not. Tests set
    /// the class through <see cref="Override"/>; production code never does.
    /// </remarks>
    public static class LifecyclePlatform
    {
        private static bool? overrideIsMobile;

        /// <summary>True when focus loss and pause are a departure (phones and tablets).</summary>
        public static bool IsMobile => overrideIsMobile ?? Application.isMobilePlatform;

        /// <summary>True when focus loss and pause are not a departure (desktop players and editor Play Mode).</summary>
        public static bool IsDesktop => !IsMobile;

        /// <summary>
        /// Forces the platform class until the returned scope is disposed, which restores the previous override. Intended for
        /// deterministic tests of both classes on one machine.
        /// </summary>
        public static IDisposable Override(bool isMobile)
        {
            var scope = new OverrideScope(overrideIsMobile);
            overrideIsMobile = isMobile;
            return scope;
        }

        private sealed class OverrideScope : IDisposable
        {
            private readonly bool? previous;
            private bool disposed;

            public OverrideScope(bool? previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                overrideIsMobile = previous;
            }
        }
    }
}
