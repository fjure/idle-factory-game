using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// F006 T538 (FR-003, FR-008a, research R20): display-v2.json = { version 2, reducedMotion, showZoomButtons }. The v2 file wins and
    /// is read by the strict v2 parser (invalid means defaults plus one diagnostic, never a v1 fallback); without it the unchanged v1
    /// parser reads display-v1.json and showZoomButtons is false; writes go atomically through display-v2.json.tmp and leave v1 in
    /// place; showZoomButtons is rejected in v1; 4096-byte cap; nothing throws and no diagnostic carries a path or content.
    /// </summary>
    public sealed class DisplayPreferencesV2Tests
    {
        private const string ValidV2 = "{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":true}";
        private const string ValidV1 = "{\"version\":1,\"reducedMotion\":\"off\"}";

        private string root;
        private List<string> diagnostics;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "idlefactory-display-v2-" + Guid.NewGuid().ToString("N"));
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

        private string Write(string name, byte[] bytes)
        {
            Directory.CreateDirectory(PreferencesDirectory);
            var path = Path.Combine(PreferencesDirectory, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        private static byte[] Concat(byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, result, 0, first.Length);
            Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
            return result;
        }

        private string WriteV1(string text) => Write("display-v1.json", Encoding.UTF8.GetBytes(text));

        private string WriteV2(string text) => Write("display-v2.json", Encoding.UTF8.GetBytes(text));

        // ------------------------------------------------------------------------------------------------------
        // v1 read and migration
        // ------------------------------------------------------------------------------------------------------

        [TestCase("followSystem", ReducedMotionPreference.FollowSystem)]
        [TestCase("on", ReducedMotionPreference.On)]
        [TestCase("off", ReducedMotionPreference.Off)]
        public void AV1FileWithoutAV2FileReadsWithTheV1ParserAndHidesTheZoomButtons(string token, ReducedMotionPreference expected)
        {
            WriteV1("{\"version\":1,\"reducedMotion\":\"" + token + "\"}");
            var store = Store();

            Assert.That(store.Reload(), Is.EqualTo(expected));
            Assert.That(store.ShowZoomButtons, Is.False);
            Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public void ShowZoomButtonsIsRejectedInAV1File()
        {
            WriteV1("{\"version\":1,\"reducedMotion\":\"on\",\"showZoomButtons\":true}");
            var store = Store();

            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(store.ShowZoomButtons, Is.False);
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
        }

        [Test]
        public void AV2VersionNumberInAV1FileIsInvalid()
        {
            WriteV1("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":true}");

            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(diagnostics, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheFirstChangeMigratesV1ToV2AndLeavesTheV1FileByteForByte()
        {
            var v1Path = WriteV1(ValidV1);
            var v1Bytes = File.ReadAllBytes(v1Path);
            var store = Store();
            store.Reload();

            Assert.That(store.TrySaveShowZoomButtons(true), Is.True);

            Assert.That(File.ReadAllText(store.FilePath), Is.EqualTo("{\"version\":2,\"reducedMotion\":\"off\",\"showZoomButtons\":true}"),
                "the v1 reduced-motion choice carries into v2");
            Assert.That(File.ReadAllBytes(v1Path), Is.EqualTo(v1Bytes), "v1 stays in place, unchanged");
            Assert.That(File.Exists(store.TempFilePath), Is.False);

            var fresh = Store();
            Assert.That(fresh.Reload(), Is.EqualTo(ReducedMotionPreference.Off));
            Assert.That(fresh.ShowZoomButtons, Is.True);
            Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public void ChangingReducedMotionFirstAlsoMigratesAndKeepsTheZoomChoiceFalse()
        {
            WriteV1(ValidV1);
            var store = Store();
            store.Reload();

            Assert.That(store.TrySave(ReducedMotionPreference.On), Is.True);

            Assert.That(File.ReadAllText(store.FilePath), Is.EqualTo("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":false}"));
        }

        [Test]
        public void ChangingTheMotionChoiceKeepsTheStoredZoomChoiceAndTheOtherWayRound()
        {
            var store = Store();
            Assert.That(store.TrySaveShowZoomButtons(true), Is.True);
            Assert.That(store.TrySave(ReducedMotionPreference.Off), Is.True);
            Assert.That(File.ReadAllText(store.FilePath), Is.EqualTo("{\"version\":2,\"reducedMotion\":\"off\",\"showZoomButtons\":true}"));

            Assert.That(store.TrySaveShowZoomButtons(false), Is.True);
            Assert.That(File.ReadAllText(store.FilePath), Is.EqualTo("{\"version\":2,\"reducedMotion\":\"off\",\"showZoomButtons\":false}"));
        }

        // ------------------------------------------------------------------------------------------------------
        // v2 read order
        // ------------------------------------------------------------------------------------------------------

        [TestCase("followSystem", "false", ReducedMotionPreference.FollowSystem, false)]
        [TestCase("followSystem", "true", ReducedMotionPreference.FollowSystem, true)]
        [TestCase("on", "false", ReducedMotionPreference.On, false)]
        [TestCase("on", "true", ReducedMotionPreference.On, true)]
        [TestCase("off", "false", ReducedMotionPreference.Off, false)]
        [TestCase("off", "true", ReducedMotionPreference.Off, true)]
        public void AValidV2FileReadsBothChoices(string token, string zoom, ReducedMotionPreference expected, bool expectedZoom)
        {
            WriteV2("{ \"version\": 2, \"reducedMotion\": \"" + token + "\", \"showZoomButtons\": " + zoom + " }\n");
            var store = Store();

            Assert.That(store.Reload(), Is.EqualTo(expected));
            Assert.That(store.ShowZoomButtons, Is.EqualTo(expectedZoom));
            Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public void V2KeyOrderWhitespaceAndABomDoNotMatter()
        {
            Write("display-v2.json", Concat(new byte[] { 0xEF, 0xBB, 0xBF }, Encoding.UTF8.GetBytes("\r\n\t{\"showZoomButtons\" :true,\r\n \"version\":2 , \"reducedMotion\":\"off\"}  \r\n")));
            var store = Store();

            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.Off));
            Assert.That(store.ShowZoomButtons, Is.True);
        }

        [Test]
        public void TheV2FileWinsOverTheV1File()
        {
            WriteV1(ValidV1);
            WriteV2("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":true}");
            var store = Store();

            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.On));
            Assert.That(store.ShowZoomButtons, Is.True);
            Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public void AnInvalidV2FileNeverFallsBackToAValidV1File()
        {
            WriteV1("{\"version\":1,\"reducedMotion\":\"on\"}");
            WriteV2("{\"version\":2,\"reducedMotion\":\"on\"");
            var store = Store();

            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem), "no silent v1 fallback once v2 exists");
            Assert.That(store.ShowZoomButtons, Is.False);
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
        }

        [Test]
        public void NeitherFileMeansDefaultsAndTheOncePerSessionDiagnostic()
        {
            var store = Store();

            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(store.ShowZoomButtons, Is.False);
            store.Reload();
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
            Assert.That(File.Exists(store.FilePath), Is.False, "reading never creates a file");
        }

        [Test]
        public void ReloadAfterAnInvalidV2ResetsAnEarlierValidChoiceToDefaults()
        {
            WriteV2(ValidV2);
            var store = Store();
            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.On));
            Assert.That(store.ShowZoomButtons, Is.True);

            WriteV2("not json");
            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(store.ShowZoomButtons, Is.False);
        }

        // ------------------------------------------------------------------------------------------------------
        // v2 strictness: malformed and unknown content
        // ------------------------------------------------------------------------------------------------------

        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":tru")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":true")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",")]
        [TestCase("{\"version\":2,")]
        [TestCase("{")]
        [TestCase("")]
        public void ATruncatedV2FileIsInvalid(string text)
        {
            WriteV2(text);

            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
        }

        [TestCase("{\"version\":1,\"reducedMotion\":\"on\",\"showZoomButtons\":true}")]
        [TestCase("{\"version\":3,\"reducedMotion\":\"on\",\"showZoomButtons\":true}")]
        [TestCase("{\"version\":0,\"reducedMotion\":\"on\",\"showZoomButtons\":true}")]
        [TestCase("{\"version\":22,\"reducedMotion\":\"on\",\"showZoomButtons\":true}")]
        [TestCase("{\"version\":-2,\"reducedMotion\":\"on\",\"showZoomButtons\":true}")]
        [TestCase("{\"version\":2.0,\"reducedMotion\":\"on\",\"showZoomButtons\":true}")]
        [TestCase("{\"version\":\"2\",\"reducedMotion\":\"on\",\"showZoomButtons\":true}")]
        [TestCase("{\"reducedMotion\":\"on\",\"showZoomButtons\":true}")]
        public void AnUnknownOrMissingV2VersionIsInvalid(string text)
        {
            WriteV2(text);

            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
        }

        [TestCase("{\"version\":2,\"reducedMotion\":\"on\"}")]
        [TestCase("{\"version\":2,\"showZoomButtons\":true}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":true,\"extra\":1}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoombuttons\":true}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"ShowZoomButtons\":true}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":true,\"showZoomButtons\":false}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"reducedMotion\":\"off\",\"showZoomButtons\":true}")]
        [TestCase("{\"version\":2,\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":true}")]
        public void MissingUnknownAndDuplicateV2MembersAreInvalid(string text)
        {
            WriteV2(text);

            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
        }

        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":1}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":0}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":\"true\"}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":null}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":[true]}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":True}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":truee}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":falsey}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":truefalse}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"sometimes\",\"showZoomButtons\":true}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"ON\",\"showZoomButtons\":true}")]
        [TestCase("{\"version\":2,\"reducedMotion\":true,\"showZoomButtons\":true}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"o\\u006e\",\"showZoomButtons\":true}")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":true} trailing")]
        [TestCase("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":true}{}")]
        [TestCase("[2]")]
        [TestCase("true")]
        public void WrongTypesValuesAndTrailingDataInV2AreInvalid(string text)
        {
            WriteV2(text);

            var store = Store();
            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(store.ShowZoomButtons, Is.False);
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
        }

        [Test]
        public void BinaryGarbageInV2IsInvalid()
        {
            Write("display-v2.json", new byte[] { 0xFF, 0xFE, 0x00, 0x7B, 0x00, 0x80, 0x81 });

            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(diagnostics, Has.Count.EqualTo(1));
        }

        [Test]
        public void EveryProperPrefixOfAValidV2FileIsRejectedWithoutThrowing()
        {
            var full = Encoding.UTF8.GetBytes(ValidV2);
            for (var length = 0; length < full.Length; length++)
            {
                var prefix = new byte[length];
                Buffer.BlockCopy(full, 0, prefix, 0, length);
                var ok = false;
                Assert.DoesNotThrow(() => ok = DisplayPreferencesStore.TryParseV2(prefix, out _, out _), "prefix " + length);
                Assert.That(ok, Is.False, "prefix of length " + length);
            }

            Assert.That(DisplayPreferencesStore.TryParseV2(full, out var motion, out var zoom), Is.True);
            Assert.That(motion, Is.EqualTo(ReducedMotionPreference.On));
            Assert.That(zoom, Is.True);
        }

        [Test]
        public void TheV2CapIs4096BytesAndAnOversizedFileIsNeverLoaded()
        {
            var json = ValidV2;
            var padded = json + new string(' ', DisplayPreferencesStore.MaxFileBytes - json.Length);
            WriteV2(padded);
            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.On), "exactly the cap is read");
            Assert.That(diagnostics, Is.Empty);

            WriteV2(padded + " ");
            var recording = new RecordingFileSystem();
            var store = Store(recording);
            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(store.ShowZoomButtons, Is.False);
            Assert.That(recording.ReadRequests, Is.Empty, "the length check rejects it before any byte is read");
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
        }

        [Test]
        public void AFileThatGrowsPastTheCapBetweenTheLengthCheckAndTheReadStillReadsAtMostOneByteOverTheCap()
        {
            WriteV2(ValidV2);
            var recording = new RecordingFileSystem { LengthOverride = 10 };
            WriteV2(new string(' ', DisplayPreferencesStore.MaxFileBytes * 3) + ValidV2);
            var store = Store(recording);

            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(recording.ReadRequests, Is.EqualTo(new[] { DisplayPreferencesStore.MaxFileBytes + 1 }), "the read is bounded by the cap");
        }

        [Test]
        public void AnUnreadableV2FileIsInvalidAndDoesNotThrow()
        {
            WriteV2(ValidV2);
            var store = Store(new RecordingFileSystem { ThrowOnRead = new UnauthorizedAccessException() });

            Assert.That(() => store.Reload(), Throws.Nothing);
            Assert.That(store.Preference, Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));
        }

        [Test]
        public void TheV2DiagnosticCarriesNoPathAndNoFileContent()
        {
            WriteV2("{\"version\":2,\"reducedMotion\":\"secret-content\",\"showZoomButtons\":true}");
            var store = Store();

            store.Reload();
            store.Reload();

            Assert.That(diagnostics, Is.EqualTo(new[] { "preferences.display.invalid" }));
            Assert.That(diagnostics[0], Does.Not.Contain(root));
            Assert.That(diagnostics[0], Does.Not.Contain("secret-content"));
        }

        // ------------------------------------------------------------------------------------------------------
        // Atomic write and the temp file
        // ------------------------------------------------------------------------------------------------------

        [Test]
        public void TheWriteGoesThroughTheV2TempFileAndAReplaceIntoTheV2File()
        {
            var recording = new RecordingFileSystem();
            var store = Store(recording);

            Assert.That(store.TrySaveShowZoomButtons(true), Is.True);

            Assert.That(Path.GetFileName(store.TempFilePath), Is.EqualTo("display-v2.json.tmp"));
            Assert.That(Path.GetFileName(store.FilePath), Is.EqualTo("display-v2.json"));
            Assert.That(recording.TempWrites, Is.EqualTo(new[] { store.TempFilePath }));
            Assert.That(recording.Replaces, Is.EqualTo(new[] { store.TempFilePath + " -> " + store.FilePath }));
            Assert.That(recording.Steps, Is.EqualTo(new[] { "write-temp", "replace" }), "the temp file is written before it replaces the target");
            Assert.That(File.Exists(store.TempFilePath), Is.False, "the temp file is consumed by the replace");
            Assert.That(Directory.GetFiles(PreferencesDirectory), Is.EqualTo(new[] { store.FilePath }));
        }

        [Test]
        public void ATempFileWrittenInTheSameDirectoryAsTheTarget()
        {
            var store = Store();

            Assert.That(Path.GetDirectoryName(store.TempFilePath), Is.EqualTo(Path.GetDirectoryName(store.FilePath)));
            Assert.That(store.FilePath, Does.StartWith(root), "the file stays under the persistence root");
        }

        [Test]
        public void AFailedReplaceLeavesThePriorV2FileUnchangedKeepsTheChoiceAndCleansTheTemp()
        {
            var v2Path = WriteV2(ValidV2);
            var original = File.ReadAllBytes(v2Path);
            var store = Store(new RecordingFileSystem { ThrowOnReplace = new IOException("disk full", unchecked((int)0x80070070)) });
            store.Reload();

            Assert.That(store.TrySaveShowZoomButtons(false), Is.False);

            Assert.That(File.ReadAllBytes(v2Path), Is.EqualTo(original), "the prior v2 file is unchanged");
            Assert.That(File.Exists(store.TempFilePath), Is.False, "the temp file is cleaned up");
            Assert.That(store.ShowZoomButtons, Is.False, "the choice still applies in memory");
            Assert.That(store.HasUnsavedChoice, Is.True);
            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.On));
            Assert.That(store.ShowZoomButtons, Is.False, "a re-read keeps the unsaved choice");
        }

        [TestCase(1, unchecked((int)0x80070497))]
        [TestCase(2, unchecked((int)0x80070497))]
        [TestCase(3, unchecked((int)0x80070497))]
        [TestCase(1, unchecked((int)0x80070498))]
        [TestCase(2, unchecked((int)0x80070020))]
        [TestCase(3, unchecked((int)0x80070021))]
        public void ATransientIoFailureOnTheReplaceIsRetriedAndThenSaves(int failures, int hresult)
        {
            var v2Path = WriteV2(ValidV2);
            var attempts = 0;
            var recording = new RecordingFileSystem { OnReplace = () => { if (attempts++ < failures) throw new IOException("transient", hresult); } };
            var store = Store(recording);
            store.Reload();

            Assert.That(store.TrySaveShowZoomButtons(false), Is.True);
            Assert.That(attempts, Is.EqualTo(failures + 1));
            Assert.That(store.HasUnsavedChoice, Is.False);
            Assert.That(File.ReadAllText(v2Path), Is.EqualTo("{\"version\":2,\"reducedMotion\":\"on\",\"showZoomButtons\":false}"));
            Assert.That(File.Exists(store.TempFilePath), Is.False);
        }

        [Test]
        public void AnAccessDeniedOnTheReplaceIsRetriedAndThenSaves()
        {
            var attempts = 0;
            var store = Store(new RecordingFileSystem { OnReplace = () => { if (attempts++ < 2) throw new UnauthorizedAccessException(); } });

            Assert.That(store.TrySave(ReducedMotionPreference.Off), Is.True);
            Assert.That(attempts, Is.EqualTo(3));
            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.Off));
        }

        [TestCase(unchecked((int)0x80070497))]
        [TestCase(unchecked((int)0x80070020))]
        public void AReplaceThatAlwaysFailsGivesUpAfterTheBoundedRetriesKeepsThePriorFileAndNeverThrows(int hresult)
        {
            var v2Path = WriteV2(ValidV2);
            var original = File.ReadAllBytes(v2Path);
            var attempts = 0;
            var store = Store(new RecordingFileSystem { OnReplace = () => { attempts++; throw new IOException("locked", hresult); } });
            store.Reload();

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var saved = true;
            Assert.That(() => saved = store.TrySaveShowZoomButtons(false), Throws.Nothing);
            watch.Stop();

            Assert.That(saved, Is.False);
            Assert.That(attempts, Is.EqualTo(1 + DisplayPreferencesStore.RetryDelaysMilliseconds.Length));
            Assert.That(File.ReadAllBytes(v2Path), Is.EqualTo(original), "the prior file stays intact");
            Assert.That(File.Exists(store.TempFilePath), Is.False, "the temp file is cleaned up");
            Assert.That(store.HasUnsavedChoice, Is.True);
            Assert.That(System.Linq.Enumerable.Sum(DisplayPreferencesStore.RetryDelaysMilliseconds), Is.LessThanOrEqualTo(150), "the main-thread wait stays bounded");
            Assert.That(diagnostics, Is.Empty, "a failed write reports nothing and logs no path");
        }

        [Test]
        public void OnlyTransientFailuresAreClassifiedAsRetryable()
        {
            Assert.That(DisplayPreferencesStore.IsTransient(new UnauthorizedAccessException()), Is.True);
            Assert.That(DisplayPreferencesStore.IsTransient(new IOException("x", unchecked((int)0x80070497))), Is.True);
            Assert.That(DisplayPreferencesStore.IsTransient(new IOException("x", unchecked((int)0x80070070))), Is.False, "disk full");
            Assert.That(DisplayPreferencesStore.IsTransient(new IOException("x", unchecked((int)0x80070003))), Is.False, "path not found");
            Assert.That(DisplayPreferencesStore.IsTransient(new InvalidOperationException()), Is.False);
        }

        [Test]
        public void ManyConsecutiveRealSavesAllSucceedAndTheLastOneWins()
        {
            // Back-to-back File.Replace calls fail about 0.6 percent of the time on this host without the retry; none may now.
            var store = Store();
            for (var i = 0; i < 400; i++)
            {
                Assert.That(store.TrySaveShowZoomButtons(i % 2 == 0), Is.True, "save " + i);
            }

            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem));
            Assert.That(File.ReadAllText(store.FilePath), Does.Contain("\"showZoomButtons\":false"));
            Assert.That(File.Exists(store.TempFilePath), Is.False);
        }

        [Test]
        public void AFailedTempWriteLeavesBothFilesUnchanged()
        {
            var v1Path = WriteV1(ValidV1);
            var v1Bytes = File.ReadAllBytes(v1Path);
            var store = Store(new RecordingFileSystem { ThrowOnWriteTemp = new IOException() });

            Assert.That(store.TrySaveShowZoomButtons(true), Is.False);
            Assert.That(File.ReadAllBytes(v1Path), Is.EqualTo(v1Bytes));
            Assert.That(File.Exists(store.FilePath), Is.False, "no v2 file appears from a failed write");
            Assert.That(store.ShowZoomButtons, Is.True);
        }

        [Test]
        public void AStaleTempFileIsNeverReadAndTheNextWriteOverwritesIt()
        {
            Write("display-v2.json.tmp", Encoding.UTF8.GetBytes(ValidV2));
            var store = Store();

            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.FollowSystem), "a lone temp file is not a preferences file");
            Assert.That(store.ShowZoomButtons, Is.False);
            Assert.That(diagnostics, Is.EqualTo(new[] { DisplayPreferencesStore.InvalidDiagnosticCode }));

            Assert.That(store.TrySave(ReducedMotionPreference.Off), Is.True);
            Assert.That(File.Exists(store.TempFilePath), Is.False, "the write consumed the stale temp file");
            Assert.That(Store().Reload(), Is.EqualTo(ReducedMotionPreference.Off));
        }

        [Test]
        public void AStaleTempFileDoesNotShadowAValidV1File()
        {
            WriteV1(ValidV1);
            Write("display-v2.json.tmp", Encoding.UTF8.GetBytes("garbage"));
            var store = Store();

            Assert.That(store.Reload(), Is.EqualTo(ReducedMotionPreference.Off));
            Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public void ADirectoryWhereTheV2FileBelongsFailsTheSaveWithoutThrowing()
        {
            Directory.CreateDirectory(Path.Combine(PreferencesDirectory, "display-v2.json"));
            var store = Store();

            Assert.That(() => store.TrySaveShowZoomButtons(true), Throws.Nothing);
            Assert.That(store.ShowZoomButtons, Is.True);
        }

        [Test]
        public void ChangedIsRaisedByTheZoomSaveToo()
        {
            var raised = 0;
            var store = Store();
            store.Changed += () => raised++;

            store.TrySaveShowZoomButtons(true);
            store.TrySave(ReducedMotionPreference.On);
            store.Reload();

            Assert.That(raised, Is.EqualTo(2), "both saves raise it and Reload does not");
        }

        // ------------------------------------------------------------------------------------------------------
        // Doubles
        // ------------------------------------------------------------------------------------------------------

        private sealed class RecordingFileSystem : IDisplayPreferencesFileSystem
        {
            private readonly IDisplayPreferencesFileSystem inner = SystemDisplayPreferencesFileSystem.Instance;

            public readonly List<int> ReadRequests = new List<int>();
            public readonly List<string> TempWrites = new List<string>();
            public readonly List<string> Replaces = new List<string>();
            public readonly List<string> Steps = new List<string>();
            public long? LengthOverride;
            public Exception ThrowOnRead;
            public Exception ThrowOnWriteTemp;
            public Exception ThrowOnReplace;
            public Action OnReplace;

            public bool FileExists(string path) => inner.FileExists(path);

            public long FileLength(string path) => LengthOverride ?? inner.FileLength(path);

            public byte[] ReadAll(string path, int maxBytes)
            {
                ReadRequests.Add(maxBytes);
                if (ThrowOnRead != null)
                {
                    throw ThrowOnRead;
                }

                return inner.ReadAll(path, maxBytes);
            }

            public void CreateDirectory(string path) => inner.CreateDirectory(path);

            public void WriteTemp(string path, byte[] bytes)
            {
                Steps.Add("write-temp");
                TempWrites.Add(path);
                if (ThrowOnWriteTemp != null)
                {
                    throw ThrowOnWriteTemp;
                }

                inner.WriteTemp(path, bytes);
            }

            public void ReplaceOrMove(string tempPath, string destinationPath)
            {
                Steps.Add("replace");
                Replaces.Add(tempPath + " -> " + destinationPath);
                OnReplace?.Invoke();
                if (ThrowOnReplace != null)
                {
                    throw ThrowOnReplace;
                }

                inner.ReplaceOrMove(tempPath, destinationPath);
            }

            public void DeleteQuietly(string path) => inner.DeleteQuietly(path);
        }
    }
}
