using System;
using System.Collections.Generic;
using System.Linq;
using IdleFactory.Core.Application;
using IdleFactory.Domain;
using UnityEngine;

namespace IdleFactory.Unity.Presentation
{
    public sealed class FactoryView : MonoBehaviour
    {
        public const int GridSortingOrder = 0;
        public const int BoundarySortingOrder = 10;
        public const int MachineSortingOrder = 100;
        public const int PreviewSortingOrder = 200;
        public const int BadgeSortingOrder = 300;

        private readonly Dictionary<BuildingInstanceId, BuildingRender> buildingRenders =
            new Dictionary<BuildingInstanceId, BuildingRender>();
        private PlacementCommands commands;
        private DefinitionCatalog definitions;
        private F001SpriteCatalog sprites;
        private GridWorldMapper mapper;
        private Transform contentRoot;
        private BuildingInstanceId? selectedInstanceId;
        private MachinePresentationState[] presentedStates = Array.Empty<MachinePresentationState>();
        private long syncedRevision = -1;

        public event Action<SelectionResolution> SelectionChanged;
        public event Action<string> ErrorChanged;

        public BuildingInstanceId? SelectedInstanceId => selectedInstanceId;
        public int RenderedBuildingCount => buildingRenders.Count;
        public long RenderedRevision { get; private set; } = -1;
        public string Error { get; private set; } = string.Empty;

        public void Configure(
            PlacementCommands placementCommands,
            DefinitionCatalog definitionCatalog,
            F001SpriteCatalog spriteCatalog,
            GridWorldMapper gridMapper)
        {
            commands = placementCommands ?? throw new ArgumentNullException(nameof(placementCommands));
            definitions = definitionCatalog ?? throw new ArgumentNullException(nameof(definitionCatalog));
            sprites = spriteCatalog ?? throw new ArgumentNullException(nameof(spriteCatalog));
            mapper = gridMapper ?? throw new ArgumentNullException(nameof(gridMapper));
            EnsureRoot();
            if (!sprites.Validate())
            {
                SetError(sprites.Error);
                ClearRenderedContent();
                return;
            }

            SetError(string.Empty);
            Refresh();
        }

        public void Refresh()
        {
            if (commands == null || definitions == null || sprites == null || mapper == null || !sprites.IsValid)
            {
                return;
            }

            var snapshot = commands.GetFactorySnapshot();
            Rebuild(snapshot);
            ResolveSelection();
        }

        public void SelectAt(GridPosition cell)
        {
            var snapshot = commands?.GetFactorySnapshot();
            if (snapshot == null)
            {
                return;
            }

            selectedInstanceId = snapshot.Occupancy.TryGetValue(cell, out var instanceId)
                ? instanceId
                : (BuildingInstanceId?)null;
            Rebuild(snapshot);
            ResolveSelection();
        }

        public void Select(BuildingInstanceId? instanceId)
        {
            selectedInstanceId = instanceId;
            Refresh();
        }

        /// <summary>
        /// Brings every rendered state badge and the selection summary up to date with simulation-driven state
        /// changes (T062). Runs after the runtime host advanced the session this frame. It returns without work
        /// while the revision is unchanged, reads the presented states into a reused buffer, and only swaps the
        /// sprite of a badge whose presented state changed, so a steady factory neither rebuilds nor allocates.
        /// Structural changes (placement, restore, recovery) keep their explicit <see cref="Refresh"/> owners.
        /// </summary>
        /// <returns>True when at least one presented machine state changed.</returns>
        public bool SyncPresentedStates()
        {
            if (commands == null || sprites == null || !sprites.IsValid || contentRoot == null)
            {
                return false;
            }

            var revision = commands.CurrentRevision;
            if (revision == syncedRevision)
            {
                return false;
            }

            var count = commands.CopyMachinePresentationStates(presentedStates);
            if (count > presentedStates.Length)
            {
                presentedStates = new MachinePresentationState[count];
                count = commands.CopyMachinePresentationStates(presentedStates);
            }

            if (count != buildingRenders.Count)
            {
                return false;
            }

            for (var index = 0; index < count; index++)
            {
                if (!buildingRenders.ContainsKey(presentedStates[index].InstanceId))
                {
                    return false;
                }
            }

            syncedRevision = revision;
            var changed = false;
            var selectedChanged = false;
            for (var index = 0; index < count; index++)
            {
                var state = presentedStates[index];
                var render = buildingRenders[state.InstanceId];
                if (render.PresentedState.Equals(state))
                {
                    continue;
                }

                render.PresentedState = state;
                changed = true;
                if (render.StateBadge != null)
                {
                    var key = StateBadgeKey(state.State);
                    render.StateBadge.sprite = sprites.Get(key);
                    render.StateBadge.gameObject.name = key;
                }

                if (selectedInstanceId.HasValue && selectedInstanceId.Value == state.InstanceId)
                {
                    selectedChanged = true;
                }
            }

            if (selectedChanged)
            {
                SelectionChanged?.Invoke(commands.ResolveSelection(selectedInstanceId));
            }

            return changed;
        }

