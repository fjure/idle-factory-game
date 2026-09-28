using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using IdleFactory.Domain;

namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// A complete catalog, or diagnostics for a rejected document. Partial catalogs
    /// are never exposed to callers.
    /// </summary>
    public sealed class ContentLoadResult
    {
        private static readonly IReadOnlyList<ContentDiagnostic> NoDiagnostics =
            new ReadOnlyCollection<ContentDiagnostic>(new List<ContentDiagnostic>());

        private ContentLoadResult(DefinitionCatalog catalog, IReadOnlyList<ContentDiagnostic> diagnostics)
        {
            Catalog = catalog;
            Diagnostics = diagnostics;
        }

        public bool IsSuccess => Catalog != null;

        public DefinitionCatalog Catalog { get; }

        public IReadOnlyList<ContentDiagnostic> Diagnostics { get; }

        public static ContentLoadResult Loaded(DefinitionCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            return new ContentLoadResult(catalog, NoDiagnostics);
        }

        public static ContentLoadResult Rejected(IEnumerable<ContentDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            var copy = new List<ContentDiagnostic>();
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic == null)
                {
                    throw new ArgumentException("Diagnostics must not contain null entries.", nameof(diagnostics));
                }

                copy.Add(diagnostic);
            }

            if (copy.Count == 0)
            {
                throw new ArgumentException("A rejected content result must include at least one diagnostic.", nameof(diagnostics));
            }

            return new ContentLoadResult(null, new ReadOnlyCollection<ContentDiagnostic>(copy));
        }

        public static ContentLoadResult Rejected(ContentDiagnostic diagnostic)
        {
            if (diagnostic == null)
            {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            return Rejected(new[] { diagnostic });
        }
    }
}
