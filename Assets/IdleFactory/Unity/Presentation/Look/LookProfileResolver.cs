using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleFactory.Unity.Presentation.Look
{
    /// <summary>
    /// The one look-profile resolver of a composition (contracts/look-profile.md rules 3 and 5, ARCH-5, ARCH-6, ARCH-11). The bootstrap creates it once and
    /// passes what it resolves to the world views, the camera settings and the effect pool; nothing here is static, so a second composition has its own.
    /// It picks the active profile (the shipped default, or the profile an <see cref="ILookProfileSource"/> selects in a development build), validates the
    /// profile's catalog against the profile's own requirement table, and fails closed: a missing key or a wrong size is an error, never a fallback to
    /// another profile. The one fallback is the logged one of ARCH-11: an unknown selected id yields the shipped default. A release build ignores the
    /// source altogether. It reads no simulation state and has no write path into it. Resolution allocates nothing once the profile is active.
    /// </summary>
    public sealed class LookProfileResolver : IDisposable
    {
        public const string UnknownSelectionCode = "lookprofile.selection.unknown";
        public const string InvalidProfileCode = "lookprofile.profile.invalid";
        public const string ShippedPrototypeCode = "lookprofile.shipped.prototype";
        public const string CatalogMissingCode = "lookprofile.catalog.missing";
        public const int MaxLoggedIdLength = 32;

        private readonly LookProfile shippedDefault;
        private readonly PresentationSpriteCatalog shippedSceneCatalog;
        private readonly ILookProfileSource source;
        private readonly bool honorSource;
        private readonly Action<string> log;
        private readonly HashSet<string> reportedUnknown = new HashSet<string>(StringComparer.Ordinal);
        private LookProfile active;
        private PresentationSpriteCatalog activeCatalog;
        private Func<PresentationSpriteCatalog, string> contentKeyCheck;

        /// <summary>
        /// Whether this build honours a development profile source: true only with <c>IDLEFACTORY_DEV</c>. A release build never reads the selection key
        /// (ARCH-11). Tests pass the flag to the constructor instead.
        /// </summary>
        public static bool DevelopmentBuild
        {
            get
            {
#if IDLEFACTORY_DEV
                return true;
#else
                return false;
#endif
            }
        }

        /// <param name="shippedDefault">The shipped default profile, named in the shipped presentation settings. It is never a prototype.</param>
        /// <param name="shippedSceneCatalog">The catalog the scene serializes; the sprites of a profile that sets <see cref="LookProfile.UsesShippedSceneCatalog"/>.</param>
        /// <param name="source">The development source, or null (always null in a release build).</param>
        /// <param name="honorSource">False in a release build: the source and its selection are ignored.</param>
        /// <param name="logSink">Receives each log entry; defaults to a Unity warning.</param>
        public LookProfileResolver(
            LookProfile shippedDefault,
            PresentationSpriteCatalog shippedSceneCatalog,
            ILookProfileSource source,
            bool honorSource,
            Action<string> logSink = null)
        {
            this.shippedDefault = shippedDefault ?? throw new ArgumentNullException(nameof(shippedDefault));
            this.shippedSceneCatalog = shippedSceneCatalog;
            this.honorSource = honorSource;
            this.source = honorSource ? source : null;
            log = logSink ?? DefaultLog;
            if (this.source != null)
            {
                this.source.Changed += HandleSourceChanged;
            }
        }

        /// <summary>The shipped default profile.</summary>
        public LookProfile ShippedDefault => shippedDefault;

        /// <summary>The active profile, or null before <see cref="Resolve"/> succeeded.</summary>
        public LookProfile Active => active;

        /// <summary>The catalog of the active profile, or null before <see cref="Resolve"/> succeeded.</summary>
        public PresentationSpriteCatalog ActiveCatalog => activeCatalog;

        /// <summary>True once a profile is active.</summary>
        public bool IsValid => active != null;

        /// <summary>The last failure (a coded message), or empty. A failed switch keeps the previous profile and sets this.</summary>
        public string Error { get; private set; } = "The look profile has not been resolved.";

        /// <summary>The art pixels per world unit of the active profile (the shipped default's before resolution). The camera model reads it.</summary>
        public int ArtPixelsPerCell => (active != null ? active : shippedDefault).PixelsPerUnit;

        /// <summary>Raised after a later switch made another profile active. The bootstrap answers with the existing world rebuild path.</summary>
        public event Action<LookProfile> Applied;

        /// <summary>
        /// Start-up resolution: picks the profile, validates its catalog against its own requirement table and makes it active. Fails closed: false with
        /// <see cref="Error"/> set and no active profile. In a development build with a ready source, the source's selection is used.
        /// </summary>
        public bool Resolve()
        {
            var chosen = Choose();
            var problem = Validate(chosen, out var catalog);
            if (problem != null)
            {
                active = null;
                activeCatalog = null;
                Error = problem;
                return false;
            }

            active = chosen;
            activeCatalog = catalog;
            Error = string.Empty;
            return true;
        }

        /// <summary>
        /// Re-reads the source after a change. A profile that fails validation is reported and the current profile stays; this never falls back to the
        /// shipped default by itself. Raises <see cref="Applied"/> when another profile became active. Returns true when the active profile changed.
        /// </summary>
        public bool Refresh()
        {
            if (active == null)
            {
                return Resolve();
            }

            var chosen = Choose();
            if (ReferenceEquals(chosen, active))
            {
                return false;
            }

            var problem = Validate(chosen, out var catalog);
            if (problem != null)
            {
                Error = problem;
                log(problem);
                return false;
            }

            active = chosen;
            activeCatalog = catalog;
            Error = string.Empty;
            Applied?.Invoke(active);
            return true;
        }

        /// <summary>
        /// Supplies the content-key check of a later switch (the bootstrap does it once the gameplay content has loaded): a function that returns null when
        /// every presentation key of the content resolves in the candidate catalog, else a coded problem. A profile that fails it is rejected like at start-up.
        /// </summary>
        public void SetContentKeyCheck(Func<PresentationSpriteCatalog, string> check) => contentKeyCheck = check;

        /// <summary>Frame 0 of a key of the active profile; false for a key it has not registered. Allocation-free.</summary>
        public bool TryGetSprite(string key, out Sprite sprite)
        {
            if (activeCatalog == null)
            {
                sprite = null;
                return false;
            }

            return activeCatalog.TryGet(key, out sprite);
        }

        /// <summary>
        /// The zoom settings with the active profile's pixels per unit. It returns <paramref name="authored"/> itself when they agree, which is always the
        /// case for F006, so the shipped camera inputs are untouched.
        /// </summary>
        public RunZoomSettings ZoomFor(RunZoomSettings authored)
        {
            if (authored == null)
            {
                throw new ArgumentNullException(nameof(authored));
            }

            var pixelsPerUnit = ArtPixelsPerCell;
            if (authored.artPixelsPerCell == pixelsPerUnit)
            {
                return authored;
            }

            return new RunZoomSettings
            {
                artPixelsPerCell = pixelsPerUnit,
                readableCellPoints = authored.readableCellPoints,
                maxScale = authored.maxScale,
            };
        }

        public void Dispose()
        {
            if (source != null)
            {
                source.Changed -= HandleSourceChanged;
            }
        }

        private void HandleSourceChanged()
        {
            // Before the bootstrap resolved, the first Resolve reads the source itself.
            if (active != null)
            {
                Refresh();
            }
        }

        private LookProfile Choose()
        {
            if (!honorSource || source == null || !source.IsReady)
            {
                return shippedDefault;
            }

            var id = source.SelectedProfileId;
            if (string.IsNullOrEmpty(id) || string.Equals(id, shippedDefault.Id, StringComparison.Ordinal))
            {
                return shippedDefault;
            }

            if (source.TryGetProfile(id, out var profile) && profile != null && string.Equals(profile.Id, id, StringComparison.Ordinal))
            {
                return profile;
            }

            if (reportedUnknown.Add(id))
            {
                log(UnknownSelectionCode + ": " + Printable(id));
            }

            return shippedDefault;
        }

        // Returns the coded problem of a profile, or null when it validates; on success the catalog the profile draws from is validated against the
        // profile's own table.
        private string Validate(LookProfile profile, out PresentationSpriteCatalog catalog)
        {
            catalog = null;
            if (ReferenceEquals(profile, shippedDefault) && profile.IsPrototype)
            {
                return ShippedPrototypeCode + ": " + Printable(profile.Id);
            }

            var problem = profile.FirstProblem();
            if (problem != null)
            {
                return InvalidProfileCode + ": " + Printable(profile.Id) + ": " + problem;
            }

            catalog = profile.UsesShippedSceneCatalog ? shippedSceneCatalog : profile.SpriteCatalog;
            if (catalog == null)
            {
                return CatalogMissingCode + ": " + Printable(profile.Id);
            }

            // The catalog enforces the required rows of an authored table itself; resolution writes no setting of a profile's serialized catalog.
            catalog.UseRequirementTable(profile.RequirementTable);
            if (!catalog.Validate())
            {
                var error = catalog.Error;
                catalog = null;
                return error;
            }

            // N2: a switch is held to the same content-key rule as start-up. The bootstrap supplies the check once the content has loaded.
            var contentProblem = contentKeyCheck?.Invoke(catalog);
            if (contentProblem != null)
            {
                catalog = null;
                return contentProblem;
            }

            return null;
        }

        // A persisted id is untrusted text: bounded and reduced to printable ASCII before it reaches a log.
        private static string Printable(string text)
        {
            if (text == null)
            {
                return string.Empty;
            }

            var length = Math.Min(text.Length, MaxLoggedIdLength);
            var chars = new char[length];
            for (var i = 0; i < length; i++)
            {
                var c = text[i];
                chars[i] = c >= ' ' && c <= '~' ? c : '?';
            }

            return new string(chars);
        }

        private static void DefaultLog(string line) =>
            Debug.LogFormat(LogType.Warning, LogOption.NoStacktrace, null, "{0}", line);
    }
}
