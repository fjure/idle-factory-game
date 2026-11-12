using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using IdleFactory.Domain;
using IdleFactory.Unity.Bootstrap;
using IdleFactory.Unity.Content;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// Proves the packaged JSON document is the only gameplay-content authority (FR-019, FR-020, SC-006):
    /// no ScriptableObject gameplay definition type remains, the bootstrap has a single content input,
    /// every JSON presentation key resolves in the scene sprite catalog, and no retired asset or script
    /// GUID is referenced anywhere under Assets/.
    /// </summary>
    public sealed class JsonContentAuthorityTests
    {
        private const string ContentPath = "Assets/IdleFactory/Content/Development/development-catalog.json";
        private const string ScenePath = "Assets/IdleFactory/Scenes/CoreGameFoundation.unity";

        // GUIDs of the six retired ScriptableObject scripts and the six retired development .asset files.
        private static readonly string[] RetiredGuids =
        {
            "52d54424f4c14e7da9e30fe45c01b1d4", // ResourceDefinitionAsset.cs
            "8bd31c8757694789b60d26fd1dfb72fa", // RecipeDefinitionAsset.cs
            "d6de19d8c3aa4ef592a942eab712d9b1", // BuildingDefinitionAsset.cs
            "8c592377089943d3bef4243d9a6c7aa0", // OfflinePolicyAsset.cs
            "c81cab8aaf784006b5d157e271b26a18", // DefinitionCatalogAsset.cs
            "f0884821496e43c192ccde59029137f8", // DefinitionCatalogBuilder.cs
            "90ae5b99d5a354b4ba2873e82928d729", // Resources/dev.input.asset
            "a3b72958fbe6e5b49a08bc96fce209e0", // Resources/dev.output.asset
            "480b4c15d1c61f748b75844b26e69c73", // Recipes/D001.asset
            "9324d1cd652f6014d972148aaf48486c", // Buildings/D001.asset
            "9296196dfa988d848b719c5795cc278e", // OfflinePolicies/revision-1.asset
            "75558a7ce085ccc4f8a4e5e4be83a055", // DevelopmentCatalog.asset
        };

        private static readonly string[] RetiredTypeNames =
        {
            "ResourceDefinitionAsset",
            "RecipeDefinitionAsset",
            "BuildingDefinitionAsset",
            "OfflinePolicyAsset",
            "DefinitionCatalogAsset",
            "DefinitionCatalogBuilder",
        };

        private const BindingFlags DeclaredInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        [Test]
        public void ContentNamespaceHasNoScriptableObjectGameplayTypes()
        {
            var assembly = typeof(GameplayContentLoader).Assembly;
            var scriptableTypes = assembly.GetTypes()
                .Where(type => type.Namespace == "IdleFactory.Unity.Content")
                .Where(type => typeof(ScriptableObject).IsAssignableFrom(type))
                .Select(type => type.FullName)
                .ToArray();

            Assert.That(scriptableTypes, Is.Empty, "Gameplay content must not be authored as ScriptableObjects.");

            var retiredTypes = assembly.GetTypes()
                .Where(type => RetiredTypeNames.Contains(type.Name))
                .Select(type => type.FullName)
                .ToArray();
            Assert.That(retiredTypes, Is.Empty);
        }

        // T512 (Feature 006, R22): presentation tuning assets are not gameplay content (FR-001 holds). WorldCompositionSettings holds the world
        // layout margin, the depot and deposit cell rules and prop weights and density; it reaches no simulation, economy or save data. Each
        // type listed here must live in the presentation namespace (checked below), so a gameplay definition cannot hide behind this list.
        private static readonly Type[] PresentationTuningAssets = { typeof(IdleFactory.Unity.Presentation.WorldCompositionSettings) };

        [Test]
        public void NoScriptableObjectInTheUnityAssemblyCarriesSerializedGameplayFields()
        {
            Assert.That(PresentationTuningAssets.Select(type => type.Namespace), Is.All.EqualTo("IdleFactory.Unity.Presentation"),
                "only presentation tuning assets may be exempt");
            var offenders = typeof(GameplayContentLoader).Assembly.GetTypes()
                .Where(type => typeof(ScriptableObject).IsAssignableFrom(type) && !PresentationTuningAssets.Contains(type))
                .SelectMany(type => type.GetFields(DeclaredInstance))
                .Where(field => field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                .Select(field => field.DeclaringType.FullName + "." + field.Name)
                .ToArray();

            Assert.That(offenders, Is.Empty);
        }

        [Test]
        public void BootstrapHasExactlyOneGameplayContentInputTheSerializedTextAsset()
        {
            var serialized = typeof(CoreGameFoundationBootstrap).GetFields(DeclaredInstance)
                .Where(field => field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                .ToArray();

            var textAssets = serialized.Where(field => field.FieldType == typeof(TextAsset)).ToArray();
            // Feature 003 (T110): the version 2 document is the one gameplay content input; the version 1 development
            // catalog is packaged only as the legacy import catalog, which the bootstrap reads when a Save V1 import needs it.
            Assert.That(textAssets.Select(field => field.Name), Is.EqualTo(new[] { "gameplayContent", "legacyImportContent" }));

            var gameplayInputs = serialized
                .Where(field => IsScriptableObjectOrCatalog(field.FieldType))
                .Select(field => field.Name)
                .ToArray();
            Assert.That(gameplayInputs, Is.Empty);
        }

        [Test]
        public void EveryJsonPresentationKeyResolvesInTheSceneSpriteCatalog()
        {
            var result = GameplayContentLoader.Load(File.ReadAllBytes(ContentPath));
            Assert.That(result.IsSuccess, Is.True);

            var keys = result.Catalog.Resources.Select(resource => resource.Value.PresentationKey)
                .Concat(result.Catalog.Buildings.Select(building => building.Value.PresentationKey))
                .Where(key => key != null)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Assert.That(keys, Is.Not.Empty);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var bootstrap = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<CoreGameFoundationBootstrap>(true))
                    .Single();
                var sprites = bootstrap.SpriteCatalog;
                Assert.That(sprites.Validate(), Is.True, sprites.Error);

                foreach (var key in keys)
                {
                    Assert.That(sprites.TryGet(key, out var sprite) && sprite != null, Is.True,
                        $"presentationKey '{key}' does not resolve in the scene F001SpriteCatalog");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void NoRetiredGuidIsReferencedUnderAssets()
        {
            var extensions = new[] { ".unity", ".prefab", ".asset", ".meta", ".mat", ".controller", ".anim", ".asmdef", ".asmref" };
            var pattern = new Regex(string.Join("|", RetiredGuids), RegexOptions.Compiled);
            var offenders = Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories)
                .Where(path => extensions.Contains(Path.GetExtension(path)))
                .Where(path => !path.EndsWith("JsonContentAuthorityTests.cs", StringComparison.Ordinal))
                .Where(path => pattern.IsMatch(File.ReadAllText(path)))
                .ToArray();

            Assert.That(offenders, Is.Empty, "Retired ScriptableObject GUIDs are still referenced.");
        }

        private static bool IsScriptableObjectOrCatalog(Type type)
        {
            // Engine presentation assets (InputActionAsset, VisualTreeAsset, StyleSheet) are ScriptableObjects
            // but carry no gameplay content; only project-defined ScriptableObjects and catalogs count.
            var isProjectType = type.Namespace != null && type.Namespace.StartsWith("IdleFactory", StringComparison.Ordinal);
            if (isProjectType &&
                (typeof(ScriptableObject).IsAssignableFrom(type) || typeof(DefinitionCatalog).IsAssignableFrom(type)))
            {
                return true;
            }

            var element = type.IsArray ? type.GetElementType() : null;
            return (element != null && IsScriptableObjectOrCatalog(element)) ||
                   (type.IsGenericType && type.GetGenericArguments().Any(IsScriptableObjectOrCatalog));
        }
    }
}
