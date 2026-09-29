Create the specification for **Feature 003: First Factory Gameplay & Prestige Loop** for the game **Mobile Idle Factory**.

The technical **Core Game Foundation exists as Feature 001**, and the gameplay-content JSON authoring migration is complete as **Feature 002**. This specification must build on that foundation rather than redesigning it.

Feature 003 introduces the first actual gameplay systems and should result in the first complete playable progression loop:

**Start a new factory → build and optimize production → complete contracts → use items → research improvements → scale the factory → prestige → start again permanently stronger.**

The first prestige should be realistically reachable after approximately **2 hours of reasonably active first-time gameplay**.

The specification should focus on gameplay behavior, rules, state transitions, progression, balancing targets, edge cases and acceptance criteria.

Do not prescribe unnecessary implementation architecture. Technical architecture belongs primarily in the planning phase.

---

# 1. Core Design Philosophy

Mobile Idle Factory is an **Idle / Incremental Factory Optimization Game**.

The game should not become a Factorio-style logistics simulator.

The primary recurring player question should be:

> What is currently limiting my factory, and what is the best use of my next investment?

Meaningful decisions should primarily come from:

- production rates
- resource consumption
- bottlenecks
- limited machine slots
- machine upgrades
- building duplicate machines
- storage capacity
- product value
- selling decisions
- contracts
- temporary items
- run-specific Research
- prestige and permanent meta progression

The player should be able to understand why production is limited and make deliberate decisions to improve it.

The game should remain approachable on mobile while still supporting increasingly deep optimization.

---

# 2. Feature 003 Progression Structure

Feature 003 consists conceptually of the following progression phases.

## Phase 1 – Factory Fundamentals

Introduce:

- production simulation
- resources
- recipes
- machines
- global inventory
- storage
- visible machine slots
- machine construction
- duplicate machines
- machine upgrades
- machine milestones
- economy and selling
- product progression
- production bottlenecks
- active Factory Overdrive

## Phase 2 – Contracts

Introduce:

- production objectives
- contract rewards
- timed contracts where appropriate
- Research Point rewards
- Item rewards where appropriate

## Phase 3 – Consumable Items

Introduce:

- Item Inventory
- Tier I–III items
- temporary effects
- tactical item usage
- persistent unused items across prestige

## Phase 4 – Research

Introduce:

- Research Points
- Research progression
- immediate Research purchases
- run-specific production/economy improvements

Research must NOT be time-gated.

## Phase 5 – Prestige

Introduce:

- Factory Sale
- Industry Points
- permanent meta progression
- complete factory rebuild
- noticeably accelerated subsequent runs

---

# 3. First Factory – Iron Age

The entire first production experience uses **Iron as the only raw resource family**.

Do not introduce Copper, Coal, Steel, Silicon or other resource families in Feature 003.

The production chain is:

Iron Ore  
→ Iron Bar  
→ Iron Plate  
→ Gear

The corresponding machines are:

Iron Ore → Miner  
Iron Bar → Smelter  
Iron Plate → Press  
Gear → Assembler

This intentionally simple chain should teach the player how production, consumption and bottlenecks work before future specifications introduce branching production networks.

---

# 4. Baseline Production

Initial balancing targets:

## Miner

Requires no input.

Cycle time:

2 seconds

Output:

2 Iron Ore

Effective base production:

1 Iron Ore / second

## Smelter

Recipe:

2 Iron Ore  
→ 1 Iron Bar

Cycle time:

2 seconds

Effective consumption:

1 Iron Ore / second

Effective production:

0.5 Iron Bars / second

## Press

Recipe:

2 Iron Bars  
→ 1 Iron Plate

Cycle time:

4 seconds

Effective consumption:

0.5 Iron Bars / second

Effective production:

0.25 Iron Plates / second

## Assembler

Recipe:

2 Iron Plates  
→ 1 Gear

Initial target cycle time:

5 seconds

Effective production:

0.2 Gears / second when sufficiently supplied.

These values are initial balancing targets rather than immutable constants.

The initial production chain should be understandable and approximately synchronized before upgrades begin introducing bottlenecks.

---

# 5. Production Simulation

Machines automatically execute production cycles according to the simulation rules provided by the Core Game Foundation.

A processing machine may start or complete production only according to clearly defined resource and storage rules.

The specification must define deterministic behavior for:

- insufficient recipe inputs
- unavailable output storage
- multiple machines consuming the same resource
- multiple machines completing production simultaneously
- machine construction/removal during production
- machine upgrades during production
- temporary effects changing production speed
- offline simulation

