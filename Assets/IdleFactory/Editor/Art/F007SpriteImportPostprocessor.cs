using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Editor.Art
{
    /// <summary>A prototype look folder of the F007 art (contracts/art-pipeline-v2.md).</summary>
    public enum F007Look
    {
        /// <summary>Direction A: pixel route, point filter, palette.</summary>
        A,

        /// <summary>Direction B: <c>render-hd</c> soft-alpha sprites.</summary>
        B,

        /// <summary>Direction C: <c>render-hd</c> environment sprites (the machines are meshes).</summary>
        C,
    }

    /// <summary>The import settings of one look.</summary>
    public readonly struct F007LookImport
    {
        public F007LookImport(int ppu, FilterMode filter, int mipLevels, bool tightMesh, TextureImporterFormat androidFormat, TextureImporterFormat windowsFormat)
        {
            Ppu = ppu;
            Filter = filter;
            MipLevels = mipLevels;
            TightMesh = tightMesh;
            AndroidFormat = androidFormat;
            WindowsFormat = windowsFormat;
        }

        /// <summary>Art pixels per world unit, so that one cell stays one world unit.</summary>
        public int Ppu { get; }

        public FilterMode Filter { get; }

        /// <summary>Mip levels the look uses; 0 means mipmaps are off (TA-7: limited mips).</summary>
        public int MipLevels { get; }

        /// <summary>Soft-alpha looks use tight meshes to cut overdraw (TA-7); the pixel look uses full rects.</summary>
        public bool TightMesh { get; }

        public TextureImporterFormat AndroidFormat { get; }

        public TextureImporterFormat WindowsFormat { get; }

        /// <summary>Sprite Atlas padding: at least 2, and at least 2^MipLevels so a mip never bleeds a neighbour (TA-7).</summary>
        public int AtlasPadding => Math.Max(2, 1 << MipLevels);
    }

    /// <summary>
    /// T613 (FR-019, FR-034; research R14 amendments TA-4 to TA-8): per-look import rules for F007 art, and the path rules that keep them away from every
    /// F004 and F006 asset. Pure and testable; the postprocessor and the atlas settings only apply what is decided here.
    /// </summary>
    /// <remarks>
    /// Assumptions recorded in tools/art-pipeline/README-v2.md: PPU per look follows R05 (A 48, B 128); C reuses 128 because its floor and
    /// environment sprites are in the B style. Mips stay off in M2 (limited to zero) until the M2 measurement says otherwise.
    /// </remarks>
    public static class F007ImportRules
    {
        public const string ArtRoot = "Assets/IdleFactory/Art/F007";
        public const string PrototypeRoot = ArtRoot + "/Prototype";

        /// <summary>Filename markers: the shadow pass is a single-channel mask, the normal pass is data.</summary>
        public const string ShadowMarker = ".shadow.";
        public const string NormalMarker = ".normal.";

        public static F007LookImport For(F007Look look)
        {
            switch (look)
            {
                case F007Look.A:
                    return new F007LookImport(48, FilterMode.Point, 0, false, TextureImporterFormat.RGBA32, TextureImporterFormat.RGBA32);
                case F007Look.B:
                case F007Look.C:
                    return new F007LookImport(128, FilterMode.Bilinear, 0, true, TextureImporterFormat.ASTC_6x6, TextureImporterFormat.BC7);
                default:
                    throw new ArgumentOutOfRangeException(nameof(look));
            }
        }

        /// <summary>True for a PNG under the F007 art path, and only there.</summary>
        public static bool IsGovernedPath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath)
                && assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                && assetPath.StartsWith(ArtRoot + "/", StringComparison.Ordinal);
        }

        /// <summary>
        /// The look of a governed path: the folder directly under <c>Art/F007/Prototype/</c> (<c>A</c>, <c>B</c> or <c>C</c>). False for any other path,
        /// which includes every F004 and F006 asset.
        /// </summary>
        public static bool TryGetLook(string assetPath, out F007Look look)
        {
            look = F007Look.A;
            if (!IsGovernedPath(assetPath) || !assetPath.StartsWith(PrototypeRoot + "/", StringComparison.Ordinal))
            {
                return false;
            }

            var rest = assetPath.Substring(PrototypeRoot.Length + 1);
            var slash = rest.IndexOf('/');
            return slash > 0 && Enum.TryParse(rest.Substring(0, slash), false, out look) && Enum.IsDefined(typeof(F007Look), look);
        }

        public static bool IsShadowPass(string assetPath) => assetPath.IndexOf(ShadowMarker, StringComparison.OrdinalIgnoreCase) >= 0;

        public static bool IsNormalPass(string assetPath) => assetPath.IndexOf(NormalMarker, StringComparison.OrdinalIgnoreCase) >= 0;

        [Serializable]
        private sealed class PivotRow
        {
            public float[] pivot;
        }

        /// <summary>Reads the normalised pivot (origin bottom-left) from a render-hd manifest row.</summary>
        public static bool TryReadPivot(string manifestJson, out Vector2 pivot)
        {
            pivot = new Vector2(0.5f, 0.5f);
            if (string.IsNullOrEmpty(manifestJson))
            {
                return false;
            }

            var row = JsonUtility.FromJson<PivotRow>(manifestJson);
            if (row == null || row.pivot == null || row.pivot.Length != 2 || row.pivot[0] < 0f || row.pivot[0] > 1f || row.pivot[1] < 0f || row.pivot[1] > 1f)
            {
                return false;
            }

            pivot = new Vector2(row.pivot[0], row.pivot[1]);
            return true;
        }

        /// <summary>Writes the look's rules into an importer. The caller decides whether the path is governed.</summary>
        public static void Apply(TextureImporter importer, F007Look look, string assetPath, Vector2? pivot)
        {
            var rules = For(look);
            var shadow = IsShadowPass(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = rules.Ppu;
            importer.filterMode = rules.Filter;
            importer.mipmapEnabled = rules.MipLevels > 0;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.crunchedCompression = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = !shadow;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.sRGBTexture = !IsNormalPass(assetPath) && !shadow;
            importer.maxTextureSize = 2048;

            var android = shadow ? TextureImporterFormat.Alpha8 : rules.AndroidFormat;
            var windows = shadow ? TextureImporterFormat.Alpha8 : rules.WindowsFormat;
            var compressed = !shadow && look != F007Look.A;
            importer.textureCompression = compressed ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
            SetPlatform(importer, "Android", android, compressed);
            SetPlatform(importer, "Standalone", windows, compressed);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = rules.TightMesh ? SpriteMeshType.Tight : SpriteMeshType.FullRect;
            if (pivot.HasValue)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot.Value;
            }

            importer.SetTextureSettings(settings);
            if (pivot.HasValue)
            {
                importer.spritePivot = pivot.Value;
            }
        }

        private static void SetPlatform(TextureImporter importer, string platform, TextureImporterFormat format, bool compressed)
        {
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = platform,
                overridden = true,
                maxTextureSize = 2048,
                format = format,
                textureCompression = compressed ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed,
                crunchedCompression = false,
            });
        }
    }

    /// <summary>
    /// T613: Sprite Atlas v2 settings per family and look (<c>Assets/IdleFactory/Art/F007/Atlases/&lt;look&gt;/&lt;family&gt;.spriteatlasv2</c>). The page rule in
    /// force stays the F004 one, at most 512 x 512 (1 MiB of RGBA32), until the T614 sweep passes on the Pixel and the owner accepts a change at M4. Compressed
    /// looks cap at the same page size, so their GPU memory is below the budget as well.
    /// </summary>
    public static class F007SpriteAtlasSettings
    {
        public const string AtlasFolder = F007ImportRules.ArtRoot + "/Atlases";

        /// <summary>The page cap in force; deliberately the F004 constants, so raising it is a change to one place and to its test.</summary>
        public const int MaxPageSize = F004SpriteAtlasSettings.MaxPageSize;

        public static int MaxPageBytes => F004SpriteAtlasSettings.MaxPageBytes;

        /// <summary>RGBA32-equivalent bytes of a square page (the unit of the page rule).</summary>
        public static int PageBytes(int pageSize) => pageSize * pageSize * 4;

        /// <summary>The atlas asset path of a family: <c>&lt;folder&gt;/&lt;look&gt;/&lt;family&gt;.spriteatlasv2</c> (one atlas per family and look).</summary>
        public static string AtlasPath(F007Look look, string family, string folder = AtlasFolder)
        {
            return folder + "/" + look + "/" + family + F004SpriteAtlasSettings.AtlasExtension;
        }

        /// <summary>A family key is lower-case letters, digits and hyphens, starting with a letter or digit.</summary>
        public static bool IsValidFamily(string family)
        {
            return !string.IsNullOrEmpty(family) && System.Text.RegularExpressions.Regex.IsMatch(family, "^[a-z0-9][a-z0-9-]*$");
        }

        /// <summary>
        /// Creates (or reconfigures) the Sprite Atlas v2 of one family in one look and packs the given folders or sprites into it, mirroring
        /// <see cref="F004SpriteAtlasSettings.CreateFamilyAtlas"/>. Pages stay at 512 px (1 MiB of RGBA32) and padding follows the look. The result is
        /// validated and an invalid atlas throws, so a family can never ship with a page plan the rules forbid. Not called by any build step yet: the
        /// production families arrive with their art (M5), and <paramref name="folder"/> exists so tests can work outside <c>Art/F007</c>.
        /// </summary>
        public static UnityEditor.U2D.SpriteAtlasImporter CreateFamilyAtlas(F007Look look, string family, string folder, params UnityEngine.Object[] packables)
        {
            if (!IsValidFamily(family))
            {
                throw new System.ArgumentException("family must be lower-case letters, digits and hyphens: '" + family + "'", nameof(family));
            }

            if (PageBytes(MaxPageSize) > MaxPageBytes)
            {
                throw new System.InvalidOperationException("the page cap " + MaxPageSize + " exceeds the page budget of " + MaxPageBytes + " B");
            }

            var path = AtlasPath(look, family, folder);
            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace((char)92, '/'));
            if (AssetImporter.GetAtPath(path) == null)
            {
                var asset = new UnityEditor.U2D.SpriteAtlasAsset();
                if (packables != null && packables.Length > 0)
                {
                    asset.Add(packables);
                }

                UnityEditor.U2D.SpriteAtlasAsset.Save(asset, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = (UnityEditor.U2D.SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            Configure(importer, look);
            importer.SaveAndReimport();
            importer = (UnityEditor.U2D.SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            var problems = Validate(importer, look);
            if (problems.Count > 0)
            {
                throw new System.InvalidOperationException("F007 atlas " + path + " is invalid: " + string.Join("; ", problems));
            }

            return importer;
        }

        /// <summary>Same as the explicit-folder overload, in the production folder <see cref="AtlasFolder"/>.</summary>
        public static UnityEditor.U2D.SpriteAtlasImporter CreateFamilyAtlas(F007Look look, string family, params UnityEngine.Object[] packables)
        {
            return CreateFamilyAtlas(look, family, AtlasFolder, packables);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(folder).Replace((char)92, '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }

        /// <summary>Writes the look's atlas settings into an importer (does not save).</summary>
        public static void Configure(UnityEditor.U2D.SpriteAtlasImporter importer, F007Look look)
        {
            var rules = F007ImportRules.For(look);
            importer.packingSettings = new UnityEditor.U2D.SpriteAtlasPackingSettings
            {
                blockOffset = 1,
                padding = rules.AtlasPadding,
                enableRotation = false,
                enableTightPacking = rules.TightMesh,
                enableAlphaDilation = rules.Filter != FilterMode.Point,
            };
            importer.textureSettings = new UnityEditor.U2D.SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = rules.MipLevels > 0,
                sRGB = true,
                filterMode = rules.Filter,
                anisoLevel = 0,
            };
            Set(importer, "DefaultTexturePlatform", rules.WindowsFormat);
            Set(importer, "Standalone", rules.WindowsFormat);
            Set(importer, "Android", rules.AndroidFormat);
        }

        private static void Set(UnityEditor.U2D.SpriteAtlasImporter importer, string platform, TextureImporterFormat format)
        {
            importer.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name = platform,
                overridden = true,
                maxTextureSize = MaxPageSize,
                format = format,
                textureCompression = format == TextureImporterFormat.RGBA32 ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed,
                crunchedCompression = false,
            });
        }

        /// <summary>Violations of the look's atlas contract; empty when the importer matches.</summary>
        public static System.Collections.Generic.List<string> Validate(UnityEditor.U2D.SpriteAtlasImporter importer, F007Look look)
        {
            var rules = F007ImportRules.For(look);
            var problems = new System.Collections.Generic.List<string>();
            if (importer.packingSettings.padding < rules.AtlasPadding)
            {
                problems.Add("padding " + importer.packingSettings.padding + " (needs at least " + rules.AtlasPadding + ")");
            }

            if (importer.textureSettings.filterMode != rules.Filter)
            {
                problems.Add("filter " + importer.textureSettings.filterMode + " (expected " + rules.Filter + ")");
            }

            foreach (var platform in F004SpriteAtlasSettings.Platforms)
            {
                var s = importer.GetPlatformSettings(platform);
                if (!s.overridden)
                {
                    problems.Add(platform + ": no explicit override");
                }
                else if (s.maxTextureSize > MaxPageSize)
                {
                    problems.Add(platform + ": max page " + s.maxTextureSize + " (limit " + MaxPageSize + ")");
                }
            }

            return problems;
        }
    }

    /// <summary>
    /// T613: applies <see cref="F007ImportRules"/> to every PNG under <c>Assets/IdleFactory/Art/F007/Prototype/&lt;A|B|C&gt;/</c> and to nothing else.
    /// F004 and F006 PNGs keep their own postprocessors and their .meta files unchanged. The pivot comes from the render-hd manifest row next to the PNG
    /// (<c>&lt;asset&gt;.manifest.json</c>) when present.
    /// </summary>
    public sealed class F007SpriteImportPostprocessor : AssetPostprocessor
    {
        /// <summary>Raise on every rule change so Unity re-imports every governed PNG.</summary>
        public const uint PostprocessorVersion = 1;

        public override uint GetVersion() => PostprocessorVersion;

        private void OnPreprocessTexture()
        {
            if (!F007ImportRules.IsGovernedPath(assetPath))
            {
                return;
            }

            if (!F007ImportRules.TryGetLook(assetPath, out var look))
            {
                Debug.LogWarning("F007 import skipped " + assetPath + ": the PNG is not inside a look folder " + F007ImportRules.PrototypeRoot + "/<A|B|C>/.");
                return;
            }

            Vector2? pivot = null;
            var stem = Path.GetFileName(assetPath);
            var dot = stem.IndexOf('.');
            var manifest = Path.Combine(Path.GetDirectoryName(assetPath) ?? string.Empty, (dot > 0 ? stem.Substring(0, dot) : stem) + ".manifest.json");
            context.DependsOnSourceAsset(manifest.Replace('\\', '/'));
            if (File.Exists(manifest) && F007ImportRules.TryReadPivot(File.ReadAllText(manifest), out var read))
            {
                pivot = read;
            }

            F007ImportRules.Apply((TextureImporter)assetImporter, look, assetPath, pivot);
        }
    }
}
