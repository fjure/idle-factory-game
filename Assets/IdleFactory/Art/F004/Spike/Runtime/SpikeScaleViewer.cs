using System.Collections.Generic;
using IdleFactory.Unity.Presentation;
using UnityEngine;

namespace IdleFactory.Art.F004.Spike
{
    public enum SpikeProjection
    {
        Oblique34,
        Isometric21,
    }

    public enum SpikeViewMode
    {
        /// <summary>The smallest allowed integer scale for the screen (n_min).</summary>
        NMin,

        /// <summary>The largest integer scale at which all 12 slots fit (fit-all).</summary>
        FitAll,
    }

    /// <summary>
    /// Spike scene variant (T414): shows the imported D = 24 style-spike sprites on the 12-slot grid at the integer scale
    /// of <see cref="CameraScaleModel"/>, at n_min or fit-all, for the S1 evidence (T416). It owns the camera of its scene
    /// only and has no gameplay, save or production dependency. Remove it with the Spike path.
    /// </summary>
    public sealed class SpikeScaleViewer : MonoBehaviour
    {
        private const float RunFramesPerSecond = 8f;

        /// <summary>The sprite base name shown in each of the 12 slots in reading order; machines get an occupied plate behind them.</summary>
        private static readonly string[] SlotAssets =
        {
            "s1-miner-run", "s1-assembler-run", "s1-miner-warning", "s1-assembler-paused",
            "s1-miner-inactive", "s1-assembler-inactive", "s1-slot-empty", "s1-slot-selected",
            "s1-slot-locked", "s1-slot-occupied", "s1-res-ore", "s1-res-ingot",
        };

        [SerializeField] private Camera targetCamera;
        [SerializeField] private int artPixelsPerCell = 24;
        [SerializeField] private int readableCellPoints = 24;
        [SerializeField] private int maxScale = 6;
        [SerializeField] private SpikeProjection projection = SpikeProjection.Oblique34;
        [SerializeField] private SpikeViewMode viewMode = SpikeViewMode.FitAll;
        [SerializeField] private Sprite[] obliqueSprites = new Sprite[0];
        [SerializeField] private Sprite[] isometricSprites = new Sprite[0];

        private readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        private readonly List<string[]> frameNames = new List<string[]>();
        private CameraScaleModel model;
        private Dictionary<string, Sprite> lookup = new Dictionary<string, Sprite>();
        private int builtWidth;
        private int builtHeight;
        private SpikeProjection builtProjection;
        private SpikeViewMode builtMode;

        public CameraScaleModel Model => model;
        public SpikeProjection Projection => projection;
        public SpikeViewMode ViewMode => viewMode;
        public int RendererCount => renderers.Count;

        public void Configure(Camera camera, Sprite[] oblique, Sprite[] isometric)
        {
            targetCamera = camera;
            obliqueSprites = oblique;
            isometricSprites = isometric;
        }

        public void Show(SpikeProjection newProjection, SpikeViewMode newMode)
        {
            projection = newProjection;
            viewMode = newMode;
            Rebuild();
        }

        private void OnEnable() => Rebuild();

        private void Update()
        {
            if (builtWidth != Screen.width || builtHeight != Screen.height || builtProjection != projection || builtMode != viewMode)
            {
                Rebuild();
            }

            var frame = (int)(Time.unscaledTime * RunFramesPerSecond);
            for (var i = 0; i < frameNames.Count && i < renderers.Count; i++)
            {
                var names = frameNames[i];
                if (names.Length > 1 && lookup.TryGetValue(names[frame % names.Length], out var sprite))
                {
                    renderers[i].sprite = sprite;
                }
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(8, 8, 420, 120));
            GUILayout.Label($"S1 spike D={artPixelsPerCell} {projection} {viewMode} n={model?.Scale} (n_min={model?.LowerBound}, n_fit={model?.FitScale})");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Oblique 3/4")) { Show(SpikeProjection.Oblique34, viewMode); }
            if (GUILayout.Button("Isometric 2:1")) { Show(SpikeProjection.Isometric21, viewMode); }
            if (GUILayout.Button("n_min")) { Show(projection, SpikeViewMode.NMin); }
            if (GUILayout.Button("Fit-all")) { Show(projection, SpikeViewMode.FitAll); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void Rebuild()
        {
            if (targetCamera == null)
            {
                return;
            }

            builtWidth = Screen.width;
            builtHeight = Screen.height;
            builtProjection = projection;
            builtMode = viewMode;

            var set = projection == SpikeProjection.Oblique34 ? obliqueSprites : isometricSprites;
            lookup = new Dictionary<string, Sprite>(set.Length);
            foreach (var sprite in set)
            {
                if (sprite != null)
                {
                    lookup[sprite.name] = sprite;
                }
            }

            var dpi = Screen.dpi;
            var pixelsPerPoint = dpi > 0f ? dpi / 160f : 1f;
            var columns = SlotGridLayout.ColumnsFor(builtWidth, builtHeight);
            var layout = new ScaleLayout(builtWidth, builtHeight, pixelsPerPoint, new Rect(0f, 0f, builtWidth, builtHeight), columns);
            model = new CameraScaleModel(new ArtScaleSettings(artPixelsPerCell, readableCellPoints, maxScale), Debug.Log);
            model.Fit(layout);
            if (viewMode == SpikeViewMode.NMin && model.LowerBound < model.Scale)
            {
                model.SnapZoomTo(model.LowerBound, new Vector2(builtWidth * 0.5f, builtHeight * 0.5f));
            }

            targetCamera.orthographic = true;
            targetCamera.orthographicSize = CameraScaleModel.OrthographicSize(builtHeight, model.Scale, artPixelsPerCell);
            targetCamera.transform.position = new Vector3(model.Position.x, model.Position.y, -10f);
            BuildSlots(columns);
        }

        private void BuildSlots(int columns)
        {
            foreach (var r in renderers)
            {
                if (r != null)
                {
                    Destroy(r.gameObject);
                }
            }

            for (var c = transform.childCount - 1; c >= 0; c--)
            {
                Destroy(transform.GetChild(c).gameObject);
            }

            renderers.Clear();
            frameNames.Clear();
            for (var i = 0; i < SlotGridLayout.SlotCount; i++)
            {
                var centre = SlotGridLayout.SlotRect(i, columns).center;
                var baseName = SlotAssets[i];
                if (baseName.StartsWith("s1-miner") || baseName.StartsWith("s1-assembler"))
                {
                    Place("plate" + i, "s1-slot-occupied_00", centre, 0, null);
                }

                var frames = new List<string>();
                for (var f = 0; lookup.ContainsKey($"{baseName}_{f:00}"); f++)
                {
                    frames.Add($"{baseName}_{f:00}");
                }

                if (frames.Count == 0)
                {
                    frames.Add(baseName + "_00");
                }

                Place("slot" + i, frames[0], centre, 1, frames.ToArray());
            }
        }

        private void Place(string objectName, string spriteName, Vector2 position, int order, string[] frames)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            if (lookup.TryGetValue(spriteName, out var sprite))
            {
                sr.sprite = sprite;
            }

            if (frames != null)
            {
                renderers.Add(sr);
                frameNames.Add(frames);
            }
        }
    }
}
