using System;
using System.Collections;
using System.IO;
using System.Reflection;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// F006 T535 (FR-020 to FR-022): the hub ("Office") and Back in a real player. The packaged scene composes the hub and the Industry view from its
    /// serialized assets (no editor API); the entry opens the hub, which closes an open machine card; a card opens its system view through the registry;
    /// Back returns to the factory in one press.
    /// </summary>
    public sealed class HubPlayerTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static T Field<T>(object owner, string name)
        {
            var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, owner.GetType().Name + " no longer declares " + name);
            return (T)field.GetValue(owner);
        }

        [Category("StandalonePlayer")]
        [UnityTest]
        public IEnumerator TheHubOpensASystemViewAndBackReturnsToTheFactoryInAPlayer()
        {
            var checkpoints = Path.Combine(Application.persistentDataPath, "IdleFactory", "Checkpoints", "v1");
            var runCheckpoints = Path.Combine(Application.persistentDataPath, "IdleFactory", "Checkpoints", "v2");
            DeleteIfExists(checkpoints);
            DeleteIfExists(runCheckpoints);

            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null, "Could not start loading " + ScenePath);
            while (!load.isDone)
            {
                yield return null;
            }

            yield return null;
            var scene = SceneManager.GetSceneByPath(ScenePath);
            Exception failure = null;
            var body = Body(scene);
            while (true)
            {
                bool moved;
                try
                {
                    moved = body.MoveNext();
                }
                catch (Exception exception)
                {
                    failure = exception;
                    break;
                }

                if (!moved)
                {
                    break;
                }

                yield return body.Current;
            }

            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone)
            {
                yield return null;
            }

            DeleteIfExists(checkpoints);
            DeleteIfExists(runCheckpoints);
            if (failure != null)
            {
                throw failure;
            }
        }

        private static IEnumerator Body(Scene scene)
        {
            {
                CoreGameFoundationBootstrap bootstrap = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    bootstrap = root.GetComponentInChildren<CoreGameFoundationBootstrap>(true);
                    if (bootstrap != null)
                    {
                        break;
                    }
                }

                Assert.That(bootstrap, Is.Not.Null, "the bootstrap is in the packaged scene");
                Assert.That(bootstrap.IsInitialized, Is.True, bootstrap.InitializationError);
                for (var frame = 0; frame < 60 && !(bootstrap.RunShell != null && bootstrap.RunShell.IsReady && bootstrap.ShellPresenter != null); frame++)
                {
                    yield return null;
                }

                var shell = bootstrap.RunShell;
                Assert.That(shell, Is.Not.Null);
                Assert.That(shell.IsReady, Is.True, shell.Error);
                Assert.That(bootstrap.PanelDiagnostics, Is.Empty, "every panel, the machine card included, composed from the packaged assets");
                Assert.That(shell.HasMachineCard, Is.True, "the card is registered");

                var built = bootstrap.RunCommands.Build(0, "machine.miner");
                Assert.That(built.IsSuccess, Is.True, built.ReasonCode);
                for (var frame = 0; frame < 10; frame++)
                {
                    yield return null;
                }

                Assert.That(shell.Panels.IsRegistered(ShellPanelIds.Hub), Is.True, "the hub is registered");
                Assert.That(shell.Panels.IsRegistered(ShellPanelIds.Industry), Is.True, "Industry has its own registry id");
                Assert.That(shell.HubEntryText, Is.EqualTo("Office"));
                Assert.That(bootstrap.ShellPresenter.OpenSlot(0), Is.True);
                Assert.That(shell.IsCardOpen, Is.True);

                shell.OpenHub();
                for (var frame = 0; frame < 10; frame++)
                {
                    yield return null;
                }

                Assert.That(shell.IsHubOpen, Is.True, "the entry opens the hub");
                Assert.That(shell.IsCardOpen, Is.False, "exclusive with the card");
                Assert.That(shell.SelectedSlot, Is.EqualTo(-1));

                var market = shell.Root.Q<UnityEngine.UIElements.Button>("hub-card-market");
                Assert.That(market, Is.Not.Null, "the hub shows its cards");
                using (var submit = UnityEngine.UIElements.NavigationSubmitEvent.GetPooled())
                {
                    submit.target = market;
                    market.SendEvent(submit);
                }

                Assert.That(shell.ActivePanelId, Is.EqualTo(ShellPanelIds.Market), "a card opens its view through the registry");
                yield return null;
                Assert.That(shell.HandleBack(), Is.True, "Back returns to the factory in one press");
                Assert.That(shell.IsSheetOpen, Is.False);
            }
        }
    }
}
