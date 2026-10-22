using System.IO;
using NUnit.Framework;
using PlayerCopy = IdleFactory.Tests.EditMode.SaveV1PlayerFixtureParityTests.PlayerCopy;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T336 (QA-2, PF-11): the W005-L Save V3 fixture the StandalonePlayer timing test loads is a Play Mode <c>TextAsset</c> copy in a
    /// <c>Resources</c> folder. This class proves that the copy is byte-identical (SHA-256) to the committed Edit Mode original, reusing
    /// <see cref="PlayerCopy"/> of the Save V1 parity tests, and that the hash the player test pins is the one the fixture README lists.
    /// </summary>
    public sealed class SaveV3PlayerFixtureParityTests
    {
        private const string PlayerResources = "Assets/IdleFactory/Tests/PlayMode/Fixtures/SaveV3/Resources/W005PlayerFixtures/";
        private const string Originals = "Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV3/";
        private const string Readme = Originals + "README.md";

        /// <summary>The SHA-256 of <c>01-w005-l.json</c> that <c>W005PlayerTimingTests</c> pins (QA-2). A regeneration changes both.</summary>
        internal const string W005LSha256 = "7066eed15513d2bdeb2c349bfa98c2a3fdc500d6b179b997ed8fe92b954a70fd";

        internal static readonly PlayerCopy[] Pairs =
        {
            new PlayerCopy(PlayerResources + "01-w005-l.json", Originals + "01-w005-l.json"),
        };

        [Test]
        public void EveryW005PlayerCopyHashEqualsItsEditModeOriginal()
        {
            foreach (var pair in Pairs)
            {
                pair.AssertParity();
            }
        }

        [Test]
        public void TheReadmeListsTheHashOfEveryPlayerCopy()
        {
            string readme = File.ReadAllText(Readme);
            foreach (var pair in Pairs)
            {
                string hash = PlayerCopy.Sha256(File.ReadAllBytes(pair.PlayerCopyPath));
                Assert.That(readme, Does.Contain(hash), pair.PlayerCopyPath);
            }
        }

        [Test]
        public void ThePlayerTestPinsTheHashOfTheCommittedFixture()
        {
            Assert.That(PlayerCopy.Sha256(File.ReadAllBytes(Pairs[0].OriginalPath)), Is.EqualTo(W005LSha256),
                "W005PlayerTimingTests and this class pin the same SHA-256; update both with the fixture (PF-16).");
        }

        [Test]
        public void AMismatchedOriginalIsReportedNotAccepted()
        {
            var mismatched = new PlayerCopy(Pairs[0].PlayerCopyPath, Originals + "README.md");
            Assert.That(() => mismatched.AssertParity(), Throws.InstanceOf<AssertionException>());
        }
    }
}
