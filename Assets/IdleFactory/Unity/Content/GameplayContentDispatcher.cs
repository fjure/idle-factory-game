using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using IdleFactory.Domain;
using IdleFactory.Run.Content;

namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// Outcome of <see cref="GameplayContentDispatcher.Load"/>. Exactly one of <see cref="LegacyCatalog"/> (version 1) and
    /// <see cref="RunCatalog"/> (version 2) is set on success; neither is set when diagnostics are returned.
    /// </summary>
    public sealed class GameplayContentLoadResult
    {
        private static readonly IReadOnlyList<ContentDiagnostic> NoDiagnostics =
            new ReadOnlyCollection<ContentDiagnostic>(new List<ContentDiagnostic>());

        private GameplayContentLoadResult(
            int schemaVersion,
            DefinitionCatalog legacyCatalog,
            RunCatalog runCatalog,
            IReadOnlyList<ContentDiagnostic> diagnostics)
        {
            SchemaVersion = schemaVersion;
            LegacyCatalog = legacyCatalog;
            RunCatalog = runCatalog;
            Diagnostics = diagnostics;
        }

        public bool IsSuccess => LegacyCatalog != null || RunCatalog != null;

        /// <summary>1 or 2 when the version peek succeeded, otherwise 0.</summary>
        public int SchemaVersion { get; }

        /// <summary>The Feature 002 catalog of a version-1 document, which only serves the Save V1 import.</summary>
        public DefinitionCatalog LegacyCatalog { get; }

        /// <summary>The catalog of a version-2 document.</summary>
        public RunCatalog RunCatalog { get; }

        public IReadOnlyList<ContentDiagnostic> Diagnostics { get; }

        public static GameplayContentLoadResult FromLegacy(ContentLoadResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            return result.IsSuccess
                ? new GameplayContentLoadResult(1, result.Catalog, null, NoDiagnostics)
                : new GameplayContentLoadResult(1, null, null, result.Diagnostics);
        }

        public static GameplayContentLoadResult FromRun(RunCatalogLoadResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            return result.IsSuccess
                ? new GameplayContentLoadResult(2, null, result.Catalog, NoDiagnostics)
                : new GameplayContentLoadResult(2, null, null, result.Diagnostics);
        }

        public static GameplayContentLoadResult Rejected(ContentDiagnostic diagnostic)
        {
            if (diagnostic == null)
            {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            return new GameplayContentLoadResult(0, null, null, new ReadOnlyCollection<ContentDiagnostic>(new[] { diagnostic }));
        }
    }

    /// <summary>
    /// Single entry point for a gameplay content document (contract v2, "Carried over from v1 unchanged"). It peeks
    /// <c>contentSchemaVersion</c> with <see cref="RunContentReader.PeekSchemaVersion"/>, which inherits the v2 reader's
    /// lexical rules (strict UTF-8 without a BOM, at most 1 MiB, an object root, no duplicate member, an integer value,
    /// the shared depth limit) and so reports the same static diagnostics, never file paths or document text. Then:
    /// <list type="bullet">
    /// <item><description><c>1</c>: the unchanged Feature 002 <see cref="GameplayContentLoader"/>; the result is the legacy import catalog.</description></item>
    /// <item><description><c>2</c>: <see cref="RunContentReader"/>, <see cref="RunContentValidator"/>, then <see cref="RunCatalogBuilder"/>.</description></item>
    /// <item><description>anything else: <c>content.version.unsupported</c>.</description></item>
    /// </list>
    /// </summary>
    public static class GameplayContentDispatcher
    {
        public static GameplayContentLoadResult Load(byte[] bytes)
        {
            return Load(bytes, null);
        }

        public static GameplayContentLoadResult Load(byte[] bytes, IPresentationKeyResolver presentationKeys)
        {
            var peek = RunContentReader.PeekSchemaVersion(bytes);
            if (!peek.IsSuccess)
            {
                return GameplayContentLoadResult.Rejected(peek.Diagnostic);
            }

            if (peek.Version == 1)
            {
                return GameplayContentLoadResult.FromLegacy(GameplayContentLoader.Load(bytes));
            }

            var read = RunContentReader.Read(bytes);
            if (!read.IsSuccess)
            {
                return GameplayContentLoadResult.Rejected(read.Diagnostic);
            }

            return GameplayContentLoadResult.FromRun(RunCatalogBuilder.Build(read.Document, presentationKeys));
        }
    }
}
