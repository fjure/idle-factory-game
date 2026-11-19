using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using IdleFactory.Run.Application;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;
using IdleFactory.Unity.Presentation;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace IdleFactory.Tests.PlayMode.F006
{
    /// <summary>
    /// W006-V (T531, research R16, Feature 006): the visual and presentation worst case of Feature 006, built on W004-V
    /// (<see cref="W004VFixture"/>: every machine state, both markers and the selection frame on the 12 occupied locations).
    /// On top of it the fixture holds, at the same time:
    /// <list type="bullet">
    /// <item>the Established-stage deck (stage 2: railing, wall and gantry edges, the whole margin ring lit, props);</item>
    /// <item>12 link branches, every one with a running pip (12 machines Running in a real <see cref="RunSession"/>);</item>
    /// <item>the ore vein with the chip loop playing (a mining hold);</item>
    /// <item>3 world effects (the active limit of the pool, completion pops at 3 locations);</item>
    /// <item>the card open state. T534 has merged, so the card is the REAL docked machine card of the shell, not a stub; it exists only in the
    /// real-scene cases (<c>W006VRealScenePlayerTests</c>, <c>W006VWorldPlayerTests</c>), because the synthetic world has no shell. In the shipped UI the
    /// ore-vein control is hidden while the card is open, so the real-scene cases measure "chip loop, card closed" and "card open, vein at rest" as
    /// two states of the same workload; the synthetic world keeps the chip loop on and draws everything else.</item>
    /// </list>
    /// The fixture touches no Core state: the forced W004-V visuals replace what the grid draws, the session only feeds the link layer.
    /// </summary>
    internal static class W006VFixture
    {
        public const string Name = "W006-V";

        /// <summary>The development stage of the fixture: Established (12 of 12 locations open).</summary>
        public const int Stage = 2;

        public const int Locations = 12;
        public const int BranchPips = 12;
        public const int Effects = 3;

        /// <summary>The location whose docked machine card is open in the real-scene cases.</summary>
        public const int CardSlot = 0;

        /// <summary>SHA-256 of <see cref="Canonical"/>: any change of the base, the stage, the counts or the card state changes it.</summary>
        public const string ExpectedHash = "9496a6b3e3821c71a1167296eba95c746deb75d5bc605ed4e2f72add24026ac7";

        public static string Canonical()
        {
            var b = new StringBuilder();
            b.Append(Name).Append("|base=").Append(W004VFixture.Name).Append(':').Append(W004VFixture.ExpectedHash);
            b.Append("|stage=").Append(Stage.ToString(CultureInfo.InvariantCulture));
            b.Append("|locations=").Append(Locations.ToString(CultureInfo.InvariantCulture));
            b.Append("|branches=").Append(BranchPips.ToString(CultureInfo.InvariantCulture)).Append("+pips-all-running");
            b.Append("|vein=chip-loop");
            b.Append("|effects=").Append(Effects.ToString(CultureInfo.InvariantCulture)).Append("*completion-pop");
            b.Append("|card=real-docked-machine-card@slot").Append(CardSlot.ToString(CultureInfo.InvariantCulture)).Append("(T534)");
            return b.ToString();
        }

        public static string Hash()
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Canonical()))).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }

    /// <summary>One count of the renderers a world camera draws (research R16, Art Bible 2.2a and 21.12): enabled sprite renderers with a sprite, plus the Tilemap renderers.</summary>
    internal sealed class RendererCensus
    {
        public int SpriteRenderers;
        public int TilemapRenderers;
        public int Tiles;

        /// <summary>Every sprite renderer the views own, enabled or not (the FX pool keeps 8, a pip renderer exists per location): the ceiling the enabled count can reach.</summary>
        public int AllocatedSprites;

        public int AllocatedTilemapRenderers;
        public int AllocatedTotal => AllocatedSprites + AllocatedTilemapRenderers;
        public readonly SortedDictionary<string, int> ByGroup = new SortedDictionary<string, int>(StringComparer.Ordinal);

        public int Total => SpriteRenderers + TilemapRenderers;

        public override string ToString()
        {
            return "allocatedRenderers=" + AllocatedTotal + " renderers=" + Total + " sprites=" + SpriteRenderers + " tilemapRenderers=" + TilemapRenderers + " tiles=" + Tiles + " groups=" +
                string.Join(",", ByGroup.Select(pair => pair.Key + ":" + pair.Value));
        }

        private static string GroupOf(Component component)
        {
            if (component.GetComponentInParent<EffectPoolView>() != null) return "fx";
            if (component.GetComponentInParent<DepositView>() != null) return "deposit";
            if (component.GetComponentInParent<DepotLinkView>() != null) return component is SpriteRenderer ? "pips-depot" : "links";
            if (component.GetComponentInParent<FactoryDeckView>() != null) return component is SpriteRenderer ? "edges-props" : "base-deck";
            if (component.GetComponentInParent<AttentionMarkerView>() != null) return "attention";
            if (component.GetComponentInParent<SlotGridView>() != null) return "slots";
            return "other";
        }

        /// <summary>The census of the given sprite and tilemap renderers (the world camera's: UI Toolkit is not in it).</summary>
        public static RendererCensus Of(IEnumerable<SpriteRenderer> sprites, IEnumerable<TilemapRenderer> tilemaps)
        {
            var census = new RendererCensus();
            foreach (var renderer in sprites)
            {
                census.AllocatedSprites += renderer != null ? 1 : 0;
                if (renderer == null || !renderer.enabled || renderer.sprite == null || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                census.SpriteRenderers++;
                var group = GroupOf(renderer);
                census.ByGroup[group] = (census.ByGroup.TryGetValue(group, out var count) ? count : 0) + 1;
            }

            foreach (var renderer in tilemaps)
            {
                census.AllocatedTilemapRenderers += renderer != null ? 1 : 0;
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var tilemap = renderer.GetComponent<Tilemap>();
                var tiles = tilemap == null ? 0 : DeckTestRig.TileCount(tilemap);
                if (tiles == 0)
                {
                    continue;
                }

                census.TilemapRenderers++;
                census.Tiles += tiles;
                var group = GroupOf(renderer);
                census.ByGroup[group] = (census.ByGroup.TryGetValue(group, out var count) ? count : 0) + 1;
            }

            return census;
        }

        /// <summary>The census of everything in the loaded scenes.</summary>
        public static RendererCensus OfLoadedScenes()
        {
            return Of(UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include),
                UnityEngine.Object.FindObjectsByType<TilemapRenderer>(FindObjectsInactive.Include));
        }

        /// <summary>The census of the hosts a synthetic world owns.</summary>
        public static RendererCensus OfHosts(IEnumerable<GameObject> hosts)
        {
            var sprites = new List<SpriteRenderer>();
            var tilemaps = new List<TilemapRenderer>();
            foreach (var host in hosts)
            {
                if (host == null) continue;
                sprites.AddRange(host.GetComponentsInChildren<SpriteRenderer>(true));
                tilemaps.AddRange(host.GetComponentsInChildren<TilemapRenderer>(true));
            }

            return Of(sprites, tilemaps);
        }
    }

    /// <summary>
    /// The synthetic W006-V world: the real view classes (slot grid, deck, depot and link layer, deposit, effect pool view, world effects feed) over the flat-colour
    /// catalog of <see cref="DeckTestRig"/>, driven by a real <see cref="RunSession"/> with 12 running Miners. It also runs in the Windows test player. The frame
    /// work (<see cref="WorldFrame"/>) is what the bootstrap does per frame for the world; the session advance (<see cref="AdvanceRun"/>) is not measured.
    /// </summary>
    internal sealed class W006VWorld
    {
        private readonly DeckTestRig rig = new DeckTestRig();
        private readonly List<GameObject> hosts = new List<GameObject>();
        private readonly MilestoneWatcher milestones = new MilestoneWatcher();
        private RunFactorySnapshot developmentSnapshot;
        private DevelopmentStageValue development;
        private DepotLinkModel linkModel;
        private System.Action frameWork;

        public RunSession Session { get; private set; }
        public RunCommands Commands { get; private set; }
        public SlotGridView Grid { get; private set; }
        public FactoryDeckView Deck { get; private set; }
        public EffectPool Pool { get; private set; }
        public EffectPoolView EffectView { get; private set; }
        public DepotLinkView Links { get; private set; }
        public DepositView Deposit { get; private set; }
        public WorldEffectsFeed Feed { get; private set; }
        public FactoryWorldLayout Layout { get; private set; }
        public SlotVisual[] Forced { get; private set; }
        public int Stage => development.Stage;
        public long Now;
        public bool Mining = true;
        public bool Reduced;

        public System.Action Frame => frameWork;

        public void Dispose() => rig.Dispose();

        public RendererCensus Census() => RendererCensus.OfHosts(hosts);

        /// <summary>Builds the world for <paramref name="layout"/>; <paramref name="catalog"/> is the run content (the Iron catalog, or one with a production contract).</summary>
        public static W006VWorld Create(FactoryWorldLayout layout, RunCatalog catalog, string offerToAccept = null)
        {
            var world = new W006VWorld { Layout = layout };
            world.Build(layout, catalog, offerToAccept);
            return world;
        }

        private void Build(FactoryWorldLayout layout, RunCatalog catalog, string offerToAccept)
        {
            var permanent = new PermanentState(catalog);
            var start = NextRunStart.Compute(catalog, permanent);
            var state = new RunState(catalog, Guid.NewGuid(), 1, null, 1_000_000_000_000L, 0, start.StorageLevel, 1, catalog.RunStart.InitialUnlocks);
            if (new RunSimulator(catalog).Advance(state, permanent, 0, RunPlayerFixtures.StartUtcMs, null) != RunAdvanceResult.Ok)
            {
                throw new InvalidOperationException("W006-V: the run did not start.");
            }

            Session = new RunSession(catalog, state, permanent, RunPlayerFixtures.StartUtcMs, null);
            Commands = new RunCommands(Session);
            for (var slot = 0; slot < W006VFixture.Locations; slot++)
            {
                var built = Commands.Build(slot, "machine.miner");
                for (var upgrades = 0; !built.IsSuccess && upgrades < 8; upgrades++)
                {
                    Commands.UpgradeFactory();
                    built = Commands.Build(slot, "machine.miner");
                }

                if (!built.IsSuccess)
                {
                    throw new InvalidOperationException("W006-V: slot " + slot + " did not build: " + built.ReasonCode);
                }

                if (!Session.Advance(2_300_000).IsSuccess) throw new InvalidOperationException("W006-V: advance failed");
            }

            if (offerToAccept != null)
            {
                Session.Advance(0);
                if (!Commands.AcceptContract(offerToAccept).IsSuccess) throw new InvalidOperationException("W006-V: the contract was not accepted.");
            }

            var spriteCatalog = rig.Catalog();
            var gridHost = new GameObject("w006v-grid");
            rig.Owned.Add(gridHost);
            hosts.Add(gridHost);
            Grid = gridHost.AddComponent<SlotGridView>();
            Grid.Configure(spriteCatalog);
            Grid.SetLayout(layout);

            var deckHost = new GameObject("w006v-deck");
            rig.Owned.Add(deckHost);
            hosts.Add(deckHost);
            Deck = deckHost.AddComponent<FactoryDeckView>();
            Deck.Configure(spriteCatalog, rig.Settings());
            Deck.Bind(Grid);
            Grid.SetProjectedPixelsPerCell(24f);

            Pool = new EffectPool();
            var fxHost = new GameObject("w006v-fx");
            rig.Owned.Add(fxHost);
            hosts.Add(fxHost);
            EffectView = fxHost.AddComponent<EffectPoolView>();
            EffectView.Configure(rig.Catalog(), Pool);
            EffectView.Bind(Grid);
            Feed = new WorldEffectsFeed(Session, Pool);

            var linkHost = new GameObject("w006v-links");
            rig.Owned.Add(linkHost);
            hosts.Add(linkHost);
            Links = linkHost.AddComponent<DepotLinkView>();
            Links.Configure(spriteCatalog);
            Links.Bind(Grid);
            linkModel = new DepotLinkModel();

            var depositHost = new GameObject("w006v-deposit");
            rig.Owned.Add(depositHost);
            hosts.Add(depositHost);
            Deposit = depositHost.AddComponent<DepositView>();
            Deposit.Configure(spriteCatalog);
            Deposit.Bind(Grid);

            // The established-stage deck and the forced W004-V presentation of the 12 occupied locations.
            developmentSnapshot = RunSnapshots.Factory(Session);
            development = DevelopmentStage.Evaluate(developmentSnapshot, catalog.FactoryLevels[0].SlotCount);
            milestones.Observe(developmentSnapshot, Session.State.RunId, 0, catalog.FactoryLevels[0].SlotCount);
            var basis = DeckTestRig.Frame(W006VFixture.Locations, i => SlotCondition.Occupied);
            Forced = W004VFixture.Apply(basis);
            Grid.Render(Forced);
            Deck.SetDevelopment(development);

            var inventory = Session.State.Inventory;
            frameWork = () =>
            {
                milestones.Observe(developmentSnapshot, Session.State.RunId, 0, catalog.FactoryLevels[0].SlotCount);
                Deck.SetDevelopment(development);
                Links.SetStage(development.Stage);
                Deposit.SetStage(development.Stage);
                Deck.TickFeedback(Now);
                milestones.TryDequeue(out _);
                Feed.Update(Layout, Now, Reduced);
                // Three completion pops at three locations: the pool's active limit, so 3 effects stay on screen (the 4th would be dropped).
                Pool.TrySpawn(EffectKind.CompletionPop, 0);
                Pool.TrySpawn(EffectKind.CompletionPop, Layout.LocationCount / 3);
                Pool.TrySpawn(EffectKind.CompletionPop, 2 * Layout.LocationCount / 3);
                EffectView.Present();
                linkModel.Refresh(Layout.LocationCount, Feed.LiveStates, Feed.LiveStateCount, Feed.Model, inventory.Total, inventory.Capacity);
                Links.Present(linkModel, Now, Reduced);
                Deposit.Present(Mining, Now, Reduced);
            };
        }

        /// <summary>One simulated frame of 100 ms of run time (not measured), then the presentation clock moves on.</summary>
        public void AdvanceRun()
        {
            Now += 100;
            if (!Session.Advance(1_000_000).IsSuccess) throw new InvalidOperationException("W006-V: advance failed");
        }

        public int RunningBranches => linkModel.RunningCount;
    }
}
