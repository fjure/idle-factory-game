using System;
using System.Globalization;
using UnityEngine;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>
    /// A privacy-safe description of why a checkpoint operation was rejected. It carries only fixed
    /// operation/stage identifiers, an exception type name and a source member/line; never save bytes,
    /// identifiers, file paths or exception messages.
    /// </summary>
    public sealed class CheckpointDiagnostic
    {
        public CheckpointDiagnostic(string operation, string stage, string exceptionType, string origin)
        {
            Operation = operation ?? string.Empty;
            Stage = stage ?? string.Empty;
            ExceptionType = exceptionType ?? string.Empty;
            Origin = origin ?? string.Empty;
        }

        public string Operation { get; }
        public string Stage { get; }
        public string ExceptionType { get; }
        public string Origin { get; }

        public override string ToString()
        {
            var text = "[IdleFactory.Persistence] " + Operation + " rejected at " + Stage;
            if (ExceptionType.Length > 0) text += " (" + ExceptionType + ")";
            if (Origin.Length > 0) text += " origin " + Origin;
            return text;
        }
    }

    /// <summary>
    /// Reports checkpoint rejections that are otherwise collapsed into generic outcomes. Player-facing
    /// messages stay generic; this channel exists so player-build failures are diagnosable from logs.
    /// </summary>
    public static class CheckpointDiagnostics
    {
        private static readonly object Gate = new object();
        private static Action<CheckpointDiagnostic> listeners;

        /// <summary>When true (default), each diagnostic is also written to the Unity log as a warning.</summary>
        public static bool LogToUnity { get; set; } = true;

        public static event Action<CheckpointDiagnostic> Reported
        {
            add { lock (Gate) listeners += value; }
            remove { lock (Gate) listeners -= value; }
        }

        internal static void Report(string operation, string stage, Exception exception = null)
        {
            string exceptionType = null;
            string origin = null;
            if (exception != null)
            {
                exceptionType = exception.GetType().Name;
                if (exception is ICheckpointDiagnosticOrigin located)
                {
                    origin = located.Member + ":" + located.Line.ToString(CultureInfo.InvariantCulture);
                }
            }

            Publish(new CheckpointDiagnostic(operation, stage, exceptionType, origin));
        }

        private static void Publish(CheckpointDiagnostic diagnostic)
        {
            Action<CheckpointDiagnostic> snapshot;
            lock (Gate) snapshot = listeners;
            try
            {
                snapshot?.Invoke(diagnostic);
            }
            catch (Exception)
            {
                // Diagnostics must never change persistence outcomes.
            }

            if (LogToUnity)
            {
                Debug.LogWarning(diagnostic.ToString());
            }
        }
    }

    internal interface ICheckpointDiagnosticOrigin
    {
        string Member { get; }
        int Line { get; }
    }
}