Production must never duplicate or destroy resources unintentionally.

---

# 6. Global Inventory

All factory resources use a **shared global inventory**.

Machines do not have independent inventories.

Conceptually:

Machine  
→ Global Inventory  
→ Machine

Every produced resource enters the global inventory.

Every processing machine consumes its recipe inputs from the global inventory.

Example:

GLOBAL STORAGE

Iron Ore: 420  
Iron Bars: 180  
Iron Plates: 75  
Gears: 20

Used:

695 / 1,000

The capacity applies to the combined quantity of all stored resource types.

This creates strategic pressure because excessive accumulation of one resource can consume storage needed by other products.

---

# 7. Storage Progression

Initial global storage capacity:

1,000 units.

Storage can be upgraded during the current run.

Initial balancing targets:

Storage Lv.1 → 1,000  
Storage Lv.2 → 2,000  
Storage Lv.3 → 4,000  
Storage Lv.4 → 8,000  
Storage Lv.5 → 16,000

Example upgrade costs:

Lv.2 → $2,500  
Lv.3 → $10,000  
Lv.4 → $40,000  
Lv.5 → $160,000

These values are balancing targets.

The specification must define deterministic behavior when storage is full.

Production must never cause inventory capacity to be exceeded.

Storage upgrades reset on prestige.

Future specifications may expand storage mechanics, but Feature 003 should use one shared global capacity.

---

# 8. Factory Machine Slots

Factory space is intentionally limited.

The player has **visible physical machine slots**.

Each slot can contain one machine.

Example:

[ Miner ] [ Smelter ] [ Press ] [ Empty ]

Multiple machines of the same type are allowed.

Example:

[ Miner ]  
[ Smelter ]  
[ Smelter ]  
[ Press ]

This creates a fundamental decision:

> Upgrade an existing machine or use valuable factory space for another machine?

Machine position itself has no gameplay effect in Feature 003.

There are no adjacency bonuses.

Initial factory slot progression target:

Factory Lv.1 → 4 slots  
Factory Lv.2 → 6 slots  
Factory Lv.3 → 8 slots  
Factory Lv.4 → 10 slots  
Factory Lv.5 → 12 slots

Factory expansion should become progressively more expensive.

Factory level and available slots reset on prestige.

---

# 9. Conveyor Belts and Logistics

Gameplay-relevant conveyor belts are explicitly outside Feature 003.

There is no:

- belt throughput
- belt capacity
- routing
- splitters
- mergers
- belt upgrading
- physical resource-routing simulation

Resources are transferred logically through the global inventory.

However, the game may visually display:

- conveyor belts
- pipes
- transported resources
- machines exchanging materials
- other factory activity

These visuals do not represent individual simulated resources.

For example:

Simulation:

8.72e18 Iron Ore / second

Visual representation:

a manageable number of animated ore objects moving through the factory.

Visual production must therefore be decoupled from actual simulation quantities.

Future specifications may revisit logistics.

Feature 003 must not require future logistics systems, but should avoid unnecessary decisions that make their later introduction prohibitively difficult.

---

# 10. Machine Construction

Machines are built into available factory slots.

Construction requires Cash.

The player cannot build a machine if no slot is available.

Different machine types may have different construction costs.

Construction costs should increase appropriately as the player progresses through the production chain.

Duplicate machines are explicitly supported.

The specification should define behavior for removing or replacing machines.

Machine construction and machine ownership reset on prestige.

---

# 11. Machine Upgrades

Every machine instance can be upgraded individually.

Upgrades improve productive capability.

An initial balancing model may use approximately:

Upgrade Cost = Base Cost × 1.15^Level

Regular production growth may use approximately:

Base Production × 1.08^(Level - 1)

These are balancing starting points, not mandatory final formulas.

Machine upgrades should become increasingly expensive while still creating meaningful progression.

---

# 12. Machine Milestones

Machine levels include significant milestone bonuses.

Initial milestone targets:

Level 10 → ×2 production  
Level 25 → ×2 production  
Level 50 → ×3 production  
Level 100 → ×5 production

Additional milestones may later exist.

Milestones should create meaningful short-term goals.

The player should sometimes choose to continue upgrading a machine specifically because an important milestone is approaching.

Example:

Press Lv.24

Current production:

3.12 Plates/s

Next milestone:

Lv.25

Production multiplier:

×2

Machine milestones reset with the machine on prestige.

---

