using System;
using System.Collections.Generic;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Unity.Bootstrap;
using UnityEngine;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>
    /// The composition of the session log for the run shell (T338): the writer (the sink every <see cref="RunSession"/> is constructed with, so the
    /// recording toggle lives in the sink, A1), the sampler, the export and the per-frame driver. The bootstrap creates it before the session
    /// and attaches it once the session is live. It holds no gameplay state.
    /// </summary>
    public sealed class SessionLogRuntime : ISessionLogControls
    {
        public SessionLogRuntime(SessionLogWriter writer, SessionLogExport export)
        {
            Writer = writer ?? throw new ArgumentNullException(nameof(writer));
            Export = export ?? throw new ArgumentNullException(nameof(export));
            Sampler = new SessionLogSampler(writer);
        }

        public SessionLogWriter Writer { get; }

        public SessionLogExport Export { get; }

        public SessionLogSampler Sampler { get; }

        /// <inheritdoc/>
        public bool Enabled => Writer.Enabled;

        /// <inheritdoc/>
        public void SetEnabled(bool enabled) => Writer.SetEnabled(enabled);

        /// <inheritdoc/>
        public bool Clear() => Writer.Clear();

        /// <inheritdoc/>
        public SessionLogExportResult LastExport { get; private set; }

        /// <inheritdoc/>
        SessionLogExportResult ISessionLogControls.Export()
        {
            var result = Export.Export();
            if (result.IsExported) LastExport = result;
            return result;
        }

        /// <summary>The sink to pass to the <see cref="RunSession"/> constructor; never null.</summary>
        public IRunEventSink Sink => Writer;

        /// <summary>The product composition: the real file layer, <c>PlayerPrefs</c> and the catalog's IDs, under <paramref name="root"/> (<c>PersistenceRoots.Resolve</c>).</summary>
        public static SessionLogRuntime Create(string root, RunCatalog catalog, Func<ITimeScaleSource> timeScale)
        {
            var files = new SessionLogFileSystem();
            var writer = new SessionLogWriter(new SessionLogOptions
            {
                Root = root,
                Files = files,
                ContentIds = new SessionLogCatalogIds(catalog),
                TimeScale = timeScale,
                ContentRevision = catalog.ContentRevision,
                BuildKind = CurrentBuildKind(),
                PlatformClass = LifecyclePlatform.IsDesktop ? RunPlatformClass.Desktop : RunPlatformClass.Mobile,
            });
            return new SessionLogRuntime(writer, new SessionLogExport(writer, files));
        }

        /// <summary>The Editor, a development build or <c>IDLEFACTORY_DEV</c> are told apart from a release build; the header records which.</summary>
        public static RunBuildKind CurrentBuildKind()
        {
#if UNITY_EDITOR
            return RunBuildKind.Editor;
#else
            return PersistenceRoots.IsDevelopmentRoot || Debug.isDebugBuild ? RunBuildKind.Development : RunBuildKind.Release;
#endif
        }

        /// <summary>
        /// Starts the session (Header and start record) and creates the driver under <paramref name="parent"/>, which then ticks the writer, polls the
        /// sampler and records the departure and quit lifecycle points. Never throws.
        /// </summary>
        public void Attach(RunSession session, Transform parent)
        {
            try
            {
                Writer.Start();
                var driverObject = new GameObject("SessionLogDriver");
                if (parent != null) driverObject.transform.SetParent(parent, false);
                driverObject.AddComponent<SessionLogDriver>().Configure(this, session);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
            }
        }
    }
}
