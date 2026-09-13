# Product Concept

Create a specification for a mobile-first idle incremental factory game.

The game should combine:

* long-term incremental progression,
* offline progression,
* active factory optimization,
* free 2D factory building,
* automation,
* logistics,
* research,
* power management,
* multiple regions,
* prestige systems,
* optional active side-content,
* and optional roguelite-inspired expeditions.

The primary long-term progression inspiration is **Idle Obelisk Miner**.

The factory and logistics inspirations are **Factorio**, **Satisfactory**, and **Captain of Industry**.

The game should not copy these games directly. Their design principles should instead be used to create a distinct game identity.

---

# Core Game Fantasy

The player begins with almost nothing and gradually builds an increasingly large automated industrial empire.

The initial experience should start with very simple manual resource extraction.

The player then progressively replaces manual work with machines, logistics and automation.

The central progression fantasy is:

> Turn work into systems.

Conceptually:

Manual Work
→ Machine Assistance
→ Automation
→ Optimization
→ Large-Scale Industry

Over long-term progression, this may expand from a small local factory into:

Local Factory
→ Regional Industry
→ Multi-Region Industrial Network
→ Planetary Industry
→ Potentially Orbital or Interplanetary Industry

Later stages are a long-term direction and do not need to be fully defined yet.

---

# Primary Platform

The primary platform is mobile.

The game must therefore be designed around:

* touch controls,
* small screens,
* short sessions,
* long offline periods,
* interruption and backgrounding,
* battery and performance constraints,
* readable factory layouts,
* simple but powerful building interactions.

The architecture and interaction model should not prevent a future PC/Steam release.

---

# Core Factory Gameplay

Factories exist physically on a freely buildable two-dimensional grid.

The factory must not be represented only through abstract production menus or upgrade buttons.

Players should place and connect real factory structures such as:

* Miners
* Conveyor Belts
* Smelters
* Production Machines
* Storage
* Splitters
* Mergers
* Research Buildings
* Power Infrastructure
* Later Logistics Infrastructure

Spatial layout is an important part of gameplay.

Production systems should contain real throughput constraints.

Example:

Resource Deposit
→ Miner
→ Conveyor
→ Smelter
→ Intermediate Product
→ Assembler
→ Advanced Product

Players should be able to identify and solve factory bottlenecks.

---

# Mobile Factory UX

The free 2D grid must remain comfortable on touch devices.

The game should avoid requiring desktop-like precision.

Preferred interaction concepts include:

* strong grid snapping,
* tap-to-place machines,
* drag-to-build conveyor belts,
* automatic conveyor route suggestions,
* easy machine rotation,
* contextual actions,
* simple deletion and rebuilding,
* zoom and pan,
* clear input/output connection points,
* placement previews,
* optional undo support.

When a connection is obvious, the game should assist the player.

The player should still be able to manually optimize layouts.

Guiding principle:

> Reduce input friction without removing factory decisions.

---

# Simulation and Performance

Factory gameplay simulation must be separated from visual representation.

The game should not require every visible resource item on every conveyor belt to exist as an independently simulated gameplay entity.

Production should primarily use deterministic rate-based simulation.

Example:

Miner Output:
90 Ore / minute

Conveyor Capacity:
120 Items / minute

Smelter Consumption:
60 Ore / minute

The authoritative simulation tracks production rates, capacity, state and inventories.

Conveyor items may be rendered visually to communicate activity, but they do not need to represent every actual resource unit.

This separation should make very large factories technically feasible on mobile devices.

---

# Resource Deposits

Resource deposits are infinite.

Deposits do not permanently deplete through normal production.

They differ primarily through quality and extraction potential.

Possible conceptual qualities include:

* Poor
* Normal
* Rich
* Pure

Exact names and numerical values are not yet finalized.

Higher-quality deposits should support higher extraction rates.

Mining technology may further increase extraction throughput.

Discovering better deposits should therefore remain valuable throughout progression.

---

# World and Region Structure

The world consists of multiple distinct regions.

Each region contains a large freely buildable factory area.