# 13. Production Bottlenecks

Bottleneck management is a central gameplay mechanic.

The player should have enough information to understand:

- production per second
- consumption per second
- net resource change
- unavailable recipe inputs
- idle machines
- storage saturation
- downstream shortages

Example:

Iron Ore

Production: +1.5/s  
Consumption: -1.0/s  
Net: +0.5/s

The player should recognize that Iron Ore production currently exceeds Smelter capacity.

Possible responses include:

- upgrade Smelter
- construct another Smelter
- upgrade another related machine
- expand factory slots
- expand storage
- change automatic selling
- activate an Item
- purchase Research

The game must not automatically choose the optimal solution.

---

# 14. Economy and Selling

Resources have individual market values.

Initial balancing targets:

Iron Ore → $1  
Iron Bar → $3  
Iron Plate → $8  
Gear → $24

Further processing should generally increase economic value.

Example:

2 Iron Ore = $2

Processing produces:

1 Iron Bar = $3

The player therefore gains value by processing materials.

The player can configure automatic selling per resource.

Example:

Iron Ore → OFF  
Iron Bar → OFF  
Iron Plate → ON  
Gear → ON

This creates a trade-off between immediate Cash and preserving intermediate products for downstream production.

Cash resets on prestige.

---

# 15. Product Unlock Progression

The entire production chain should not necessarily be available immediately.

The player gradually progresses through:

Iron Ore  
→ Iron Bars  
→ Iron Plates  
→ Gears

The Assembler and Gear production should become relevant after the player already understands basic production.

Initial target:

Gear production becomes relevant after approximately **10–15 minutes** during the first run.

Exact timing is a balancing target.

---

# 16. Active Gameplay – Factory Overdrive

The game should provide occasional active interactions without making continuous activity mandatory.

Initial active mechanic:

Factory Overdrive

Target effect:

×2 production speed

Target duration:

30 seconds

Research may later improve its strength or duration.

Active mechanics should accelerate progress but should not invalidate idle gameplay.

---

# 17. Contracts

Contracts provide production objectives.

Their purpose is to encourage temporary production decisions rather than simply maximizing passive Cash income.

Example:

Industrial Parts

Deliver:

100 Iron Plates

Normal market value:

$800

Contract reward:

approximately $1,000

Another example:

Gear Shipment

Deliver:

50 Gears

Normal market value:

$1,200

Reward:

approximately $1,500

Timed contracts may provide increased rewards.

Example:

Deliver:

250 Iron Bars

Time limit:

5 minutes

Normal market value:

$750

Reward:

approximately $1,125

Contracts may reward:

- Cash
- Research Points
- Consumable Items

Contracts are a primary source of Research Points.

Contract progression resets on prestige.

---

# 18. Consumable Items

Items are **temporary consumable gameplay tools**.

They are NOT permanent passive bonuses.

Items are stored in an Item Inventory.

Unused Items **survive prestige**.

Activating an Item consumes one instance.

Temporary effects should generally last approximately **1–2 minutes** during early balancing.

---

# 19. Item Tiers

Items have at least three tiers.

## Tier I

Weakest.

Rare and meaningful when first introduced.

Becomes increasingly common as progression advances.

## Tier II

Stronger.

Initially substantially rarer than Tier I.

Becomes easier to obtain later.

## Tier III

Strongest Item tier within Feature 003.

Rare and valuable during early progression.

Should eventually become more obtainable as long-term progression advances.

The intended progression feeling is:

Early:

Tier I feels exciting.

Later:

Tier I becomes normal and Tier II becomes exciting.

Still later:

Tier II becomes accessible and Tier III becomes the desirable reward.

Item rarity is therefore relative to overall player progression.

---

# 20. Example Items

Example:

Diamond Drill Head — Tier I

Target:

Miner

Effect:

×2 Miner Output

Duration:

60 seconds

Possible additional Item archetypes:

Overclocked Motor

Temporarily increases machine speed.

Precision Mold

Temporarily reduces recipe input requirements.

Industrial Lubricant

Temporarily increases production across the factory.

Golden Contract

Temporarily improves or multiplies a Contract reward.

Higher-tier Items may modify gameplay more strongly rather than simply increasing percentages.

Items should encourage tactical use around:

- bottlenecks
- Contracts
- progression pushes
- pre-prestige production
- post-prestige acceleration

Permanent Equipment is NOT part of Feature 003.

---

# 21. Research

Research is **run-specific progression**.

It behaves conceptually as an additional progression mechanic connected strongly to Contracts.

