using System;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    /// <summary>
    /// The one shell-owned text-scale factor (T191; FR-032, Art Bible 9.3): 100 % to 200 %, taken from the Android system font
    /// scale on Android (clamped) and 100 % elsewhere. The shell applies it as a USS class on its root
    /// (<c>run-text-scale--NNN</c>, in steps of 10 percent) that sets the font-size variables panels reuse. There is no in-game
    /// setting in Feature 003.
    /// </summary>
    public static class ShellTextScale
    {
        public const float MinimumFactor = 1f;
        public const float MaximumFactor = 2f;
        public const int StepPercent = 10;
        public const int MinimumPercent = 100;
        public const int MaximumPercent = 200;

        private const string ClassPrefix = "run-text-scale--";

        /// <summary>Clamps a factor to 1.0 to 2.0; a NaN or infinite factor is 1.0.</summary>
        public static float Clamp(float factor) =>
            float.IsNaN(factor) || float.IsInfinity(factor) ? MinimumFactor : Mathf.Clamp(factor, MinimumFactor, MaximumFactor);

        /// <summary>The clamped factor rounded to the nearest 10 percent step (100, 110, ... 200).</summary>
        public static int Percent(float factor)
        {
            var steps = Mathf.FloorToInt(Clamp(factor) * 100f / StepPercent + 0.5f);
            return Mathf.Clamp(steps * StepPercent, MinimumPercent, MaximumPercent);
        }

        /// <summary>The USS class of a text-scale step, such as <c>run-text-scale--150</c>.</summary>
        public static string ClassName(int percent) => ClassPrefix + percent;

        /// <summary>Every text-scale step percent, 100 to 200.</summary>
        public static int[] Steps()
        {
            var steps = new int[(MaximumPercent - MinimumPercent) / StepPercent + 1];
            for (var i = 0; i < steps.Length; i++)
            {
                steps[i] = MinimumPercent + i * StepPercent;
            }

            return steps;
        }

        /// <summary>The system font scale: the Android configuration's <c>fontScale</c> on a device, 1.0 elsewhere.</summary>
        public static float ProbeSystemFactor()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var resources = activity.Call<AndroidJavaObject>("getResources"))
                using (var configuration = resources.Call<AndroidJavaObject>("getConfiguration"))
                {
                    return Clamp(configuration.Get<float>("fontScale"));
                }
            }
            catch (Exception)
            {
                return MinimumFactor;
            }
#else
            return MinimumFactor;
#endif
        }
    }
}
