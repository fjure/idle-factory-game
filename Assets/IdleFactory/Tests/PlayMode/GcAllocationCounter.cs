using System;
using UnityEngine.Profiling;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// Counts managed GC allocations made on the current thread by one action, using Unity's <c>GC.Alloc</c>
    /// profiler recorder (the mechanism behind the Test Framework's <c>AllocatingGCMemory</c> constraint).
    /// <see cref="GC.GetAllocatedBytesForCurrentThread"/> returns 0 under the Unity Editor's Mono runtime even
    /// for <c>new object[1024]</c>, so it cannot prove an allocation-free path (T063).
    /// </summary>
    internal static class GcAllocationCounter
    {
        public static int Count(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            var recorder = Recorder.Get("GC.Alloc");
            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            recorder.enabled = true;
            try
            {
                action();
            }
            finally
            {
                recorder.enabled = false;
                recorder.CollectFromAllThreads();
            }

            return recorder.sampleBlockCount;
        }
    }
}
