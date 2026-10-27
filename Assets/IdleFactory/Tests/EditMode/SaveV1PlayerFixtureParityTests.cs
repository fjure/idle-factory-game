using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T121 (QA-2): the StandalonePlayer tests cannot read the editor-only fixture folders, so the bytes they need are
    /// committed a second time as Play Mode <see cref="TextAsset"/>s in a <c>Resources</c> folder (the only way to package a
    /// stand-alone asset in a player without a scene). This class proves that every copy is byte-identical to its original
    /// by SHA-256, so a player test can never run against stale bytes. <see cref="PlayerCopy.AssertParity"/> is the reusable
    /// check (T122 adds its Save V2 fixtures to <see cref="Pairs"/>).
    /// </summary>
    public sealed class SaveV1PlayerFixtureParityTests
    {
        private const string PlayerResources = "Assets/IdleFactory/Tests/PlayMode/Fixtures/SaveV1/Resources/RunPlayerFixtures/";

        internal readonly struct PlayerCopy
        {
            public PlayerCopy(string playerCopyPath, string originalPath)
            {
                PlayerCopyPath = playerCopyPath;
                OriginalPath = originalPath;
            }

            public string PlayerCopyPath { get; }
            public string OriginalPath { get; }

            public override string ToString() => PlayerCopyPath;

            public static string Sha256(byte[] bytes)
            {
                using (var sha = SHA256.Create())
                {
                    return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
                }
            }

            /// <summary>Fails unless the copy is a packaged TextAsset in a <c>Resources</c> folder whose bytes hash-equal the original's.</summary>
            public void AssertParity()
            {
                Assert.That(File.Exists(OriginalPath), Is.True, "Original missing: " + OriginalPath);
                Assert.That(File.Exists(PlayerCopyPath), Is.True, "Player copy missing: " + PlayerCopyPath);
                Assert.That(PlayerCopyPath, Does.Contain("/Resources/"), "A player copy must live in a Resources folder to be packaged.");

                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(PlayerCopyPath);
                Assert.That(asset, Is.Not.Null, "The player copy is not imported as a TextAsset: " + PlayerCopyPath);

                string original = Sha256(File.ReadAllBytes(OriginalPath));
                Assert.That(Sha256(File.ReadAllBytes(PlayerCopyPath)), Is.EqualTo(original), "File bytes differ from " + OriginalPath);
                Assert.That(Sha256(asset.bytes), Is.EqualTo(original), "TextAsset bytes differ from " + OriginalPath);
            }
        }

        internal static readonly PlayerCopy[] Pairs =
        {
            new PlayerCopy(PlayerResources + "02-idle-placed-d001.json",
                "Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV1/02-idle-placed-d001.json"),
            new PlayerCopy(PlayerResources + "iron-factory-content.json",
                "Assets/IdleFactory/Content/Iron/iron-factory-content.json"),
            new PlayerCopy(PlayerResources + "development-catalog.json",
                "Assets/IdleFactory/Content/Development/development-catalog.json"),
        };

        [Test]
        public void EveryPlayerCopyHashEqualsItsEditModeOriginal()
        {
            foreach (var pair in Pairs)
            {
                pair.AssertParity();
            }
        }

        [Test]
        public void ADifferingByteChangesTheHashSoTheCheckFails()
        {
            byte[] original = File.ReadAllBytes(Pairs[0].OriginalPath);
            byte[] tampered = (byte[])original.Clone();
            tampered[tampered.Length / 2] ^= 0x01;

            Assert.That(PlayerCopy.Sha256(tampered), Is.Not.EqualTo(PlayerCopy.Sha256(original)));
            Assert.That(PlayerCopy.Sha256(original), Is.EqualTo(PlayerCopy.Sha256((byte[])original.Clone())));

            // A copy that points at a different original must be reported, not silently accepted.
            var mismatched = new PlayerCopy(Pairs[0].PlayerCopyPath, Pairs[1].OriginalPath);
            Assert.That(() => mismatched.AssertParity(), Throws.InstanceOf<AssertionException>());
        }
    }
}
