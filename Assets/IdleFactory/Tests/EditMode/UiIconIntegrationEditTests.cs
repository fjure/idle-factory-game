using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T441 (FR-014, US5): the shipped scene serves the restyled icon of every UI key (no Feature 003 placeholder), every key the UI code asks for
    /// exists, and the UI token contrast table holds after tint (Art Bible 9, rules C4 and C6; non-text 3:1).
    /// </summary>
    public sealed class UiIconIntegrationEditTests
    {
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private const string IconRoot = "Assets/IdleFactory/Art/F004/ui-icons";
        private const string ShellStyle = "Assets/IdleFactory/Unity/UI/FactoryRun.uss";
        private const string UiSource = "Assets/IdleFactory/Unity";
        private static readonly Color32 Glyph = new Color32(242, 245, 250, 255);

        private static string[] AllKeys() =>
            Directory.GetFiles(IconRoot, "*_00.png")
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .Select(name => name.Substring(0, name.Length - 3).ToUpperInvariant())
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();

        [Test]
        public void TheShippedSceneServesTheRestyledIconOfEveryUiKeyAndNoPlaceholder()
        {
            var keys = AllKeys();
            Assert.That(keys, Has.Length.EqualTo(50), "49 F003 keys and the one F004 settings key");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var bootstrap = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoreGameFoundationBootstrap>(true))
                    .Single();
                var catalog = bootstrap.SpriteCatalog;
                Assert.That(catalog.Validate(), Is.True, catalog.Error);
                foreach (var key in keys)
                {
                    Assert.That(catalog.TryGet(key, out var sprite), Is.True, key);
                    var path = AssetDatabase.GetAssetPath(sprite);
                    Assert.That(path, Does.StartWith(IconRoot + "/"), key + " is served from the restyled family, not the placeholder set");
                    Assert.That(sprite.rect.width, Is.EqualTo(24f).Within(0.01f), key + " is 24 art px");
                    Assert.That(sprite.rect.height, Is.EqualTo(24f).Within(0.01f), key);
                    Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point), key + " is point-filtered");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void EveryIconKeyTheUiCodeAsksForIsAShippedKeyAndTheUnboundOnesAreListed()
        {
            var shipped = new HashSet<string>(AllKeys());
            var asked = new HashSet<string>();
            foreach (var file in Directory.GetFiles(UiSource, "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(file), "\"(F00[34]-UI-ICON-[A-Z0-9-]+)\""))
                {
                    asked.Add(match.Groups[1].Value);
                }
            }

            // Keys built from an effect kind or a state come from tables the code owns.
            asked.UnionWith(new[]
            {
                "F003-UI-ICON-EFFECT-SPEED", "F003-UI-ICON-EFFECT-OUTPUT", "F003-UI-ICON-EFFECT-SALE-PRICE", "F003-UI-ICON-EFFECT-UPGRADE-COST",
            });
            Assert.That(asked.Except(shipped), Is.Empty, "no UI code asks for a key the family does not ship");
            var unbound = shipped.Except(asked).OrderBy(key => key, StringComparer.Ordinal).ToList();
            Directory.CreateDirectory("test-results/t441");
            File.WriteAllLines("test-results/t441/keys-not-bound-by-ui-code.txt", unbound);
            Assert.That(asked.Count, Is.GreaterThanOrEqualTo(30));
        }

        // ---- UI token contrast after tint ---------------------------------------------------------------------------

        private static Dictionary<string, Color32> ShellTokens()
        {
            var text = File.ReadAllText(ShellStyle);
            var block = text.Substring(text.IndexOf(".run-shell {", StringComparison.Ordinal));
            block = block.Substring(0, block.IndexOf('}'));
            var tokens = new Dictionary<string, Color32>();
            foreach (Match match in Regex.Matches(block, "--([a-z-]+):\\s*#([0-9A-Fa-f]{6});"))
            {
                var value = int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                tokens[match.Groups[1].Value] = new Color32((byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);
            }

            return tokens;
        }

        private static float Luminance(Color32 color)
        {
            float Channel(byte value)
            {
                var c = value / 255f;
                return c <= 0.03928f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
            }

            return 0.2126f * Channel(color.r) + 0.7152f * Channel(color.g) + 0.0722f * Channel(color.b);
        }

        private static float Contrast(Color32 a, Color32 b)
        {
            var la = Luminance(a);
            var lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        private static Color32 Tinted(Color32 tint) =>
            new Color32((byte)Mathf.RoundToInt(Glyph.r * tint.r / 255f), (byte)Mathf.RoundToInt(Glyph.g * tint.g / 255f), (byte)Mathf.RoundToInt(Glyph.b * tint.b / 255f), 255);

        private static string Hex(Color32 color) => $"#{color.r:X2}{color.g:X2}{color.b:X2}";

        [Test]
        public void EveryIconFileIsOneGlyphColourAndTheTintTableHoldsThreeToOneOnTheShellSurfaces()
        {
            // The glyph colour of the files is ui_text (#F2F5FA); the token tint multiplies it.
            foreach (var key in AllKeys())
            {
                var path = $"{IconRoot}/{key.ToLowerInvariant()}_00.png";
                var texture = new Texture2D(2, 2);
                try
                {
                    Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path)), Is.True, path);
                    foreach (var pixel in texture.GetPixels32().Where(p => p.a > 0))
                    {
                        Assert.That((pixel.r, pixel.g, pixel.b), Is.EqualTo((Glyph.r, Glyph.g, Glyph.b)), key + " is a single ui_text glyph colour");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            var tokens = ShellTokens();
            var surfaces = new[] { "bg", "surface", "surface-raised" };
            var tints = new[] { "text-strong", "text-primary", "text-secondary", "text-muted", "accent", "positive", "warning", "danger" };
            var table = new StringBuilder();
            table.AppendLine("| Tint token | Glyph x tint | vs --bg | vs --surface | vs --surface-raised |");
            table.AppendLine("|---|---|---:|---:|---:|");
            table.AppendLine("| (none: untinted) | " + Hex(Glyph) + " | " + string.Join(" | ", surfaces.Select(surface => Contrast(Glyph, tokens[surface]).ToString("0.00", CultureInfo.InvariantCulture))) + " |");
            foreach (var tint in tints)
            {
                var effective = Tinted(tokens[tint]);
                var ratios = surfaces.Select(surface => Contrast(effective, tokens[surface])).ToArray();
                table.AppendLine("| --" + tint + " | " + Hex(effective) + " | " + string.Join(" | ", ratios.Select(ratio => ratio.ToString("0.00", CultureInfo.InvariantCulture))) + " |");
                foreach (var (surface, ratio) in surfaces.Zip(ratios, (surface, ratio) => (surface, ratio)))
                {
                    Assert.That(ratio, Is.GreaterThanOrEqualTo(3f), $"--{tint} after tint on --{surface}");
                }
            }

            // An icon on a filled accent control is tinted --on-accent and seen against --accent.
            var onAccent = Tinted(tokens["on-accent"]);
            var onAccentRatio = Contrast(onAccent, tokens["accent"]);
            table.AppendLine("| --on-accent (on a --accent control) | " + Hex(onAccent) + " | vs --accent " + onAccentRatio.ToString("0.00", CultureInfo.InvariantCulture) + " | | |");
            Assert.That(onAccentRatio, Is.GreaterThanOrEqualTo(3f), "--on-accent after tint on --accent");

            Directory.CreateDirectory("test-results/t441");
            File.WriteAllText("test-results/t441/contrast-table.md", table.ToString());
        }
    }
}
