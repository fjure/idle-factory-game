using System;
using System.IO;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI.Panels;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// What the bootstrap does for the Settings sheet, without the scene: the store, its <c>Changed</c> subscription that
    /// re-resolves the effective state and applies it to a clock, and a replaceable OS signal.
    /// </summary>
    public sealed class SettingsTestHarness
    {
        private readonly string root;
        private int refreshes;

        public SettingsTestHarness(string persistenceRoot, bool systemReduces = false, bool systemAvailable = true)
        {
            root = persistenceRoot;
            Signal = new FixedSignal { Reduce = systemReduces };
            SystemAvailable = systemAvailable;
            Store = new DisplayPreferencesStore(persistenceRoot);
            Store.Changed += () => Refresh();
            Refresh();
        }

        public sealed class FixedSignal : IReducedMotionSignal
        {
            public bool Reduce;

            public bool ShouldReduceMotion() => Reduce;
        }

        public FixedSignal Signal { get; }
        public bool SystemAvailable { get; set; }
        public DisplayPreferencesStore Store { get; }
        public PresentationAnimationClock Clock { get; } = new PresentationAnimationClock();
        public EffectiveReducedMotion Effective { get; private set; }

        /// <summary>How many times the effective state was resolved (the OS signal is read once per resolve).</summary>
        public int Refreshes => refreshes;

        public EffectiveReducedMotion Refresh()
        {
            refreshes++;
            Effective = ReducedMotionResolver.Resolve(Store.Preference, Signal);
            Clock.SetReducedMotion(Effective.Reduced);
            return Effective;
        }

        public SettingsPanelPresenter NewPresenter(VisualTreeAsset tree = null, StyleSheet styles = null) =>
            new SettingsPanelPresenter(Store, Refresh, () => Effective, () => SystemAvailable, tree, styles);

        public static string NewRoot()
        {
            var path = Path.Combine(Path.GetTempPath(), "idlefactory-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public static void DeleteRoot(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception)
            {
                // A leftover temp directory is harmless.
            }
        }
    }
}
