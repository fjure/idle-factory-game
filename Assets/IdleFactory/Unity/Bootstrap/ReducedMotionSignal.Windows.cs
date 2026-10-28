#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && !(UNITY_ANDROID && !UNITY_EDITOR)
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace IdleFactory.Unity.Bootstrap
{
    public sealed partial class ReducedMotionSignal
    {
        private const uint SpiGetClientAreaAnimation = 0x1042;
        private static bool failureLogged;

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SystemParametersInfoW")]
        private static extern bool SystemParametersInfo(uint action, uint parameter, ref int value, uint winIni);

        /// <summary>
        /// Windows: the "Show animations in Windows" setting (<c>SPI_GETCLIENTAREAANIMATION</c>); off means reduce motion. A
        /// failed call is "not reduced" and logs only the Win32 error code, once.
        /// </summary>
        private static bool ProbePlatform()
        {
            try
            {
                var enabled = 1;
                if (!SystemParametersInfo(SpiGetClientAreaAnimation, 0, ref enabled, 0))
                {
                    LogFailureOnce(Marshal.GetLastWin32Error());
                    return false;
                }

                return enabled == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void LogFailureOnce(int errorCode)
        {
            if (failureLogged)
            {
                return;
            }

            failureLogged = true;
            Debug.LogWarning("[IdleFactory.Preferences] reducedMotion.signal.windows win32=" + errorCode);
        }
    }
}
#endif