        private void LateUpdate()
        {
            SyncPresentedStates();
        }

        public SpriteRenderer GetMachineRenderer(BuildingInstanceId instanceId) =>
            buildingRenders.TryGetValue(instanceId, out var render) ? render.Machine : null;

        public IReadOnlyList<SpriteRenderer> GetBadgeRenderers(BuildingInstanceId instanceId) =>
            buildingRenders.TryGetValue(instanceId, out var render)
                ? render.Badges
                : Array.Empty<SpriteRenderer>();

        private void Rebuild(FactorySnapshot snapshot)
        {
            ClearRenderedContent();
            EnsureRoot();
            RenderGrid(snapshot.Bounds);

            foreach (var building in snapshot.Buildings.OrderBy(item => item.InstanceId))
            {
                if (!definitions.Buildings.TryGetValue(building.DefinitionId, out var definition))
                {
                    SetError($"Missing definition '{building.DefinitionId.Value}' for rendered building.");
                    continue;
                }

                var selected = selectedInstanceId.HasValue && selectedInstanceId.Value == building.InstanceId;
                var machineKey = MachineKey(definition, selected);
                if (!sprites.TryGet(machineKey, out var machineSprite))
                {
                    SetError($"Missing presentation mapping '{machineKey}'.");
                    continue;
                }

                var owner = new GameObject($"building-{building.InstanceId.Value}").transform;
                owner.SetParent(contentRoot, false);
                var machine = AddSprite(owner, "machine", machineSprite, MachineSortingOrder);
                PlacementPreviewView.ApplyFootprintTransform(
                    machine.transform,
                    mapper,
                    definition.Footprint,
                    building.Anchor,
                    building.OrientationQuarterTurns);
                var badges = new List<SpriteRenderer>();
                var render = new BuildingRender(machine, badges);
                var inspection = commands.ResolveSelection(building.InstanceId);
                if (inspection.Kind == SelectionResolutionKind.Selected)
                {
                    render.PresentedState = MachinePresentationState.FromInspection(inspection.Inspection);
                    render.StateBadge = AddBadge(owner, StateBadgeKey(render.PresentedState.State), 0, building, definition);
                    badges.Add(render.StateBadge);
                    if (selected)
                    {
                        badges.Add(AddBadge(owner, F001SpriteCatalog.BadgeSelected, 1, building, definition));
                    }
                }

                buildingRenders.Add(building.InstanceId, render);
            }

            RenderedRevision = snapshot.Revision;
            syncedRevision = snapshot.Revision;
        }

        private void RenderGrid(FactoryBounds bounds)
        {
            for (var y = 0; y < bounds.Height; y++)
            {
                for (var x = 0; x < bounds.Width; x++)
                {
                    var cell = bounds.Origin.Offset(x, y);
                    var tile = AddSprite(contentRoot, $"grid-{cell.X}-{cell.Y}", sprites.Get(F001SpriteCatalog.GridTile), GridSortingOrder);
                    tile.transform.position = mapper.CellCenter(cell);
                }
            }

            for (var x = 0; x < bounds.Width; x++)
            {
                AddBoundary(bounds.Origin.Offset(x, bounds.Height - 1), 0f);
                AddBoundary(bounds.Origin.Offset(x, 0), 180f);
            }

            for (var y = 0; y < bounds.Height; y++)
            {
                AddBoundary(bounds.Origin.Offset(bounds.Width - 1, y), -90f);
                AddBoundary(bounds.Origin.Offset(0, y), 90f);
            }

            AddCorner(bounds.Origin.Offset(bounds.Width - 1, bounds.Height - 1), 0f);
            AddCorner(bounds.Origin.Offset(bounds.Width - 1, 0), -90f);
            AddCorner(bounds.Origin.Offset(0, 0), 180f);
            AddCorner(bounds.Origin.Offset(0, bounds.Height - 1), 90f);
        }

