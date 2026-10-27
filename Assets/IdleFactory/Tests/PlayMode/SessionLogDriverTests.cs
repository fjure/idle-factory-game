using System.Collections;
using System.IO;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>T364 (m2): the session-log driver raises the open Bottleneck window at the focus-loss and quit lifecycle points, before the lifecycle record flushes.</summary>
    public sealed class SessionLogDriverTests
    {
        private sealed class MemoryPrefs : ISessionLogPrefs
        {
            public bool Enabled { get; set; } = true;
        }

        [UnityTest]
        public IEnumerator TheDriverRaisesTheOpenBottleneckWindowAtTheDepartureAndAtQuit()
        {
            string root = RunPlayerFixtures.NewRoot();
            var parent = new GameObject("session-log-driver-test");
            try
            {
                var files = new SessionLogFileSystem();
                var writer = new SessionLogWriter(new SessionLogOptions
                {
                    Root = Path.Combine(root, "log"),
                    Prefs = new MemoryPrefs(),
                    Files = files,
                    Background = job => { },
                    BuildKind = RunBuildKind.Editor,
                });
                RunRig rig = RunRig.Create(root, RunPlayerFixtures.ScaledCatalog(), RunPlayerFixtures.StartUtcMs, writer);
                rig.ColdStartOk(null);
                var built = rig.Commands.Build(0, "machine.miner"); Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
                // A full store: not even one unit fits, so the Miner is blocked on a full store.
                var full = new[] { new IdleFactory.Run.Content.ResourceAmount("iron.ore", rig.Session.State.Inventory.Capacity) };
                Assert.That(rig.Session.State.Inventory.TryImport(full), Is.EqualTo(IdleFactory.Run.Domain.RunStateResult.Ok));
                const long step = 10_000_000; // 1 s
                for (int i = 0; i < 11; i++) rig.Session.Advance(step); // blocked after the first step: ten counted seconds

                var runtime = new SessionLogRuntime(writer, new SessionLogExport(writer, files));
                runtime.Attach(rig.Session, parent.transform);
                yield return null;
                GameObject driver = parent.transform.Find("SessionLogDriver").gameObject;

                driver.SendMessage("OnApplicationPause", true);
                string text = File.ReadAllText(Path.Combine(writer.LogDirectoryPath, SessionLogWriter.SegmentName(0)));
                Assert.That(text, Does.Contain("\"t\":\"bottleneck\""), "on disk at the focus-loss checkpoint");
                Assert.That(text, Does.Contain("\"storeFullSeconds\":10"));

                for (int i = 0; i < 5; i++) rig.Session.Advance(step);
                driver.SendMessage("OnApplicationQuit");
                text = File.ReadAllText(Path.Combine(writer.LogDirectoryPath, SessionLogWriter.SegmentName(0)));
                Assert.That(text, Does.Contain("\"storeFullSeconds\":5"), "and at quit");
            }
            finally
            {
                Object.Destroy(parent);
                RunPlayerFixtures.DeleteRoot(root);
            }
        }
    }
}
