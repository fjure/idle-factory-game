#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace IdleFactory.Unity.Bootstrap
{
    public sealed partial class ReducedMotionSignal
    {
        /// <summary>The platform has a reduce-motion probe.</summary>
        public static bool PlatformAvailable => true;

        /// <summary>
        /// Android: the system animator duration scale (<c>Settings.Global.animator_duration_scale</c>); 0 means the user
        /// turned animations off, which reduces motion. Follows the <c>ShellTextScale</c> <c>fontScale</c> JNI pattern. The
        /// JNI types are preserved in <c>Assets/link.xml</c> against IL2CPP stripping.
        /// </summary>
        private static bool ProbePlatform()
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                using (var settings = new AndroidJavaClass("android.provider.Settings$Global"))
                {
                    var scale = settings.CallStatic<float>("getFloat", resolver, "animator_duration_scale", 1f);
                    return !float.IsNaN(scale) && scale <= 0f;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
#endif
