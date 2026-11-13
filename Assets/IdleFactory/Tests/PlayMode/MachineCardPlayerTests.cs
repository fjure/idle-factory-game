using System;
using System.Collections;
using System.IO;
using System.Reflection;
using IdleFactory.Run.Application;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Presentation;
using IdleFactory.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IdleFactory.Tests.PlayMode
{
    /// <summary>
    /// F006 T534 (FR-016, FR-017): the machine card in a real player. The packaged scene composes the card from its serialized assets (no editor
    /// API), a machine tap opens it, the camera takes the band selection-driven (<c>OpenSheet</c>), and Back closes it and restores the view.
    /// </summary>
    public sealed class MachineCardPlayerTests
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
        public IEnumerator ATappedMachineOpensTheCardAndBackClosesItInAPlayer()
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

                var controller = Field<FactoryCameraController>(bootstrap, "cameraController");
                Assert.That(bootstrap.ShellPresenter.OpenSlot(0), Is.True, "a tap on the machine opens it");
                Assert.That(shell.IsCardOpen, Is.True, "as the docked card, not a sheet");
                Assert.That(shell.IsSheetOpen, Is.False);
                for (var frame = 0; frame < 20 && !controller.SheetOpen; frame++)
                {
                    yield return null;
                }

                Assert.That(controller.SheetOpen, Is.True, "the camera took the band the card leaves");

                Assert.That(shell.HandleBack(), Is.True, "Back closes the card");
                for (var frame = 0; frame < 20 && controller.SheetOpen; frame++)
                {
                    yield return null;
                }

                Assert.That(shell.IsCardOpen, Is.False);
                Assert.That(shell.SelectedSlot, Is.EqualTo(-1));
                Assert.That(controller.SheetOpen, Is.False, "the prior view returned");
            }
        }
    }
}
