using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Tests.EditMode.SessionLog;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T432 (Feature 004 FR-001, FR-020, research R11), re-baselined for the v2 file by F006 T538 (R20; the v2 cases are in
    /// <see cref="DisplayPreferencesV2Tests"/>; here the v1 file is still the input and the v2 file the write target): the display-preferences file is read by a strict token walk with a 4 KB
    /// limit, everything invalid reads as followSystem with one code-only diagnostic per session, the write is atomic and leaves
    /// the prior file on failure, checkpoint and session-log cleanup never touch it, and the reduced-motion resolver matrix holds.
    /// </summary>
    public sealed class DisplayPreferencesStoreTests
    {
        private string root;
        private List<string> diagnostics;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "idlefactory-display-prefs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            diagnostics = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
            catch (Exception)
            {
                // A leftover temp directory does not fail a test.
            }
        }

        private DisplayPreferencesStore Store(IDisplayPreferencesFileSystem fileSystem = null) =>
            new DisplayPreferencesStore(root, fileSystem ?? SystemDisplayPreferencesFileSystem.Instance, diagnostics.Add);

        private string PreferencesDirectory => Path.Combine(root, "IdleFactory", "Preferences");

        private string WriteRaw(byte[] bytes)
        {
            Directory.CreateDirectory(PreferencesDirectory);
            var path = Path.Combine(PreferencesDirectory, "display-v1.json");
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private string WriteRaw(string text) => WriteRaw(Encoding.UTF8.GetBytes(text));

        // ------------------------------------------------------------------------------------------------------
        // Reading
        // ------------------------------------------------------------------------------------------------------

        [TestCase("followSystem", ReducedMotionPreference.FollowSystem)]
        [TestCase("on", ReducedMotionPreference.On)]
        [TestCase("off", ReducedMotionPreference.Off)]
        public void ValidFileReadsItsChoice(string token, ReducedMotionPreference expected)
        {
            WriteRaw("{ \"version\": 1, \"reducedMotion\": \"" + token + "\" }\n");
            var store = Store();

            Assert.That(store.Reload(), Is.EqualTo(expected));
            Assert.That(store.Preference, Is.EqualTo(expected));
            Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public void KeyOrderAndWhitespaceDoNotMatter()
        {
            WriteRaw("\r\n\t{\"reducedMotion\" :\"on\",\r\n \"version\":1}  \r\n");
            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.On));
        }

        [Test]
        public void MissingFileIsFollowSystemWithTheDiagnosticOncePerSession()
        {
            var store = Store();

            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(File.Exists(store.FilePath), Is.False, "reading never creates the file");
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));

            store.Reload();
            store.Reload();
            Assert.That(diagnostics, Has.Count.EqualTo(1), "a re-read while the file stays missing does not repeat it");

            Assert.That(store.TrySave(ReducedMotionPreference.On), Is.True);
            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.On), "a save makes the file valid");
            Assert.That(diagnostics, Has.Count.EqualTo(1));
        }

        [TestCase("{\"version\":1,\"reducedMotion\":\"o")]
        [TestCase("{\"version\":1,\"reducedMotion\":")]
        [TestCase("{\"version\":1,")]
        [TestCase("{")]
        [TestCase("")]
        [TestCase("{\"version\":1,\"reducedMotion\":\"on\"")]
        public void TruncatedFileIsFollowSystem(string text)
        {
            WriteRaw(text);
            var store = Store();

            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
        }

        [Test]
        public void OversizedFileIsFollowSystemEvenWhenItsContentIsValid()
        {
            const string json = "{\"version\":1,\"reducedMotion\":\"on\"}";
            WriteRaw(json + new string(' ', DisplayPreferencesStore.MaxFileBytes + 1 - json.Length));
            var store = Store();

            Assert.That(new FileInfo(store.V1FilePath).Length, Is.EqualTo(DisplayPreferencesStore.MaxFileBytes + 1));
            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
        }

        [Test]
        public void AFileExactlyAtTheLimitIsStillRead()
        {
            const string json = "{\"version\":1,\"reducedMotion\":\"on\"}";
            WriteRaw(json + new string(' ', DisplayPreferencesStore.MaxFileBytes - json.Length));
            var store = Store();

            Assert.That(new FileInfo(store.V1FilePath).Length, Is.EqualTo(DisplayPreferencesStore.MaxFileBytes));
            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.On));
            Assert.That(diagnostics, Is.Empty);
        }

        [TestCase("{\"version\":2,\"reducedMotion\":\"on\"}")]
        [TestCase("{\"version\":0,\"reducedMotion\":\"on\"}")]
        [TestCase("{\"version\":10,\"reducedMotion\":\"on\"}")]
        [TestCase("{\"version\":-1,\"reducedMotion\":\"on\"}")]
        [TestCase("{\"reducedMotion\":\"on\"}")]
        public void AnyVersionOtherThanOneIsFollowSystem(string text)
        {
            WriteRaw(text);
            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
        }

        [TestCase("{\"version\":1,\"reducedMotion\":\"on\",\"extra\":true}")]
        [TestCase("{\"version\":1,\"reducedMotion\":\"on\",\"Version\":1}")]
        [TestCase("{\"version\":1,\"reducedmotion\":\"on\"}")]
        [TestCase("{\"version\":1}")]
        [TestCase("{\"version\":1,\"reducedMotion\":\"on\",\"version\":1}")]
        public void UnknownMissingOrDuplicateMembersAreFollowSystem(string text)
        {
            WriteRaw(text);
            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
        }

        [TestCase("{\"version\":1,\"reducedMotion\":\"sometimes\"}")]
        [TestCase("{\"version\":1,\"reducedMotion\":\"ON\"}")]
        [TestCase("{\"version\":1,\"reducedMotion\":1}")]
        [TestCase("{\"version\":1,\"reducedMotion\":true}")]
        [TestCase("{\"version\":1,\"reducedMotion\":null}")]
        [TestCase("{\"version\":1,\"reducedMotion\":[\"on\"]}")]
        [TestCase("{\"version\":1,\"reducedMotion\":{\"v\":\"on\"}}")]
        [TestCase("{\"version\":\"1\",\"reducedMotion\":\"on\"}")]
        [TestCase("{\"version\":1.0,\"reducedMotion\":\"on\"}")]
        [TestCase("{\"version\":1e0,\"reducedMotion\":\"on\"}")]
        [TestCase("{\"version\":true,\"reducedMotion\":\"on\"}")]
        [TestCase("{\"version\":null,\"reducedMotion\":\"on\"}")]
        [TestCase("{\"version\":1,\"reducedMotion\":\"o\\u006e\"}")]
        [TestCase("[1]")]
        [TestCase("\"on\"")]
        [TestCase("{\"version\":1,\"reducedMotion\":\"on\"} trailing")]
        [TestCase("{\"version\":1,\"reducedMotion\":\"on\"}{}")]
        public void WrongTypesAndValuesAreFollowSystem(string text)
        {
            WriteRaw(text);
            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
        }

        [Test]
        public void BinaryGarbageIsFollowSystem()
        {
            WriteRaw(new byte[] { 0xFF, 0xFE, 0x00, 0x7B, 0x00, 0x80, 0x81 });
            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(diagnostics, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheInvalidDiagnosticIsReportedOncePerSessionAndCarriesNoPathOrContent()
        {
            WriteRaw("{\"version\":1,\"reducedMotion\":\"secret-content\"}");
            var store = Store();

            store.Reload();
            store.Reload();
            store.Reload();
            Assert.That(diagnostics, Is.EqualTo(new[] { "preferences.display.invalid" }), "a focus-return re-read does not repeat it");
            Assert.That(diagnostics[0], Does.Not.Contain(root));
            Assert.That(diagnostics[0], Does.Not.Contain("secret-content"));

            WriteRaw("{\"version\":1,\"reducedMotion\":\"on\"}");
            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.On));
            WriteRaw("not json");
            store.Reload();
            Assert.That(diagnostics, Has.Count.EqualTo(1), "still once for the whole session");
        }

        [Test]
        public void AnUnreadableFileIsFollowSystemAndDoesNotThrow()
        {
            WriteRaw("{\"version\":1,\"reducedMotion\":\"on\"}");
            var store = Store(new ScriptedFileSystem { OnReadAll = () => throw new UnauthorizedAccessException() });

            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
        }

        [Test]
        public void ADiagnosticSinkThatThrowsDoesNotChangeTheOutcome()
        {
            WriteRaw("garbage");
            var store = new DisplayPreferencesStore(root, SystemDisplayPreferencesFileSystem.Instance, code => throw new InvalidOperationException());

            Assert.That(() => store.Reload(), Throws.Nothing);
            Assert.That(store.Preference, Is.EqualTo(ReducedMotionPreference.FollowSystem));
        }

        // ------------------------------------------------------------------------------------------------------
        // Writing
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void SaveWritesTheContractFileAtTheContractPathAndRoundTrips()
        {
            var store = Store();

            Assert.That(store.TrySave(ReducedMotionPreference.Off), Is.True);
            Assert.That(store.FilePath, Is.EqualTo(Path.Combine(root, "IdleFactory", "Preferences", "display-v2.json")));
            Assert.That(File.ReadAllText(store.FilePath), Is.EqualTo("{\"version\":2,\"reducedMotion\":\"off\",\"showZoomButtons\":false}"));
            Assert.That(Directory.GetFiles(PreferencesDirectory), Has.Length.EqualTo(1), "no temp file is left behind");

            var fresh = Store();
            Assert.That(fresh.Reload(), Is.EqualTo(ReducedMotionPreference.Off));
            Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public void SaveReplacesAnExistingFileAndRewritesAnInvalidOne()
        {
            WriteRaw("garbage");
            var store = Store();
            store.Reload();

            Assert.That(store.TrySave(ReducedMotionPreference.On), Is.True);
            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.On));
            Assert.That(store.TrySave(ReducedMotionPreference.FollowSystem), Is.True);
            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(Directory.GetFiles(PreferencesDirectory, "*.tmp"), Is.Empty, "no temp file is left behind");
            Assert.That(Directory.GetFiles(PreferencesDirectory), Has.Length.EqualTo(2), "the invalid v1 file stays next to the v2 file");
        }

        [Test]
        public void AFailedAtomicReplaceLeavesThePriorFileUnchangedAndTheChoiceInMemory()
        {
            var original = Encoding.UTF8.GetBytes("{\"version\":1,\"reducedMotion\":\"off\"}");
            var path = WriteRaw(original);
            var store = Store(new ScriptedFileSystem { OnReplace = () => throw new IOException() });
            store.Reload();

            Assert.That(store.TrySave(ReducedMotionPreference.On), Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original), "the prior file is unchanged");
            Assert.That(Directory.GetFiles(PreferencesDirectory), Has.Length.EqualTo(1), "the temp file is cleaned up");
            Assert.That(store.Preference, Is.EqualTo(ReducedMotionPreference.On), "the choice still applies");
            Assert.That(store.HasUnsavedChoice, Is.True);
            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.On), "a focus-return re-read keeps the unsaved choice");
        }

        [Test]
        public void ATransientLockOnTheReplaceIsRetriedOnceAndStillSaves()
        {
            var original = Encoding.UTF8.GetBytes("{\"version\":1,\"reducedMotion\":\"off\"}");
            WriteRaw(original);
            var attempts = 0;
            var store = Store(new ScriptedFileSystem { OnReplace = () => { if (attempts++ == 0) throw Sharing("transient lock"); } });
            store.Reload();

            Assert.That(store.TrySave(ReducedMotionPreference.On), Is.True, "one transient IOException does not show 'Not saved'");
            Assert.That(attempts, Is.EqualTo(2));
            Assert.That(store.HasUnsavedChoice, Is.False);
            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.On));
            Assert.That(Directory.GetFiles(PreferencesDirectory, "*.tmp"), Is.Empty);
            Assert.That(Directory.GetFiles(PreferencesDirectory), Has.Length.EqualTo(2), "the v1 file stays next to the new v2 file");
        }

        [Test]
        public void ThePersistentLockIsRetriedBoundedThenFails()
        {
            var attempts = 0;
            var store = Store(new ScriptedFileSystem { OnReplace = () => { attempts++; throw Sharing("locked"); } });

            Assert.That(store.TrySave(ReducedMotionPreference.On), Is.False);
            Assert.That(attempts, Is.EqualTo(1 + DisplayPreferencesStore.RetryDelaysMilliseconds.Length), "bounded retries, not an open loop");
            Assert.That(store.HasUnsavedChoice, Is.True);
        }

        [Test]
        public void ALockViolationHResultIsAlsoRetriedOnce()
        {
            var attempts = 0;
            var store = Store(new ScriptedFileSystem { OnReplace = () => { if (attempts++ == 0) throw new IOException("lock", unchecked((int)0x80070021)); } });

            Assert.That(store.TrySave(ReducedMotionPreference.On), Is.True);
            Assert.That(attempts, Is.EqualTo(2));
        }

        [Test]
        public void ANonTransientIoExceptionIsNotRetried()
        {
            var attempts = 0;
            var store = Store(new ScriptedFileSystem { OnReplace = () => { attempts++; throw new IOException("disk full", unchecked((int)0x80070070)); } });

            Assert.That(store.TrySave(ReducedMotionPreference.On), Is.False);
            Assert.That(attempts, Is.EqualTo(1), "only transient failures earn the bounded retry");
            Assert.That(store.HasUnsavedChoice, Is.True);
        }

        private static IOException Sharing(string message) => new IOException(message, unchecked((int)0x80070020));

        [Test]
        public void ChangedIsRaisedByEverySaveSucceededOrNotAndNotByReload()
        {
            var raised = 0;
            var fs = new ScriptedFileSystem();
            var store = Store(fs);
            store.Changed += () => raised++;
            store.Reload();
            Assert.That(raised, Is.EqualTo(0));

            store.TrySave(ReducedMotionPreference.On);
            fs.OnWriteTemp = () => throw new IOException();
            store.TrySave(ReducedMotionPreference.Off);
            Assert.That(raised, Is.EqualTo(2));

            store.Changed += () => throw new InvalidOperationException("a subscriber must not break the save");
            Assert.That(() => store.TrySave(ReducedMotionPreference.FollowSystem), Throws.Nothing);
        }

        [Test]
        public void AFailureWhileWritingTheTempFileLeavesThePriorFileUnchanged()
        {
            var original = Encoding.UTF8.GetBytes("{\"version\":1,\"reducedMotion\":\"on\"}");
            var path = WriteRaw(original);
            var store = Store(new ScriptedFileSystem { OnWriteTemp = () => throw new IOException() });

            Assert.That(store.TrySave(ReducedMotionPreference.Off), Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(PreferencesDirectory), Has.Length.EqualTo(1));
        }

        [Test]
        public void ASuccessfulSaveAfterAFailureClearsTheUnsavedFlag()
        {
            var fs = new ScriptedFileSystem { OnReplace = () => throw new IOException() };
            var store = Store(fs);
            Assert.That(store.TrySave(ReducedMotionPreference.On), Is.False);

            fs.OnReplace = null;
            Assert.That(store.TrySave(ReducedMotionPreference.On), Is.True);
            Assert.That(store.HasUnsavedChoice, Is.False);
            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.On));
        }

        [Test]
        public void AReadOnlyDirectoryFailsTheSaveWithoutThrowingAndKeepsTheChoice()
        {
            var original = Encoding.UTF8.GetBytes("{\"version\":1,\"reducedMotion\":\"off\"}");
            var path = WriteRaw(original);
            var store = Store(new ScriptedFileSystem
            {
                OnWriteTemp = () => throw new UnauthorizedAccessException(),
                OnDeleteQuietly = () => throw new UnauthorizedAccessException(),
            });

            // The scripted delete throws too; the store must still return false instead of propagating it.
            Assert.That(() => store.TrySave(ReducedMotionPreference.On), Throws.Nothing);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(store.Preference, Is.EqualTo(ReducedMotionPreference.On));
        }

        [Test]
        public void ABlockedDirectoryPathFailsTheRealSaveWithoutThrowing()
        {
            // A plain file where the "IdleFactory" directory must be makes CreateDirectory fail for real.
            File.WriteAllText(Path.Combine(root, "IdleFactory"), "blocker");
            var store = Store();

            Assert.That(store.TrySave(ReducedMotionPreference.On), Is.False);
            Assert.That(store.Preference, Is.EqualTo(ReducedMotionPreference.On));
            Assert.That(store.HasUnsavedChoice, Is.True);
            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.On));
        }

        // ------------------------------------------------------------------------------------------------------
        // Isolation from the save and the session log
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void ThePreferencesFileIsASiblingOfCheckpointsAndSessionLogNeverInsideThem()
        {
            var store = Store();
            var preferences = Path.GetDirectoryName(store.FilePath);
            var application = Path.Combine(root, "IdleFactory");

            Assert.That(Path.GetDirectoryName(preferences), Is.EqualTo(application));
            Assert.That(Path.GetFileName(preferences), Is.EqualTo("Preferences"));
            Assert.That(store.FilePath, Does.Not.Contain(Path.Combine("IdleFactory", "Checkpoints")));
            Assert.That(store.FilePath, Does.Not.Contain(Path.Combine("IdleFactory", "SessionLog")));
            Assert.That(store.FilePath, Does.Not.Contain(Path.Combine("IdleFactory", "Exports")));
        }

        [Test]
        public void CheckpointWritesLeaveThePreferencesFileUntouched()
        {
            var game = SaveV2Scenarios.PlayedGame();
            var bytes = Encoding.UTF8.GetBytes("{\"version\":1,\"reducedMotion\":\"on\"}");
            var path = WriteRaw(bytes);
            var stamp = File.GetLastWriteTimeUtc(path);
            var store = new JsonRunCheckpointStore(root, game[0].Catalog);

            Assert.That(store.Promote(RunCheckpointTestSupport.CheckpointOf(game[0])).Succeeded, Is.True);
            Assert.That(store.Promote(RunCheckpointTestSupport.CheckpointOf(game[2])).Succeeded, Is.True);

            Assert.That(Directory.Exists(Path.Combine(root, "IdleFactory", "Checkpoints")), Is.True);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(stamp));
            Assert.That(Directory.GetFiles(PreferencesDirectory), Has.Length.EqualTo(1));
        }

        [Test]
        public void SessionLogClearAndExportCleanupLeaveThePreferencesFileUntouched()
        {
            var bytes = Encoding.UTF8.GetBytes("{\"version\":1,\"reducedMotion\":\"off\"}");
            var path = WriteRaw(bytes);
            var stamp = File.GetLastWriteTimeUtc(path);
            var files = new SessionLogFileSystem();
            var harness = Harness.Create(options =>
            {
                options.Root = root;
                options.Files = files;
            });
            harness.RunInline = true;
            harness.Writer.Start();
            for (var i = 0; i < 3; i++)
            {
                harness.Writer.OnContract(new RunContractEvent(RunContractEventKind.Accept, "contract.deliver", i, 5, 7, 1_500_000));
            }

            var export = new SessionLogExport(harness.Writer, files);
            Assert.That(export.Export().IsExported, Is.True, "the export (which prunes its directory) ran");
            harness.Writer.Clear();

            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(stamp));
            Assert.That(Directory.GetFiles(PreferencesDirectory), Has.Length.EqualTo(1));
        }

        // ------------------------------------------------------------------------------------------------------
        // Resolver matrix and platform signal
        // ------------------------------------------------------------------------------------------------------

        public enum SignalKind
        {
            Reduce,
            NoReduce,
            Throws,
            Missing,
        }

        private static IReducedMotionSignal SignalOf(SignalKind kind)
        {
            switch (kind)
            {
                case SignalKind.Reduce: return new FixedSignal(() => true);
                case SignalKind.NoReduce: return new FixedSignal(() => false);
                case SignalKind.Throws: return new FixedSignal(() => throw new InvalidOperationException());
                default: return null;
            }
        }

        [TestCase(ReducedMotionPreference.On, SignalKind.Reduce, true, ReducedMotionSource.Preference)]
        [TestCase(ReducedMotionPreference.On, SignalKind.NoReduce, true, ReducedMotionSource.Preference)]
        [TestCase(ReducedMotionPreference.On, SignalKind.Throws, true, ReducedMotionSource.Preference)]
        [TestCase(ReducedMotionPreference.On, SignalKind.Missing, true, ReducedMotionSource.Preference)]
        [TestCase(ReducedMotionPreference.Off, SignalKind.Reduce, false, ReducedMotionSource.Preference)]
        [TestCase(ReducedMotionPreference.Off, SignalKind.NoReduce, false, ReducedMotionSource.Preference)]
        [TestCase(ReducedMotionPreference.Off, SignalKind.Throws, false, ReducedMotionSource.Preference)]
        [TestCase(ReducedMotionPreference.Off, SignalKind.Missing, false, ReducedMotionSource.Preference)]
        [TestCase(ReducedMotionPreference.FollowSystem, SignalKind.Reduce, true, ReducedMotionSource.System)]
        [TestCase(ReducedMotionPreference.FollowSystem, SignalKind.NoReduce, false, ReducedMotionSource.System)]
        [TestCase(ReducedMotionPreference.FollowSystem, SignalKind.Throws, false, ReducedMotionSource.System)]
        [TestCase(ReducedMotionPreference.FollowSystem, SignalKind.Missing, false, ReducedMotionSource.System)]
        public void ResolverMatrix(ReducedMotionPreference preference, SignalKind signal, bool reduced, ReducedMotionSource source)
        {
            var effective = ReducedMotionResolver.Resolve(preference, SignalOf(signal));

            Assert.That(effective.Reduced, Is.EqualTo(reduced));
            Assert.That(effective.Source, Is.EqualTo(source));
        }

        [Test]
        public void ThePlatformSignalNeverThrowsAndAnswersABool()
        {
            IReducedMotionSignal signal = new ReducedMotionSignal();

            Assert.That(() => signal.ShouldReduceMotion(), Throws.Nothing);
            Assert.That(() => ReducedMotionResolver.Resolve(ReducedMotionPreference.FollowSystem, signal), Throws.Nothing);
        }

        // ------------------------------------------------------------------------------------------------------
        // Test doubles
        // ------------------------------------------------------------------------------------------------------

        private sealed class FixedSignal : IReducedMotionSignal
        {
            private readonly Func<bool> answer;

            public FixedSignal(Func<bool> answer)
            {
                this.answer = answer;
            }

            public bool ShouldReduceMotion() => answer();
        }

        /// <summary>The real file system with a scripted failure at one step.</summary>
        private sealed class ScriptedFileSystem : IDisplayPreferencesFileSystem
        {
            private readonly IDisplayPreferencesFileSystem inner = SystemDisplayPreferencesFileSystem.Instance;

            public Action OnReadAll;
            public Action OnWriteTemp;
            public Action OnReplace;
            public Action OnDeleteQuietly;

            public bool FileExists(string path) => inner.FileExists(path);

            public long FileLength(string path) => inner.FileLength(path);

            public byte[] ReadAll(string path, int maxBytes)
            {
                OnReadAll?.Invoke();
                return inner.ReadAll(path, maxBytes);
            }

            public void CreateDirectory(string path) => inner.CreateDirectory(path);

            public void WriteTemp(string path, byte[] bytes)
            {
                OnWriteTemp?.Invoke();
                inner.WriteTemp(path, bytes);
            }

            public void ReplaceOrMove(string tempPath, string destinationPath)
            {
                OnReplace?.Invoke();
                inner.ReplaceOrMove(tempPath, destinationPath);
            }

            public void DeleteQuietly(string path)
            {
                OnDeleteQuietly?.Invoke();
                inner.DeleteQuietly(path);
            }
        }
    }
}
