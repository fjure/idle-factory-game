namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// Safety-guard limits for gameplay content documents (contract v1). Content is
    /// trusted build input, not untrusted player input, but authors make mistakes.
    /// See specs/002-json-content-authoring/research.md R04.
    /// </summary>
    public static class GameplayContentLimits
    {
        /// <summary>1 MiB. About 1,000x the development document.</summary>
        public const int MaximumByteCount = 1024 * 1024;

        /// <summary>Same value as Save V1. Content nests to at most depth 5.</summary>
        public const int MaximumJsonDepth = 16;

        /// <summary>Applies to resources[], recipes[], buildings[] and offlinePolicies[].</summary>
        public const int MaximumTopLevelEntryCount = 1024;

        /// <summary>
        /// Applies to recipe inputs/outputs, building buffer capacity lists and
        /// allowedOrientations. Matches the Save V1 per-buffer limit.
        /// </summary>
        public const int MaximumNestedEntryCount = 256;

        /// <summary>
        /// UTF-16 code units. FR-004 applies this to authored IDs (deferred to the
        /// semantic validator, T073/id.too-long); this reader applies the same bound
        /// structurally to non-ID label/key fields (developmentLabel, presentationKey)
        /// that have an explicit length bound in the Fields table but no dedicated
        /// identity-stage code.
        /// </summary>
        public const int MaximumIdentifierLength = 128;

        /// <summary>Diagnostics reported per run before a truncation note (semantic stage only).</summary>
        public const int MaximumReportedDiagnostics = 100;
    }
}