        private void AddBoundary(GridPosition cell, float rotation)
        {
            var renderer = AddSprite(contentRoot, "boundary-edge", sprites.Get(F001SpriteCatalog.BoundaryEdge), BoundarySortingOrder);
            renderer.transform.position = mapper.CellCenter(cell);
            renderer.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void AddCorner(GridPosition cell, float rotation)
        {
            var renderer = AddSprite(contentRoot, "boundary-corner", sprites.Get(F001SpriteCatalog.BoundaryCorner), BoundarySortingOrder);
            renderer.transform.position = mapper.CellCenter(cell);
            renderer.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private SpriteRenderer AddBadge(
            Transform owner,
            string key,
            int row,
            BuildingSnapshot building,
            BuildingDefinition definition)
        {
            var renderer = AddSprite(owner, key, sprites.Get(key), BadgeSortingOrder);
            var size = PlacementPreviewView.OrientedFootprintSize(
                definition.Footprint,
                building.OrientationQuarterTurns);
            var origin = mapper.CellOrigin(building.Anchor);
            renderer.transform.position = origin + new Vector2(
                size.x * mapper.CellSize * 0.5f,
                (size.y - 0.25f + row * 0.55f) * mapper.CellSize);
            return renderer;
        }

        private static SpriteRenderer AddSprite(Transform parent, string name, Sprite sprite, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void ResolveSelection()
        {
            var resolution = commands.ResolveSelection(selectedInstanceId);
            if (resolution.Kind == SelectionResolutionKind.Missing)
            {
                selectedInstanceId = null;
                Rebuild(commands.GetFactorySnapshot());
            }

            SelectionChanged?.Invoke(resolution);
        }

        private void EnsureRoot()
        {
            if (contentRoot != null)
            {
                return;
            }

            contentRoot = new GameObject("factory-content").transform;
            contentRoot.SetParent(transform, false);
        }

        private void ClearRenderedContent()
        {
            buildingRenders.Clear();
            if (contentRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(contentRoot.gameObject);
            }
            else
            {
                DestroyImmediate(contentRoot.gameObject);
            }

            contentRoot = null;
        }

        private void SetError(string error)
        {
            Error = error ?? string.Empty;
            ErrorChanged?.Invoke(Error);
        }

        private static string MachineKey(BuildingDefinition definition, bool selected)
        {
            if (definition.PresentationKey == F001SpriteCatalog.D001Normal)
            {
                return selected ? F001SpriteCatalog.D001Selected : F001SpriteCatalog.D001Normal;
            }

            if (definition.PresentationKey == F001SpriteCatalog.AlternateNormal)
            {
                return selected ? F001SpriteCatalog.AlternateSelected : F001SpriteCatalog.AlternateNormal;
            }

            return null;
        }

        private static string StateBadgeKey(MachinePresentedState state)
        {
            switch (state)
            {
                case MachinePresentedState.Inactive:
                    return F001SpriteCatalog.BadgeInactive;
                case MachinePresentedState.MissingInput:
                    return F001SpriteCatalog.BadgeMissingInput;
                case MachinePresentedState.FullOutput:
                    return F001SpriteCatalog.BadgeFullOutput;
                case MachinePresentedState.Producing:
                    return F001SpriteCatalog.BadgeProducing;
                default:
                    return F001SpriteCatalog.BadgeNormal;
            }
        }

        private sealed class BuildingRender
        {
            public BuildingRender(SpriteRenderer machine, IReadOnlyList<SpriteRenderer> badges)
            {
                Machine = machine;
                Badges = badges;
            }

            public SpriteRenderer Machine { get; }
            public IReadOnlyList<SpriteRenderer> Badges { get; }
            public SpriteRenderer StateBadge { get; set; }
            public MachinePresentationState PresentedState { get; set; }
        }
    }
}