Different regions provide different resource combinations.

Some basic materials may exist in multiple regions.

More advanced or rare resources may exist only in specific regions.

Possible conceptual regions may include:

* temperate region,
* desert region,
* volcanic region,
* frozen region,
* hostile or exotic region,
* later orbital environments.

These are examples and are not yet final content.

Unlocking a new region should introduce meaningful new opportunities instead of merely increasing numbers.

A region may introduce:

* new resources,
* new production methods,
* environmental constraints,
* new logistics requirements,
* new machines,
* new research opportunities.

---

# Inter-Region Logistics

Regions initially operate mostly independently.

Later progression should allow increasingly advanced transportation between regions.

Potential systems may include:

* trucks,
* automated transport routes,
* trains,
* cargo drones,
* ships,
* orbital logistics.

This should eventually enable regional specialization.

Example:

Region A produces Steel.

Region B produces Oil Products.

Region C produces Advanced Electronics.

The player may then decide where intermediate and final products should be manufactured.

Transportation decisions should eventually become part of factory optimization.

---

# Logistics and Bottlenecks

Logistics problems are intentional and should form a major part of the core gameplay.

Production may slow or stop because of:

* insufficient input,
* full output buffers,
* full storage,
* conveyor throughput limitations,
* insufficient downstream consumption,
* routing mistakes,
* insufficient power.

Production overflow must exist.

Resources should not disappear automatically merely because the factory produces too much.

If downstream production cannot consume a resource, upstream systems may eventually become blocked.

Players should have several possible solutions.

Examples:

* build additional machines,
* increase conveyor capacity,
* construct more storage,
* create another production route,
* use splitters and mergers,
* unlock alternate recipes,
* route overflow into resource sinks,
* consume resources in large projects.

The factory should behave as a system rather than as independent number generators.

---

# Offline Progression

The factory continues operating while the player is offline.

Offline players must remain fully viable.

Baseline factory production should conceptually operate according to the same rules online and offline.

Offline simulation must respect:

* input requirements,
* production rates,
* storage limits,
* conveyor capacity,
* blocked outputs,
* power constraints.

The game must not ignore logistics problems while offline.

If a factory becomes blocked after two hours of an eight-hour offline period, production should not magically continue for the remaining six hours.

On return, the player should receive a useful summary.

Example information:

* offline duration,
* resources produced,
* production that stopped,
* storage that became full,
* important bottlenecks,
* completed processes,
* major progression events.

The return screen should help the player understand what happened and what can be improved.

---

# Active vs Idle Philosophy

The game must support different play styles.

At minimum:

## Idle-Focused Player

Logs in briefly, collects progression, performs upgrades and leaves.

## Hybrid Player

Checks the factory regularly and occasionally spends longer periods optimizing production.

## Active Player

Enjoys factory redesign, optimization, progression systems and optional active gameplay.

All three play styles must remain viable.

Active gameplay should provide additional opportunities and rewards.

However:

> Active gameplay must never become mandatory for core progression.

And:

> Closing the game must never intentionally become better than actively playing.

Offline progression should prevent players with limited time from falling completely behind.

Active players should instead benefit through:

* optimization,
* faster decision-making,
* optional challenges,
* expeditions,
* better factory layouts,
* additional opportunities.

---

# Research

Research uses a hybrid factory-based model.

Research should not be generated completely independently of the factory.

The player produces specific research components through industrial production chains.

These resources are delivered to research infrastructure.

Conceptually:

Raw Resources
→ Components
→ Research Materials
→ Research Lab
→ Research Progress
→ Technology

Higher research tiers should require increasingly advanced products.

Research should therefore encourage expansion of the factory.

Research may unlock:

* new machines,
* new resources,
* new logistics systems,
* higher conveyor tiers,
* improved mining technology,
* power systems,
* automation,
* new regions,
* new progression systems.

---

# Power

Power is a meaningful factory system but should not exist immediately at the beginning of the game.

The player should first learn:

* extraction,
* conveyor logistics,
* smelting,
* processing,
* storage,
* production rates.

