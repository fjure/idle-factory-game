using System;
using UnityEngine;

namespace IdleFactory.Unity.Presentation.Look
{
    /// <summary>How a profile's sprites are sampled (data-model LookProfile.filtering).</summary>
    public enum LookFiltering
    {
        Point = 0,
        Bilinear = 1,
    }

    /// <summary>The camera model of a profile. In M2 every profile uses <see cref="IntegerScale"/>; <see cref="Continuous"/> may come at M4 (R15).</summary>
    public enum LookCameraPolicy
    {
        IntegerScale = 0,
        Continuous = 1,
    }

    /// <summary>The world renderer a profile names. <see cref="Universal"/> (C1) is a prototype-only option in M2 and needs the dev-only second URP asset (rule 9).</summary>
    public enum LookWorldRenderer
    {
        Renderer2D = 0,
        Universal = 1,
    }

    /// <summary>
    /// A look profile (contracts/look-profile.md, data-model LookProfile): which art set, indicator family, environment and camera policy the world
    /// presentation uses. It is presentation data only. It holds no reference to the simulation and cannot change simulation inputs, outputs, timing,
    /// save data or content; the world views draw the same authoritative state under every profile. The shipped default is <see cref="F006Id"/>; the
    /// prototype profiles are referenced only from a dev-only scene (ARCH-1) and never from a release build.
    /// </summary>
    public sealed class LookProfile : ScriptableObject
    {
        public const string F006Id = "F006";
        public const string PrototypeAId = "F007-A";
        public const string PrototypeBId = "F007-B";
        public const string PrototypeCId = "F007-C";
        public const string ProductionId = "F007";

        /// <summary>The ids a profile may carry (data-model LookProfile.id).</summary>
        public static readonly string[] KnownIds = { F006Id, PrototypeAId, PrototypeBId, PrototypeCId, ProductionId };

        [SerializeField] private string id = F006Id;
        [SerializeField] private bool isPrototype;

        [Tooltip("F006 only: the sprites come from the catalog that the bootstrap scene serializes today. Every other profile carries its own catalog.")]
        [SerializeField] private bool usesShippedSceneCatalog;
        [SerializeField] private PresentationSpriteCatalog spriteCatalog = new PresentationSpriteCatalog();
        [SerializeField] private LookProfileRequirementTable requirementTable = LookProfileRequirementTable.F006();
        [Tooltip("Only profile C: the optional mesh prefab set.")]
        [SerializeField] private GameObject[] meshSet;
        [SerializeField] private LookWorldRenderer worldRenderer;
        [Tooltip("The dev-only second URP renderer asset of a prototype that names the Universal renderer. Null for the 2D renderer.")]
        [SerializeField] private ScriptableObject worldRendererAsset;
        [Min(1)] [SerializeField] private int pixelsPerUnit = 24;
        [SerializeField] private LookFiltering filtering;
        [SerializeField] private LookCameraPolicy cameraPolicy;
        [SerializeField] private string environmentFamily = "f006-deck";
        [SerializeField] private string indicatorSet = "f006-badges";
        [SerializeField] private string lightingMood = string.Empty;

        public string Id => id;

        public bool IsPrototype => isPrototype;

        /// <summary>True for F006: the bootstrap supplies the scene's serialized catalog.</summary>
        public bool UsesShippedSceneCatalog => usesShippedSceneCatalog;

        public PresentationSpriteCatalog SpriteCatalog => spriteCatalog;

        public LookProfileRequirementTable RequirementTable => requirementTable;

        public GameObject[] MeshSet => meshSet;

        public LookWorldRenderer WorldRenderer => worldRenderer;

        public ScriptableObject WorldRendererAsset => worldRendererAsset;

        /// <summary>Art pixels per world unit: 24 for F006 (one world unit is 24 F006 art pixels, ARCH-8).</summary>
        public int PixelsPerUnit => pixelsPerUnit;

        public LookFiltering Filtering => filtering;

        public LookCameraPolicy CameraPolicy => cameraPolicy;

        public string EnvironmentFamily => environmentFamily;

        public string IndicatorSet => indicatorSet;

        public string LightingMood => lightingMood;

        public static bool IsKnownId(string candidate) => Array.IndexOf(KnownIds, candidate) >= 0;

        /// <summary>
        /// The F006 profile exactly as <c>Assets/IdleFactory/Art/F007/Profiles/F006.asset</c> stores it: the scene's catalog, the static registry as its
        /// requirement table, 24 pixels per unit, point filtering, the integer camera and the 2D renderer. It reproduces today's look, so nothing about
        /// it is new. The bootstrap creates it in memory when the scene names no profile asset.
        /// </summary>
        public static LookProfile CreateF006()
        {
            var profile = CreateInstance<LookProfile>();
            profile.name = F006Id;
            profile.usesShippedSceneCatalog = true;
            return profile;
        }

        /// <summary>A profile with its own catalog and table (a prototype, or a test profile). Fields not passed keep the F006 values.</summary>
        public static LookProfile Create(
            string profileId, bool prototype, PresentationSpriteCatalog catalog, LookProfileRequirementTable table, int unitsPerPixel = 24)
        {
            var profile = CreateInstance<LookProfile>();
            profile.name = profileId;
            profile.id = profileId;
            profile.isPrototype = prototype;
            profile.usesShippedSceneCatalog = false;
            profile.spriteCatalog = catalog ?? new PresentationSpriteCatalog();
            profile.requirementTable = table ?? LookProfileRequirementTable.F006();
            profile.pixelsPerUnit = unitsPerPixel;
            return profile;
        }

        /// <summary>Test and tooling hook for the fields that the factory methods do not take.</summary>
        public void ConfigureRendering(LookWorldRenderer renderer, LookCameraPolicy camera, LookFiltering filter)
        {
            worldRenderer = renderer;
            cameraPolicy = camera;
            filtering = filter;
        }

        /// <summary>Checks the fields that need no catalog. Returns the first problem, or null when the profile is sound.</summary>
        public string FirstProblem()
        {
            if (!IsKnownId(id))
            {
                return "unknown profile id";
            }

            if (pixelsPerUnit < 1)
            {
                return "pixelsPerUnit must be at least 1";
            }

            if (usesShippedSceneCatalog && (isPrototype || id != F006Id))
            {
                return "only the shipped F006 profile may use the scene catalog";
            }

            if (!usesShippedSceneCatalog && spriteCatalog == null)
            {
                return "no sprite catalog";
            }

            if (requirementTable == null)
            {
                return "no requirement table";
            }

            var tableProblem = requirementTable.FirstProblem();
            if (tableProblem != null)
            {
                return tableProblem;
            }

            if (cameraPolicy == LookCameraPolicy.Continuous)
            {
                return "the Continuous camera policy is not allowed before M4";
            }

            if (worldRenderer == LookWorldRenderer.Universal && !isPrototype)
            {
                return "only a prototype profile may name the Universal renderer";
            }

            if (meshSet != null && meshSet.Length > 0 && id != PrototypeCId)
            {
                return "only profile F007-C has a mesh set";
            }

            return null;
        }
    }
}
