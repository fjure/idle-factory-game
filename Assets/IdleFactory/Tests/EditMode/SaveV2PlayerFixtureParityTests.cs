using System.IO;
using NUnit.Framework;
using PlayerCopy = IdleFactory.Tests.EditMode.SaveV1PlayerFixtureParityTests.PlayerCopy;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T122 (QA-2, QA-7): the W003 Save V2 fixtures the StandalonePlayer timing test loads are Play Mode <c>TextAsset</c>
    /// copies in a <c>Resources</c> folder. This class proves that each copy is byte-identical (SHA-256) to its committed
    /// Edit Mode original, reusing <see cref="PlayerCopy"/> of the Save V1 parity tests, and that the hashes the player test
    /// records are the ones the fixture README lists.
    /// </summary>
    public sealed class SaveV2PlayerFixtureParityTests
    {
        private const string PlayerResources = "Assets/IdleFactory/Tests/PlayMode/Fixtures/SaveV2/Resources/W003PlayerFixtures/";
        private const string Originals = "Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV2/";
        private const string Readme = Originals + "README.md";

        internal static readonly PlayerCopy[] Pairs =
        {
            new PlayerCopy(PlayerResources + "05-w003-b-bound.json", Originals + "05-w003-b-bound.json"),
            new PlayerCopy(PlayerResources + "06-w003-s-stress.json", Originals + "06-w003-s-stress.json"),
            new PlayerCopy(PlayerResources + "07-w003-r-reference.json", Originals + "07-w003-r-reference.json"),
        };

        [Test]
        public void EveryW003PlayerCopyHashEqualsItsEditModeOriginal()
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
        public void AMismatchedOriginalIsReportedNotAccepted()
        {
            var mismatched = new PlayerCopy(Pairs[0].PlayerCopyPath, Pairs[1].OriginalPath);
            Assert.That(() => mismatched.AssertParity(), Throws.InstanceOf<AssertionException>());
        }
    }
}