Research resets completely on prestige.

Research is NOT permanent meta progression.

---

# 22. Research Must Not Be Time-Gated

Research must not use timers such as:

"Research completes in 20 minutes."

Instead, the player earns Research Points through gameplay, especially Contracts.

Research Points can be spent immediately.

Conceptually:

Complete Contracts  
→ Receive Research Points  
→ Purchase Research  
→ Factory becomes stronger during the current run

This means optimized and active gameplay can accelerate Research.

---

# 23. Research Examples

Possible Research categories:

Production  
Factory  
Economy  
Idle

Example Research:

Efficient Mining

Cost:

3 RP

Effect:

+10% Miner Output

Industrial Furnaces

Cost:

5 RP

Effect:

+10% Smelter Speed

Precision Manufacturing

Cost:

8 RP

Effect:

+15% Press Output

Larger Storage

Cost:

3 RP

Effect:

+25% Storage Capacity

Better Contracts

Cost:

5 RP

Effect:

+10% Contract Rewards

Research Points and purchased Research upgrades reset on prestige.

Research exists to accelerate and diversify the current run.

---

# 24. Offline Progression

Offline progression already exists in the Core Game Foundation.

Do not redesign the underlying offline system.

The established maximum offline effectiveness is:

**80%**

Every gameplay system introduced by Feature 003 must define its interaction with offline simulation.

Relevant cases include:

- machine production
- resource consumption
- inventory capacity
- selling
- Contracts
- temporary Item effects
- Factory Overdrive
- Research-derived modifiers

The specification must explicitly define whether timers such as temporary Item effects or timed Contracts continue while the player is offline.

Offline results must remain deterministic and consistent with the Core Game Foundation.

---

# 25. Prestige – Factory Sale

Prestige is represented as **selling the current factory**.

The player then builds a new factory from the beginning.

The first prestige should be realistically reachable after approximately:

**2 hours of reasonably active first-run gameplay.**

Example:

Factory Value:

$10,000,000

SELL FACTORY

→ receive Industry Points  
→ reset current factory  
→ begin next factory

---

# 26. Prestige Reset Rules

Prestige resets:

- Cash
- resources
- machines
- machine levels
- factory level
- machine slots unlocked through factory progression
- storage upgrades
- product unlock progression
- Contract progression
- Research Points
- purchased Research
- active temporary effects

Prestige preserves:

- Industry Points
- purchased Industry / Meta upgrades
- unused Consumable Items
- achievements
- lifetime statistics
- other explicitly account-level progression

The specification should clearly classify all state as:

- run-specific
- temporary
- prestige-persistent
- permanent meta progression

---

# 27. Industry Points

Industry Points are the primary permanent prestige currency.

Their purpose is to make subsequent runs noticeably faster.

A player should feel a substantial difference after prestige.

Initial progression targets:

First run to prestige:

approximately 120 minutes

After first prestige, reaching the equivalent progression point:

approximately 70–80 minutes

After additional progression:

approximately 45–55 minutes

Later:

approximately 30–40 minutes

These are balancing targets rather than strict guarantees.

---

# 28. Industry Meta Progression

Possible Industry upgrades:

Efficient Production

+10% global production per level

Investor Network

+25% starting Cash per level

Industrial Engineering

-5% machine upgrade costs per level

Warehouse Planning

+20% starting storage capacity per level

Experienced Workforce

+10% machine speed per level

Later upgrades may fundamentally accelerate early progression.

Example:

Automated Startup

Future factories start with Miner Lv.10.

This is desirable.

Experienced players should eventually bypass progression that was meaningful during their first runs.

---

# 29. Prestige Reward Scaling

Prestige rewards should use diminishing returns so that indefinitely delaying prestige is not always optimal.

Initial candidate formula:

IndustryPoints =
floor(10 × sqrt(FactoryValue / 10,000,000))

Example:

$10M → 10 IP  
$40M → 20 IP  
$90M → 30 IP  
$1B → 100 IP

This is an initial balancing candidate, not an immutable formula.

---

# 30. Data-Driven Gameplay

Gameplay content and balancing data must be **data-driven and easily maintainable by both humans and AI agents**.

Balancing ordinary gameplay content should generally not require editing application or simulation source code.

A structured, human-readable format such as **JSON** is preferred where appropriate.

The exact technical format and schema belong to planning, but the resulting system must satisfy this content-authoring requirement.

---

# 31. Configurable Gameplay Data

Examples of gameplay information that should be externally configurable include:

- resource definitions
- product definitions
- display metadata
- recipes
- recipe inputs
- recipe outputs
- production cycle times
- machine definitions
- machine base production values
- machine construction costs
- machine upgrade parameters
- machine milestone levels
- machine milestone effects
- factory slot progression
- factory expansion costs
- storage capacities
- storage upgrade costs
- market values
- unlock requirements
- Contract definitions
- Contract objectives
- Contract rewards
- Contract scaling parameters
- Item definitions
- Item tiers
- Item effects
- Item durations
- Item acquisition/drop parameters
- Research definitions
- Research Point costs
- Research effects
- Research dependencies
- Overdrive parameters
- prestige requirements
- Industry Point reward parameters
- Industry Meta upgrades
- economy scaling constants
- progression balancing constants

The goal is to allow rapid balancing iterations without modifying gameplay engine code.

---

# 32. Gameplay Rules vs Gameplay Content

The design must distinguish between:

## Gameplay Rules

Generic systems responsible for interpreting gameplay.

Examples:

- production simulation
- recipe processing
- machine upgrading
- effect application
- Contract evaluation
- Research purchasing
- prestige processing

## Gameplay Content

Definitions interpreted by those systems.

Examples:

- Iron Ore
- Smelter
- Iron Plate recipe
- Diamond Drill Head
- Efficient Mining Research
- specific upgrade costs
- specific production multipliers

Ordinary content changes should generally affect data rather than engine code.

Example:

Changing:

2 Iron Bars  
→ 1 Iron Plate

to:

3 Iron Bars  
→ 2 Iron Plates

should primarily be a content change.

Changing:

Diamond Drill Head

×2 Miner Output  
60 seconds

to:

×2.5 Miner Output  
90 seconds

should primarily be a content change.

Changing:

Efficient Mining

+10% Miner Output  
3 RP

to:

+15% Miner Output  
5 RP

should primarily be a content change.

---

# 33. Example Data Definitions

The following examples illustrate the desired authoring philosophy but do NOT prescribe the final technical schema.

Example Recipe:

```json
{
  "id": "iron_plate",
  "machine": "press",
  "inputs": {
    "iron_bar": 2
  },
  "outputs": {
    "iron_plate": 1
  },
  "cycleTimeSeconds": 4
}
```

Example Research:

```json
{
  "id": "efficient_mining_1",
  "category": "production",
  "cost": {
    "researchPoints": 3
  },
  "effect": {
    "type": "machine_output_multiplier",
    "target": "miner",
    "value": 1.1
  }
}
```

Example Item:

```json
{
  "id": "diamond_drill_head",
  "tier": 1,
  "target": "miner",
  "durationSeconds": 60,
  "effect": {
    "type": "output_multiplier",
    "value": 2
  }
}
```

---

# 34. Human- and AI-Friendly Content Authoring

Gameplay configuration should be consistent enough that both developers and AI coding agents can safely:

- add resources
- add machines
- add recipes
- modify balancing values
- create Items
- modify Item tiers
- create Research nodes
- create Contracts
- adjust progression curves

without requiring broad source-code changes.

Prefer:

- stable identifiers
- predictable structures
- explicit units
- explicit dependencies
- separation between stable identifiers and player-facing text
- machine-readable validation
- understandable validation errors

Avoid values whose meaning depends on undocumented implementation knowledge.

Prefer:

```json
{
  "durationSeconds": 60,
  "outputMultiplier": 2
}
```

over ambiguous structures such as:

```json
{
  "duration": 60,
  "value": 2
}
```

when practical.

---

# 35. Gameplay Data Validation

Invalid gameplay configuration must fail clearly rather than silently producing broken simulation behavior.

Examples:

- Recipe references unknown Resource
- Machine references unknown Recipe
- Research references unknown Machine
- Item references unsupported effect type
- negative production duration
- zero or negative recipe quantity
- invalid Item tier
- duplicate stable identifier
- circular or impossible Research dependency
- invalid prestige configuration
- malformed progression curve

Validation should identify the offending content entry and provide enough information for a human or AI agent to correct it.

Gameplay-data validation should be considered part of Feature 003.

---

# 36. Extensibility Principle

Feature 003 contains only Iron.

The underlying content model must nevertheless not assume that Iron is the only possible resource family.

Future specifications should be able to introduce:

Copper  
Coal  
Steel  
Silicon  
Wire  
Circuits  
Motors  
and other resources

primarily through new content definitions when they use already-supported gameplay mechanics.

