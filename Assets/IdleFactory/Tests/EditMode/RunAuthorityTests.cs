using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Content;
using IdleFactory.Unity.Persistence;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T119 (R16, GD-1, FR-031): the Feature 003 run is the only runtime the bootstrap composes. The bootstrap has exactly one
    /// gameplay content input (the version 2 document; the version 1 catalog only feeds the read-only Save V1 import), it never
    /// references the Feature 001 V1 writer <see cref="JsonCheckpointStore"/>, no asset under <c>Assets/</c> references a GUID of
    /// a retired file, and the retired types are gone from every IdleFactory assembly.
    /// </summary>
    public sealed class RunAuthorityTests
    {
        private const string BootstrapSourcePath = "Assets/IdleFactory/Unity/Bootstrap/CoreGameFoundationBootstrap.cs";
        private const string BootstrapDirectory = "Assets/IdleFactory/Unity/Bootstrap";
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";
        private const string ThisTestPath = "Assets/IdleFactory/Tests/EditMode/RunAuthorityTests.cs";

        /// <summary>The GUIDs of the files T119 deleted, with their former paths (Feature 001 runtime, HUD panels and their tests).</summary>
        private static readonly IReadOnlyDictionary<string, string> RetiredGuids = new Dictionary<string, string>
        {
            ["67776285154c4915ba2f6048a974c10a"] = "Core/Application/FactorySession.cs",
            ["9f8b0b90e0f04ef5a94034444970a7ef"] = "Core/Application/PlacementCommands.cs",
            ["bfa03466a7164dc8963459482fb8d842"] = "Core/Application/ProductionCommands.cs",
            ["eafb61db3a4178b41b5625bd5051dd79"] = "Core/Application/PersistenceCommands.cs",
            ["53848fe837b24c12a71e4f170f12f9ef"] = "Core/Application/DevelopmentSessionFactory.cs",
            ["8c811b8b8c9c4edb935ee4fd85ad03e5"] = "Unity/Input/PlacementInputController.cs",
            ["8c1b177fd1164a459bc9dd44cf05c85f"] = "Unity/Presentation/PlacementPreviewView.cs",
            ["8fbebfd4ab944ea68a8b68883f4cd51a"] = "Unity/Presentation/FactoryView.cs",
            ["4f83223a6f3246e29cf557d433a9c114"] = "Unity/Presentation/FactoryViewRegistry.cs",
            ["bf170c87aaca49638645c7a3d357a6db"] = "Unity/Bootstrap/ApplicationLifecycleAdapter.cs",
            ["de913e51aa33457da9335c1240cb060d"] = "Unity/UI/MachineInspectorPresenter.cs",
            ["2d9ae7b56ad641648ef27a822b0612e1"] = "Unity/UI/PersistencePresenter.cs",
            ["c05cb2e62bf927644ae99a834d95bbef"] = "Tests/EditMode/PersistenceApplicationTests.cs",
            ["f670927b886542cca97a68ad3cd23943"] = "Tests/EditMode/PlacementApplicationTests.cs",
            ["945aec349d2b42829af72ca67e533fd8"] = "Tests/EditMode/ProductionApplicationTests.cs",
            ["3affe73da6b6b5b46b093cff90e98661"] = "Tests/EditMode/InspectorPollingQueryTests.cs",
            ["948f94425ccdd154bba197668d63ff7b"] = "Tests/EditMode/MachinePresentationStateTests.cs",
            ["a03c4eedca364b06ba2bb5b1aac24402"] = "Tests/EditMode/SimulationEquivalenceTests.cs",
            ["19ba9f95b2794b81b8d04abee1fd3f05"] = "Tests/PlayMode/CoreGameFoundationSceneTests.cs",
            ["bd23173c00fb46bca74b20d7284f9dcf"] = "Tests/PlayMode/JsonContentPersistenceTests.cs",
            ["96216e85f7174e00914933f57b786c27"] = "Tests/PlayMode/LifecyclePersistenceTests.cs",
            ["27e7416145504383a512dfdc61e85495"] = "Tests/PlayMode/NavigationPlacementTests.cs",
            ["1ff71f30cba74e3ca4280ce3b5184b0b"] = "Tests/PlayMode/PersistenceUiTests.cs",
            ["9363d6bc08ce4692ac58727663842b80"] = "Tests/PlayMode/ViewRecreationTests.cs",
            ["27fe0d3a64ec61a4f8c00d6a49c5ba61"] = "Tests/PlayMode/ZoomReadabilityTests.cs",
            ["f6dde098cdd6fe94995f80671d54b937"] = "Tests/PlayMode/InspectorPollingTests.cs",
            ["5f769b5f053147af92384198ec8ccf27"] = "Tests/PlayMode/JsonContentBootstrapTests.cs",
            ["0a1475aa61744e59835afb9b83ad4cc3"] = "Tests/PlayMode/ProductionInspectorTests.cs",
            ["ebd6ff83baf34f719a1b6cd37c1fd2b5"] = "Tests/PlayMode/ColdStartRestoreRenderTests.cs",
            ["a7b82179e6c6ec74b9ebe1989c23e19f"] = "Tests/PlayMode/HudLayoutOverlapTests.cs",
            ["0600aeef3a7904048ac01cd80793af10"] = "Tests/PlayMode/InitialCameraFramingTests.cs",
            ["428a38f2ef5843048cdc8fa6567edbc5"] = "Tests/PlayMode/SceneCompositionTestScope.cs",
        };

        /// <summary>The retired type names. None may exist in an IdleFactory assembly or be named by a source file under Assets/.</summary>
        private static readonly string[] RetiredTypes =
        {
            "IdleFactory.Core.Application.FactorySession",
            "IdleFactory.Core.Application.PlacementCommands",
            "IdleFactory.Core.Application.ProductionCommands",
            "IdleFactory.Core.Application.PersistenceCommands",
            "IdleFactory.Core.Application.DevelopmentSessionFactory",
            "IdleFactory.Core.Application.PersistenceOperationResult",
            "IdleFactory.Core.Application.IBuildingInstanceIdSource",
            "IdleFactory.Core.Application.PlacementPreview",
            "IdleFactory.Core.Application.SelectionResolution",
            "IdleFactory.Unity.Input.PlacementInputController",
            "IdleFactory.Unity.Presentation.PlacementPreviewView",
            "IdleFactory.Unity.Presentation.FactoryView",
            "IdleFactory.Unity.Presentation.FactoryViewRegistry",
            "IdleFactory.Unity.Bootstrap.ApplicationLifecycleAdapter",
            "IdleFactory.Unity.UI.MachineInspectorPresenter",
            "IdleFactory.Unity.UI.PersistencePresenter",
        };

        [Test]
        public void TheBootstrapHasExactlyOneGameplayContentInput()
        {
            const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var textAssets = typeof(CoreGameFoundationBootstrap).GetFields(Instance)
                .Where(field => typeof(TextAsset).IsAssignableFrom(field.FieldType))
                .Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.That(textAssets, Is.EqualTo(new[] { "gameplayContent", "legacyImportContent" }),
                "The bootstrap serializes the gameplay content and the legacy import catalog, nothing else.");

            // The gameplay content is read in exactly one place, and the legacy catalog never reaches it: it is read only by
            // the Save V1 import source.
            var source = ReadSource(BootstrapSourcePath);
            var contentReader = MethodBody(source, "private byte[] ReadContentBytes()");
            Assert.That(contentReader, Does.Contain("gameplayContent"));
            Assert.That(contentReader, Does.Not.Contain("legacyImportContent"));
            Assert.That(Regex.Matches(source, @"\bgameplayContent\b").Count, Is.EqualTo(1 + Regex.Matches(contentReader, @"\bgameplayContent\b").Count),
                "Only the field declaration and ReadContentBytes name the gameplay content.");
            var legacyReader = MethodBody(source, "private byte[] ReadLegacyImportBytes()");
            Assert.That(Regex.Matches(source, @"\blegacyImportContent\b").Count, Is.EqualTo(1 + Regex.Matches(legacyReader, @"\blegacyImportContent\b").Count),
                "Only the field declaration and ReadLegacyImportBytes name the legacy import catalog.");
            Assert.That(Regex.Matches(source, @"(?<!\.)\bReadContentBytes\(\)").Count, Is.EqualTo(2),
                "ReadContentBytes is declared once and called once (the Editor override delegate aside).");
            Assert.That(source, Does.Contain("GameplayContentDispatcher.Load(contentBytes"),
                "The gameplay content goes through the version dispatcher.");

            // One Editor composition seam remains: the Feature 003 run override. The Feature 001 override is retired.
            var seams = typeof(CoreGameFoundationBootstrap)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .Where(method => method.Name.StartsWith("Override", StringComparison.Ordinal))
                .Select(method => method.Name)
                .ToArray();
            Assert.That(seams, Is.EqualTo(new[] { "OverrideRunCompositionForTests" }));

            // The authored scene assigns a version 3 document as the gameplay content.
            var bootstrapBlock = ReadSource(ScenePath);
            var match = Regex.Match(bootstrapBlock, @"\n  gameplayContent: \{fileID: \d+, guid: ([0-9a-f]{32}), type: 3\}");
            Assert.That(match.Success, Is.True, "The authored scene assigns the gameplay content.");
            var contentPath = AssetDatabase.GUIDToAssetPath(match.Groups[1].Value);
            var content = AssetDatabase.LoadAssetAtPath<TextAsset>(contentPath);
            Assert.That(content, Is.Not.Null, contentPath);
            var loaded = GameplayContentDispatcher.Load(content.bytes);
            Assert.That(loaded.SchemaVersion, Is.EqualTo(3), contentPath);
            Assert.That(loaded.IsSuccess, Is.True, contentPath);
        }

        [Test]
        public void TheBootstrapNeverReferencesTheFeature001CheckpointWriter()
        {
            foreach (var path in Directory.GetFiles(BootstrapDirectory, "*.cs", SearchOption.AllDirectories))
            {
                var source = ReadSource(path);
                Assert.That(source, Does.Not.Contain(nameof(JsonCheckpointStore)), path);
                Assert.That(Regex.IsMatch(source, @"\bICheckpointStore\b"), Is.False, path + " must not name the Save V1 store port.");
            }

            const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var bootstrapTypes = typeof(CoreGameFoundationBootstrap).Assembly.GetTypes()
                .Where(type => type.Namespace == typeof(CoreGameFoundationBootstrap).Namespace);
            foreach (var type in bootstrapTypes)
            {
                foreach (var field in type.GetFields(All))
                {
                    Assert.That(IsSaveV1Writer(field.FieldType), Is.False, type.Name + "." + field.Name);
                }

                foreach (var method in type.GetMethods(All).Where(method => method.DeclaringType == type))
                {
                    Assert.That(IsSaveV1Writer(method.ReturnType), Is.False, type.Name + "." + method.Name);
                    Assert.That(method.GetParameters().Any(parameter => IsSaveV1Writer(parameter.ParameterType)), Is.False,
                        type.Name + "." + method.Name);
                }
            }
        }

        [Test]
        public void NoAssetReferencesARetiredGuid()
        {
            var offenders = new List<string>();
            foreach (var path in Directory.GetFiles("Assets", "*", SearchOption.AllDirectories))
            {
                var normalized = path.Replace('\\', '/');
                if (normalized == ThisTestPath || !IsTextAsset(normalized))
                {
                    continue;
                }

                var text = File.ReadAllText(path);
                foreach (var guid in RetiredGuids.Keys)
                {
                    if (text.IndexOf(guid, StringComparison.Ordinal) >= 0)
                    {
                        offenders.Add(normalized + " -> " + RetiredGuids[guid]);
                    }
                }
            }

            Assert.That(offenders, Is.Empty);
            foreach (var pair in RetiredGuids)
            {
                // A Library/ that still knew the retired asset returns the deleted path; only an existing asset is an offender.
                var resolvedPath = AssetDatabase.GUIDToAssetPath(pair.Key);
                Assert.That(
                    string.IsNullOrEmpty(resolvedPath) || !(AssetDatabase.AssetPathExists(resolvedPath) || File.Exists(resolvedPath)),
                    Is.True,
                    pair.Value + " still resolves to existing asset " + resolvedPath);
                Assert.That(File.Exists("Assets/IdleFactory/" + pair.Value), Is.False, pair.Value);
                Assert.That(File.Exists("Assets/IdleFactory/" + pair.Value + ".meta"), Is.False, pair.Value + ".meta");
            }
        }

        [Test]
        public void TheRetiredTypesAreAbsent()
        {
            var assemblies = new[]
            {
                typeof(IdleFactory.Core.Application.Checkpoint).Assembly,
                typeof(CoreGameFoundationBootstrap).Assembly,
                typeof(IdleFactory.Editor.PlatformBuildCommands).Assembly,
            };
            Assert.That(assemblies.Select(assembly => assembly.GetName().Name),
                Is.EqualTo(new[] { "IdleFactory.Core", "IdleFactory.Unity", "IdleFactory.Editor" }));

            foreach (var name in RetiredTypes)
            {
                Assert.That(assemblies.Select(assembly => assembly.GetType(name, false)).Where(type => type != null), Is.Empty, name);
            }

            // No source under Assets/ declares or uses a retired type (comments in this inventory test excepted).
            var simpleNames = RetiredTypes.Select(name => name.Substring(name.LastIndexOf('.') + 1)).ToArray();
            var offenders = new List<string>();
            foreach (var path in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories))
            {
                var normalized = path.Replace('\\', '/');
                if (normalized == ThisTestPath)
                {
                    continue;
                }

                var code = StripComments(ReadSource(path));
                foreach (var simple in simpleNames)
                {
                    if (Regex.IsMatch(code, @"\b" + simple + @"\b"))
                    {
                        offenders.Add(normalized + " -> " + simple);
                    }
                }
            }

            Assert.That(offenders, Is.Empty);
        }

        private static bool IsSaveV1Writer(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (type.IsArray || type.IsByRef)
            {
                return IsSaveV1Writer(type.GetElementType());
            }

            if (type == typeof(JsonCheckpointStore) || type == typeof(IdleFactory.Core.Application.ICheckpointStore))
            {
                return true;
            }

            return type.IsGenericType && type.GetGenericArguments().Any(IsSaveV1Writer);
        }

        private static bool IsTextAsset(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".cs":
                case ".meta":
                case ".unity":
                case ".prefab":
                case ".asset":
                case ".asmdef":
                case ".asmref":
                case ".uxml":
                case ".uss":
                case ".tss":
                case ".json":
                case ".mat":
                case ".controller":
                case ".anim":
                case ".inputactions":
                case ".txt":
                case ".md":
                    return true;
                default:
                    return false;
            }
        }

        private static string ReadSource(string path) => File.ReadAllText(path).Replace("\r\n", "\n");

        /// <summary>The text between the declaration's opening brace and its matching closing brace.</summary>
        private static string MethodBody(string source, string declaration)
        {
            var start = source.IndexOf(declaration, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), declaration);
            Assert.That(source.IndexOf(declaration, start + 1, StringComparison.Ordinal), Is.LessThan(0), declaration + " is declared once.");
            var open = source.IndexOf('{', start);
            var depth = 0;
            for (var i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                if (source[i] == '}' && --depth == 0)
                {
                    return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail(declaration + " has no body.");
            return string.Empty;
        }

        private static string StripComments(string code)
        {
            code = Regex.Replace(code, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(code, @"//[^\n]*", string.Empty);
        }
    }
}
