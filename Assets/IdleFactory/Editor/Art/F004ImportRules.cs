using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace IdleFactory.Editor.Art
{
    /// <summary>One frame of a manifest entry: the PNG path relative to the manifest folder and its SHA-256.</summary>
    [Serializable]
    public sealed class F004ManifestFrame
    {
        public string file;
        public string sha256;
    }

    /// <summary>
    /// One <c>import-manifest.json</c> entry as written by <c>tools/art-pipeline</c> (<c>build_manifest_entry</c>).
    /// <c>pivot</c> is normalized to the canvas with y up; <c>canvas</c> is in art pixels. An entry flagged
    /// <c>intentionallyEmpty</c> (owner decision R8, the running badge) has no PNG: none of <c>frames</c>,
    /// <c>files</c>, <c>png</c> or <c>path</c> may be present.
    /// </summary>
    [Serializable]
    public sealed class F004ManifestEntry
    {
        public string assetId;
        public string route;
        public int ppu;
        public int fps;
        public int[] canvas;
        public int[] footprintPx;
        public float[] pivot;
        public bool intentionallyEmpty;
        public F004ManifestFrame[] frames;
        public string[] files;
        public string png;
        public string path;
    }

    [Serializable]
    public sealed class F004Manifest
    {
        public int schemaVersion;
        public F004ManifestEntry[] entries;
    }

    /// <summary>What the importer does with one PNG.</summary>
    public enum F004ImportDecisionKind
    {
        /// <summary>The path is not governed by the F004 rules (outside the F004 art path, or in the spike folder).</summary>
        NotApplicable,

        /// <summary>The PNG is under the F004 art path but no manifest entry lists it. It is left untouched.</summary>
        Unlisted,

        /// <summary>The entry is valid and the PNG matches its hash: apply the settings.</summary>
        Apply,

        /// <summary>The manifest, entry or PNG hash is invalid. Nothing is applied and <see cref="F004ImportDecision.Message"/> says why.</summary>
        Rejected,
    }

    public sealed class F004ImportDecision
    {
        public F004ImportDecisionKind Kind;
        public string Message;
        public int Ppu;
        public Vector2 Pivot;
        public int CanvasWidth;
        public int CanvasHeight;
    }

    /// <summary>
    /// Pure rules of the F004 sprite import (T430, FR-022, FR-023, research R10): which PNGs the F004 importer governs,
    /// manifest parsing and validation (schema version, density, integral pivot, <c>intentionallyEmpty</c>) and the PNG
    /// hash check. Kept free of importer state so Edit Mode tests exercise it directly.
    /// </summary>
    public static class F004ImportRules
    {
        public const string ArtRoot = "Assets/IdleFactory/Art/F004";
        public const string SpikeRoot = ArtRoot + "/Spike";
        public const string ManifestFileName = "import-manifest.json";
        public const string ManifestPath = ArtRoot + "/" + ManifestFileName;
        public const int SupportedSchemaVersion = 1;

        /// <summary>Raise on every rule change so Unity re-imports every governed PNG.</summary>
        public const uint PostprocessorVersion = 2;

        /// <summary>Rule P1 of the Art Bible: <c>|pivotPx - round(pivotPx)| &lt; 1e-3</c> on both axes (same value as the pipeline).</summary>
        public const float PivotIntegralTolerance = 1e-3f;

        /// <summary>Art density D (pixels per cell) options: the sprite PPU equals D (research R03, R10). Art outside the F004 folder, such as the F001 placeholders, keeps its own PPU and is never governed here.</summary>
        public static readonly int[] AllowedDensities = { 16, 24, 32 };

        /// <summary>True for a PNG the F004 rules govern: under the F004 art path and not in the style-spike folder.</summary>
        public static bool IsGovernedPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return assetPath.StartsWith(ArtRoot + "/", StringComparison.Ordinal)
                && !assetPath.StartsWith(SpikeRoot + "/", StringComparison.Ordinal);
        }

        /// <summary>
        /// Every folder that may hold the manifest for a path, nearest first, ending at <see cref="ArtRoot"/>. The shipped
        /// manifest is <see cref="ManifestPath"/>; a nested manifest lets tests use a throw-away folder.
        /// </summary>
        public static IReadOnlyList<string> ManifestCandidates(string assetPath)
        {
            var result = new List<string>();
            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(folder) && folder.StartsWith(ArtRoot, StringComparison.Ordinal))
            {
                result.Add(folder + "/" + ManifestFileName);
                if (folder == ArtRoot)
                {
                    break;
                }

                folder = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            }

            return result;
        }

        public static bool TryParse(string json, out F004Manifest manifest, out string error)
        {
            manifest = null;
            try
            {
                manifest = JsonUtility.FromJson<F004Manifest>(json);
            }
            catch (Exception exception)
            {
                error = "manifest is not valid JSON: " + exception.Message;
                return false;
            }

            if (manifest == null)
            {
                error = "manifest is empty";
                return false;
            }

            if (manifest.schemaVersion != SupportedSchemaVersion)
            {
                error = "manifest schemaVersion " + manifest.schemaVersion + " is not supported (expected " + SupportedSchemaVersion + ")";
                manifest = null;
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Pivot in art pixels (normalized pivot times canvas, origin bottom-left), as the pipeline computes it.</summary>
        public static Vector2 PivotPixels(F004ManifestEntry entry)
        {
            return new Vector2(entry.pivot[0] * entry.canvas[0], entry.pivot[1] * entry.canvas[1]);
        }

        /// <summary>Diagnostics for one entry; empty when the entry is valid.</summary>
        public static List<string> ValidateEntry(F004ManifestEntry entry)
        {
            var problems = new List<string>();
            if (entry == null || string.IsNullOrEmpty(entry.assetId))
            {
                problems.Add("entry has no assetId");
                return problems;
            }

            var id = entry.assetId;
            var hasPngFields = (entry.frames != null && entry.frames.Length > 0)
                || (entry.files != null && entry.files.Length > 0)
                || !string.IsNullOrEmpty(entry.png)
                || !string.IsNullOrEmpty(entry.path);
            if (entry.intentionallyEmpty)
            {
                if (hasPngFields)
                {
                    problems.Add(id + ": intentionallyEmpty entry must not carry frames, files, png or path");
                }

                return problems;
            }

            if (Array.IndexOf(AllowedDensities, entry.ppu) < 0)
            {
                problems.Add(id + ": ppu " + entry.ppu + " is not an art density D (16, 24 or 32)");
            }

            if (entry.canvas == null || entry.canvas.Length != 2 || entry.canvas[0] <= 0 || entry.canvas[1] <= 0)
            {
                problems.Add(id + ": canvas must be two positive integers");
            }
            else if (entry.pivot == null || entry.pivot.Length != 2
                || entry.pivot[0] < 0f || entry.pivot[0] > 1f || entry.pivot[1] < 0f || entry.pivot[1] > 1f)
            {
                problems.Add(id + ": pivot must be two normalized values in [0, 1]");
            }
            else
            {
                var px = PivotPixels(entry);
                if (Mathf.Abs(px.x - Mathf.Round(px.x)) >= PivotIntegralTolerance
                    || Mathf.Abs(px.y - Mathf.Round(px.y)) >= PivotIntegralTolerance)
                {
                    problems.Add(id + ": pivot " + px.x.ToString("0.####", CultureInfo.InvariantCulture)
                        + ", " + px.y.ToString("0.####", CultureInfo.InvariantCulture)
                        + " art px is not a whole pixel (rule P1: |pivotPx - round(pivotPx)| < 1e-3)");
                }
            }

            if (entry.frames == null || entry.frames.Length == 0)
            {
                problems.Add(id + ": entry lists no frames");
            }
            else
            {
                foreach (var frame in entry.frames)
                {
                    if (frame == null || string.IsNullOrEmpty(frame.file) || !IsSha256(frame.sha256))
                    {
                        problems.Add(id + ": frame needs a file and a 64-digit sha256");
                    }
                }
            }

            return problems;
        }

        public static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// Resolves one PNG against a manifest. <paramref name="readBytes"/> returns the PNG bytes for the hash check.
        /// </summary>
        public static F004ImportDecision Resolve(string assetPath, string manifestPath, F004Manifest manifest, Func<string, byte[]> readBytes)
        {
            var manifestFolder = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/') ?? ArtRoot;
            foreach (var entry in manifest.entries ?? Array.Empty<F004ManifestEntry>())
            {
                if (entry == null || entry.frames == null)
                {
                    continue;
                }

                foreach (var frame in entry.frames)
                {
                    if (frame == null || string.IsNullOrEmpty(frame.file))
                    {
                        continue;
                    }

                    var framePath = (manifestFolder + "/" + frame.file.Replace('\\', '/')).Replace("//", "/");
                    if (!string.Equals(framePath, assetPath, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var problems = ValidateEntry(entry);
                    if (problems.Count > 0)
                    {
                        return Rejected(string.Join("; ", problems));
                    }

                    var bytes = readBytes(assetPath);
                    var actual = Sha256Hex(bytes);
                    if (!string.Equals(actual, frame.sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        return Rejected(entry.assetId + ": PNG hash " + actual + " does not match the manifest hash " + frame.sha256 + " (hand edit or stale manifest)");
                    }

                    if (TryReadPngSize(bytes, out var pngWidth, out var pngHeight)
                        && (pngWidth != entry.canvas[0] || pngHeight != entry.canvas[1]))
                    {
                        return Rejected(entry.assetId + ": PNG is " + pngWidth + "x" + pngHeight + " but the manifest canvas is "
                            + entry.canvas[0] + "x" + entry.canvas[1]);
                    }

                    return new F004ImportDecision
                    {
                        Kind = F004ImportDecisionKind.Apply,
                        Ppu = entry.ppu,
                        Pivot = new Vector2(entry.pivot[0], entry.pivot[1]),
                        CanvasWidth = entry.canvas[0],
                        CanvasHeight = entry.canvas[1],
                    };
                }
            }

            return new F004ImportDecision { Kind = F004ImportDecisionKind.Unlisted, Message = "no manifest entry lists this PNG" };
        }

        /// <summary>Reads width and height from the PNG IHDR chunk without decoding the image.</summary>
        public static bool TryReadPngSize(byte[] bytes, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (bytes == null || bytes.Length < 24 || bytes[1] != (byte)'P' || bytes[2] != (byte)'N' || bytes[3] != (byte)'G')
            {
                return false;
            }

            width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
            return true;
        }

        private static F004ImportDecision Rejected(string message)
        {
            return new F004ImportDecision { Kind = F004ImportDecisionKind.Rejected, Message = message };
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
            {
                return false;
            }

            foreach (var c in value)
            {
                if (!Uri.IsHexDigit(c))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