Electricity is introduced later as an additional optimization layer.

Conceptually:

Fuel
→ Generator
→ Power Network
→ Factory Machines

Players must eventually balance:

Power Production

against

Power Consumption.

Insufficient power should create factory problems.

However, power failures should generally create soft failure rather than irreversible punishment.

Possible effects include:

* reduced production speed,
* machines shutting down,
* production sections pausing,
* later priority systems.

Exact power behavior requires a separate specification.

---

# Early Active Gameplay

The beginning of the game should contain a short manual phase.

The player initially extracts resources manually.

A possible interaction is holding the screen to operate a mining tool or mining laser.

Example:

Hold
→ Mining Progress
→ Receive Ore

This manual gameplay should not remain the primary gameplay loop.

The player quickly unlocks the first automated Miner.

This transition is important because it establishes the main progression theme:

> What the player currently performs manually can eventually become automated.

The game should avoid permanent repetitive tapping mechanics.

---

# Automation Philosophy

Automation is one of the main rewards of progression.

Systems may initially require direct player involvement and later become automated.

Potential examples include:

* resource extraction,
* production routing,
* factory logistics,
* research supply,
* region transportation,
* expedition preparation,
* production balancing.

The player should usually understand a mechanic before automation removes most of its manual workload.

Automation should therefore feel earned.

---

# Industrial Projects

Major industrial projects should act as progression milestones.

A project requires delivery of significant quantities of several manufactured products.

Conceptual example:

Orbital Uplink

Requires:

* Iron Plates
* Copper Wire
* Electronics
* Advanced Components

Completing an industrial project may unlock:

* a new technology tier,
* a new region,
* a new machine category,
* a new progression system,
* major permanent capabilities.

Industrial projects should encourage players to expand and improve production chains.

They should function as major medium-term goals between regular upgrades.

---

# Expeditions

The game should contain optional active expeditions.

Expeditions are not required for normal factory progression.

However, they should be strongly rewarding.

Their role should be comparable in philosophy to optional long-term side systems such as Archaeology in Idle Obelisk Miner.

Players who engage with Expeditions gain meaningful benefits.

Players who largely ignore them remain able to progress through the core factory game.

---

# Roguelite Expeditions

Expeditions are the preferred place for roguelite mechanics.

A run may last roughly 30–120 seconds.

Exact duration is not finalized.

The gameplay should be simple enough for mobile use while still containing meaningful decisions.

Potential mechanics include:

* choosing routes,
* selecting gates,
* avoiding hazards,
* increasing temporary power,
* collecting resources,
* temporary upgrades,
* risk-versus-reward decisions.

The gameplay may take inspiration from satisfying gameplay often shown in mobile game advertisements.

Unlike misleading mobile advertisements, this gameplay must actually exist and meaningfully affect progression.

During a run the player may receive temporary upgrade choices.

Conceptual examples:

Overcharged Drills
+40% Mining Power
+20% Heat Generation

Drone Swarm
+2 Drones
-15% Cargo Capacity

Recycler
Chance to recover consumed resources

Temporary upgrades disappear after the run.

Permanent rewards are returned to the main game.

Potential rewards may include:

* rare resources,
* blueprint fragments,
* research materials,
* permanent upgrade resources,
* cosmetic resources,
* small amounts of premium currency.

Expedition design remains an open Human-in-the-Loop topic.

---

# Factory and Expedition Relationship

The factory remains the primary game.

Expeditions are secondary optional active content.

Both systems should reinforce one another.

Conceptually:

Factory
→ produces expedition equipment/resources
→ Expedition
→ provides rare rewards/blueprints
→ improves Factory

Overall loop:

Build
→ Automate
→ Optimize
→ Explore
→ Discover
→ Upgrade
→ Build

The two systems should not feel disconnected.

---

# Prestige

Prestige is a required core feature.

The game should contain at least one meaningful reset layer.

Prestige resets significant temporary progression in exchange for permanent advantages.

Prestige should not provide only generic production multipliers.

Permanent rewards may eventually include:

* production bonuses,
* improved starting conditions,
* automation,
* quality-of-life systems,
* technology unlocks,
* new strategic options,
* new progression systems.

Prestige should make later progression cycles feel meaningfully different or faster.

The game architecture should support additional prestige layers in future long-term progression.

Exact prestige mechanics remain to be designed separately.

---

# Premium Currency and Monetization

The game contains a premium currency.

Its philosophy should be comparable to the Gem system in Idle Obelisk Miner.

The currency can be purchased with real money.

However, it must also be obtainable through normal gameplay.

As players progress, reasonable amounts should become farmable.

Potential free sources include:

* achievements,
* industrial projects,
* challenges,
* expeditions,
* prestige milestones,
* events,
* progression milestones,
* rare rewards.

Premium currency may be used for:

* progression acceleration,
* convenience,
* quality-of-life improvements,
* additional choices,
* permanent upgrades,
* cosmetics.

Core gameplay must remain accessible without real-money purchases.

The game should avoid aggressive monetization.

Forced advertisements are not part of the current product vision.

Progression walls must not exist primarily to pressure the player into spending money.

The objective is:

> Paying supports or accelerates the experience without making free play intentionally frustrating.

---

# Long-Term Progression Philosophy

The game should support meaningful progression over months or potentially years.

Complexity should be introduced gradually.

The player must not start with every system available.

New systems should continue appearing well into progression.

Possible conceptual progression:

Manual Mining
→ Automated Mining
→ Conveyor Logistics
→ Smelting
→ Manufacturing
→ Research
→ Power
→ Advanced Production
→ Expeditions
→ New Regions
→ Regional Specialization
→ Cross-Region Logistics
→ Advanced Materials
→ Large Industrial Projects
→ Prestige
→ Planetary Industry
→ Orbital Industry
→ Interplanetary Industry

This is a conceptual progression direction and not yet a finalized roadmap.

---

# Core Design Principles

The game should prioritize:

* meaningful automation over repetitive tapping,
* long-term discovery over early feature overload,
* factory optimization over passive number generators,
* transparent production systems,
* meaningful bottlenecks,
* offline viability,
* rewarding active gameplay,
* fair free progression,
* strong incremental progression,
* player-created industrial systems,
* simple mobile interaction with deep underlying systems.

The factory should remain the central identity of the game.

---

# Human-in-the-Loop Requirements

The following design questions are intentionally unresolved.

They must not be silently decided during implementation.

When one becomes relevant, provide multiple realistic options, explain their consequences, recommend one option, and request an explicit human decision.

Open topics include:

1. Exact early-game progression.
2. First resource types.
3. First machine types.
4. Exact production ratios.
5. Conveyor mechanics.
6. Factory grid dimensions and expansion.
7. Building placement rules.
8. Resource deposit quality values.
9. Research tree structure.
10. Power generation and failure behavior.
11. Region progression.
12. Inter-region transportation.
13. Expedition gameplay.
14. Roguelite upgrade structure.
15. Expedition reward economy.
16. Prestige reset scope.
17. Prestige currencies.
18. Offline progression limits.
19. Storage behavior.
20. Resource sinks.
21. Premium currency balancing.
22. Industrial Project structure.
23. Long-term progression pacing.
24. Additional prestige layers.
25. Exact monetization products.

Do not invent answers to these questions merely to unblock development.

Use structured Human-in-the-Loop decision briefs whenever these decisions become necessary.

---

# Current Product Identity

The current product vision can be summarized as:

> A mobile-first idle incremental factory game in which the player starts with manual resource extraction and gradually builds large automated factories across multiple regions.

> Resources flow through real production chains with throughput, storage, logistics and power constraints.

> The player's factories continue operating while offline.

> Active players can optimize factories and participate in optional high-value roguelite expeditions.

> Progression gradually turns manual actions into automated industrial systems.

> Research, new regions, industrial projects and prestige continually expand the game's complexity.

The intended recurring player motivation is:

> One more upgrade.

> One more production line.

> One more bottleneck solved.

> One more project.

> One more run.

> One more prestige.