The same principle applies to future:

- Machines
- Recipes
- Items
- Research
- Contracts

Do NOT attempt to make every imaginable future gameplay mechanic configurable in advance.

The guiding rule is:

> Gameplay mechanics are implemented. Gameplay content and balancing are configured.

For example:

New Smelter variant using existing mechanics  
→ content/configuration

New Recipe  
→ content/configuration

New Research using an existing effect type  
→ content/configuration

New Tier II Item using an existing temporary effect  
→ content/configuration

Entirely new Machine Overheating mechanic  
→ new gameplay implementation

Machine-specific Overheating values after that mechanic exists  
→ content/configuration

Avoid creating an unnecessary general-purpose scripting language or over-engineered game engine.

---

# 37. High-Number Compatibility

Mobile Idle Factory is an Incremental Game and should eventually support extremely large values.

Although Feature 003 starts with values such as:

1  
100  
10,000  
10,000,000

future progression may reach:

1e30  
1e80  
1e150  
or beyond.

Gameplay requirements and data definitions should avoid assumptions that ordinary small integer ranges will always be sufficient.

The specification should identify relevant precision and representation requirements without unnecessarily prescribing implementation details.

---

# 38. Important Edge Cases

The specification must explicitly cover at least:

- insufficient recipe inputs
- storage full
- multiple machines consuming the same resource
- simultaneous production completion
- selling resources while machines need them
- constructing duplicate machines
- no available machine slot
- removing/replacing a machine
- upgrading a machine during production
- reaching a machine milestone
- activating temporary Items
- activating multiple compatible/incompatible Item effects
- Item expiration
- going offline while an Item is active
- timed Contract behavior while offline
- Overdrive behavior while offline
- prestige while temporary effects are active
- prestige while a Contract is active
- prestige with unused Items
- Research reset during prestige
- Industry progression persistence
- invalid gameplay configuration
- very large numerical values

---

# 39. Explicit Non-Goals

Feature 003 must NOT introduce:

- Copper
- Coal
- Steel
- Silicon
- Electronics
- multiple raw-resource families
- branching industrial production networks
- gameplay-relevant conveyor belts
- belt routing
- belt throughput
- splitters
- mergers
- permanent Equipment
- machine adjacency bonuses
- machine synergies
- multiple industrial worlds
- Mega Projects
- satellites
- fusion reactors
- lunar factories
- advanced logistics
- endgame progression

These belong to future specifications.

Do not invent additional major gameplay systems outside this scope.

---

# 40. Desired Player Experience

At the end of Feature 003, the following complete experience must exist:

The player starts with a tiny Iron factory.

They learn how Iron Ore is produced.

They construct and improve production machines.

They unlock Iron Bars, Iron Plates and eventually Gears.

They begin creating production bottlenecks.

They learn to identify those bottlenecks.

They must decide whether to upgrade existing machines or spend limited factory slots on duplicate machines.

They manage global storage.

They decide which resources to sell and which to preserve for further processing.

They expand the factory.

They reach machine milestones.

They use Factory Overdrive.

They unlock Contracts.

Contracts create temporary production goals.

They earn Research Points.

They find increasingly valuable Consumable Items.

They tactically use those Items or save them for future runs.

They purchase Research upgrades without waiting for timers.

Their factory becomes increasingly powerful during the run.

After approximately two hours of first-run progression, they become capable of selling the factory.

They prestige.

The factory itself is reset.

Their unused Items survive.

They receive Industry Points.

They purchase permanent Industry upgrades.

They begin a new factory.

Previously difficult progression is now noticeably faster.

The player should immediately understand:

**"I lost my factory, but I became permanently stronger."**

The resulting loop should create the desire to optimize the next run and reach the next prestige faster.

---

# 41. Specification Output Expectations

Generate a complete Spec-Kit specification suitable for subsequent:

- clarification
- planning
- data-model design
- contract definition
- balancing
- task generation
- MAQA implementation

The specification should contain clear:

- user stories
- functional requirements
- acceptance scenarios
- gameplay rules
- state ownership / persistence expectations
- progression requirements
- edge cases
- measurable success criteria

Treat explicitly stated gameplay decisions as requirements.

Treat numerical values identified as balancing targets as initial values that may later be tuned through the data-driven content system.

Do not introduce implementation architecture unless required to express a functional constraint.

Do not expand the scope into later-feature systems.

Where an important behavior remains genuinely ambiguous, identify it for clarification rather than silently inventing a major design decision.