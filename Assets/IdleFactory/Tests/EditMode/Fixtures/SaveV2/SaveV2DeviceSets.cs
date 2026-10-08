using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using IdleFactory.Domain;
using IdleFactory.Run.Application;
using IdleFactory.Tests.EditMode.Fixtures.Run;
using IdleFactory.Unity.Persistence;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>One file of a device checkpoint set: its name inside the set folder and its exact bytes.</summary>
    internal sealed class SaveV2DeviceFile
    {
        public string Name;
        public byte[] Bytes;
    }

    /// <summary>
    /// The committed device checkpoint sets (T138, PE-6): a complete Save V2 checkpoint set written through the real
    /// <see cref="JsonRunCheckpointStore"/> into a temporary root, so the emulator run (T123) can push the files
    /// unchanged into <c>.../IdleFactory/Checkpoints/v2/</c>. The W003-S set is the same state as fixture
    /// <c>06-w003-s-stress.json</c>, so its data file is byte-identical to that fixture. Its departure
    /// (<see cref="W003Workload.StartUtcMs"/>, November 2023) lies far in the past, so every cold start reconciles the
    /// capped eight hours. The set holds the published slots only (data-a and manifest-a); the pending slot the protocol
    /// also writes is scratch and is left out.
    /// </summary>
    internal static class SaveV2DeviceSets
    {
        public const string W003SDirectory = "device/w003-s";

        public static readonly string[] W003SFiles =
        {
            SaveV2CheckpointFiles.DataAFileName,
            SaveV2CheckpointFiles.ManifestAFileName,
        };

        /// <summary>The W003-S checkpoint written by the store into a temporary root; returns the published files and the pending file.</summary>
        public static List<SaveV2DeviceFile> BuildW003S(out byte[] pendingBytes)
        {
            W003Scenario scenario = W003Workload.Build(W003Variant.Stress);
            var checkpoint = new RunCheckpoint(
                new CheckpointId(SaveV2Scenarios.Uuid(10 + (int)W003Variant.Stress)),
                0,
                scenario.StartUtcMs,
                scenario.Catalog.OfflinePolicy.Revision,
                null,
                scenario.Catalog.ContentRevision,
                null,
                scenario.State,
                scenario.Permanent);
            string root = RunCheckpointTestSupport.NewTempRoot();
            try
            {
                var store = new JsonRunCheckpointStore(root, scenario.Catalog);
                var promoted = store.Promote(checkpoint);
                if (!promoted.Succeeded) throw new InvalidOperationException("The W003-S checkpoint was not promoted: " + promoted.Message);
                string directory = RunCheckpointTestSupport.V2Directory(root);
                var names = Directory.GetFiles(directory).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
                var expected = new[]
                {
                    SaveV2CheckpointFiles.DataAFileName, SaveV2CheckpointFiles.ManifestAFileName, SaveV2CheckpointFiles.PendingFileName,
                }.OrderBy(n => n, StringComparer.Ordinal).ToArray();
                if (!names.SequenceEqual(expected)) throw new InvalidOperationException("Unexpected files written by the store.");
                pendingBytes = File.ReadAllBytes(Path.Combine(directory, SaveV2CheckpointFiles.PendingFileName));
                return W003SFiles.Select(name => new SaveV2DeviceFile { Name = name, Bytes = File.ReadAllBytes(Path.Combine(directory, name)) }).ToList();
            }
            finally
            {
                RunCheckpointTestSupport.DeleteQuietly(root);
            }
        }

        public static List<SaveV2DeviceFile> BuildW003S() => BuildW003S(out _);

        /// <summary>Writes the set into <paramref name="fixturesDirectory"/>/<see cref="W003SDirectory"/> (regeneration only).</summary>
        public static void WriteW003S(string fixturesDirectory)
        {
            string directory = Path.Combine(fixturesDirectory, W003SDirectory);
            Directory.CreateDirectory(directory);
            foreach (SaveV2DeviceFile file in BuildW003S()) File.WriteAllBytes(Path.Combine(directory, file.Name), file.Bytes);
        }

        /// <summary>The README section that lists the device sets with their scenario and SHA-256 hashes.</summary>
        public static string ReadmeSection()
        {
            var b = new StringBuilder();
            b.Append("\n## Device checkpoint sets\n\n");
            b.Append("Complete checkpoint sets written through `JsonRunCheckpointStore` (T138) into a temporary root and committed unchanged, for the ");
            b.Append("emulator run (T123, PE-6): push the files into `IdleFactory/Checkpoints/v2/` on the device and verify the hashes there. ");
            b.Append("`SaveV2StoreTests.DeviceSet_W003S_IsByteIdenticalToTheStoreRegeneration` regenerates the set and compares it byte for byte; ");
            b.Append("drift fails and never skips (OPS-5), and `IDLEFACTORY_REGENERATE_SAVEV2_FIXTURES=1` rewrites it. The sets are not valid top-level ");
            b.Append("fixtures: the folders are not listed above and the top-level test ignores them.\n\n");
            b.Append("| File | Scenario | SHA-256 |\n|---|---|---|\n");
            string scenario = "W003-S (auto-sell sanity) as the published active generation of a fresh store: checkpoint `" +
                              SaveV2Scenarios.Uuid(10 + (int)W003Variant.Stress).ToString("D") + "`, generation 0, departure `" +
                              W003Workload.StartUtcMs + "` (far in the past, so a cold start reconciles the capped eight hours), ";
            foreach (SaveV2DeviceFile file in BuildW003S())
            {
                string detail = file.Name == SaveV2CheckpointFiles.DataAFileName
                    ? "data slot A; byte-identical to `06-w003-s-stress.json`."
                    : "manifest slot A (sequence 1, no previous generation, binds the data file by SHA-256).";
                b.Append("| `" + W003SDirectory + "/" + file.Name + "` | " + scenario + detail + " | `" + SaveV2Scenarios.Sha256(file.Bytes) + "` |\n");
            }

            b.Append(ShippedDeviceSets.ReadmeRowsAndNotes());
            return b.ToString();
        }
    }
}
