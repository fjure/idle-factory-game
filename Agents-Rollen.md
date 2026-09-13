Ja. Für dein Projekt würde ich die Toolchain lokal-first, modular und rollenbasiert aufbauen. Wichtig ist: Nicht jeder Agent bekommt alle Tools. Ein Art-Agent sollte nicht einfach Gameplay-Code ändern dürfen, und ein Developer-Agent sollte nicht eigenmächtig neue Artstyles generieren.

Empfohlene Rollen + MCPs
Rolle	Hauptaufgabe	Tools/MCPs	Kosten
Game Designer	Mechanics, Progression, Balancing, Specs	Spec Kit, Files/Web, ggf. Tabellen/Simulation	praktisch 0 €
Software Architect	Architektur, Domain Model, Performance	Git/Filesystem, Unity MCP	0 €
Gameplay Developer	C#, Grid, Factory, Logistics, Save-System	Unity MCP, Git	0 €
UI/UX Developer	Mobile UI, Touch, HUD, Build UX	Unity MCP, ggf. ComfyUI	0 €
Art Director	Art Bible, Shape Language, Palette, Konsistenz	ComfyUI MCP, Referenzbilder	0 € lokal
Asset Artist	Gebäude, Icons, Ressourcen, Regionsassets	ComfyUI MCP	0 € lokal
Technical Artist	Import, Sprite Atlases, Animation, Prefabs, Optimierung	Unity MCP, optional Blender MCP	0 €
3D/Render Artist	Modelle → gerenderte 2D Sprites	Blender MCP	0 €
Sound Designer	Maschinenloops, UI-SFX, Ambience	lokale Audio-Tools + optional ElevenLabs	0 € bis günstig
Composer	Musik pro Region/Progressionsphase	lokal / ElevenLabs fallback	variabel
QA Agent	Tests, Profiling, Regressionen	Unity MCP	0 €
Performance Agent	Factory-Stresstests, Mobile Profiling	Unity MCP / Profiler	0 €

Der wichtigste Baustein wäre für mich MCP for Unity von CoplayDev. Das Projekt ist MIT-lizenziert und unterstützt Asset-, Scene-, Script-, Test-, Profiling- und Build-Automation direkt aus MCP-Clients. Es bietet inzwischen über 40 Editor-Tools und unterstützt auch Multi-Agent-Workflows über HTTP.

Interessant ist inzwischen sogar, dass MCP for Unity selbst Tools für 2D-Bild- und Audio-Generierung besitzt; die Asset-Generation verwendet externe Provider und ist Bring-your-own-key. Du könntest also später auch über denselben Unity-Agenten Assets generieren/importieren lassen, statt zwingend eine komplett separate Pipeline zu haben.

1. Developer / Architect

Hier reicht zunächst:

Unity 6 + CoplayDev Unity MCP + Git.

Der Agent darf zum Beispiel:

Assets/Scripts/
├── Domain/
├── Simulation/
├── Application/
├── Presentation/
└── Persistence/

anlegen, Tests ausführen, Szenen inspizieren, Prefabs konfigurieren und Compilerfehler lesen.

Der Architect sollte aber hauptsächlich Plain-C#-Architektur erzeugen und Unity eher zum Validieren nutzen.

Ich würde dafür zwei getrennte Agentenrollen definieren:

Software Architect
→ entscheidet Struktur und technische Patterns

Gameplay Developer
→ implementiert nach Architektur und Spec

Der Developer darf von der Architektur abweichen, aber nur mit Begründung bzw. Human-in-the-loop bei größeren Konsequenzen.

2. Art Director

Der Art Director sollte nicht primär Assets erzeugen.

Seine Hauptaufgabe:

Art Bible
↓
Asset Rules
↓
Visual Review

Zum Beispiel:

perspective: top-down
depth-style: pseudo-isometric
grid-readability: mandatory

shape-language:
  - chunky
  - industrial
  - readable silhouettes
  - low visual noise

building-rules:
  inputs: visually obvious
  outputs: visually obvious
  active-state: animated
  power-state: recognizable

mobile:
  minimum-important-feature-size: defined

Sein Haupttool wäre ComfyUI, aber eher zum Experimentieren mit Stilrichtungen.

Wenn ein guter Stil gefunden wurde:

Art Bible einfrieren.

Danach dürfen Produktionsassets nicht ständig einen neuen Stil ausprobieren.

3. Asset Artist

Hier würde ich ComfyUI + MCP als Hauptwerkzeug nehmen.

Pipeline:

Asset Specification
        ↓
ComfyUI workflow
        ↓
generation
        ↓
background removal
        ↓
normalization
        ↓
human/art-director review
        ↓
technical artist

Ein Asset-Auftrag sollte möglichst strukturiert sein.

Nicht:

Mach einen Smelter.

Sondern:

asset:
  id: smelter_mk1
  type: production_building

grid:
  width: 2
  height: 2

perspective:
  top_down: true
  pseudo_isometric_depth: true

ports:
  input:
    side: left
    count: 1

  output:
    side: right
    count: 1

states:
  - idle
  - working
  - no_power

art_bible: v1

Damit bekommt dein Asset-Agent reproduzierbare Aufgaben.

4. Technical Artist

Den halte ich bei deinem Projekt für extrem wichtig.

Der Asset Artist erzeugt:

smelter.png

Der Technical Artist macht daraus:

smelter.png
↓
Unity Import Settings
↓
Sprite
↓
Pivot
↓
Sorting Layer
↓
Animation
↓
Prefab
↓
Grid footprint
↓
BuildingDefinition

Dafür würde ich wieder Unity MCP nutzen.

Der Agent kann damit beispielsweise automatisiert:

Import texture
Set compression
Set sprite mode
Create animation
Create prefab
Attach BuildingView
Configure grid footprint
Validate prefab

erledigen.

Gerade bei hunderten Assets ist das viel wertvoller als reine Bildgenerierung.

5. Blender erst später

Blender MCP würde ich installieren, aber zunächst wahrscheinlich kaum verwenden.

Blender hat inzwischen einen eigenen MCP-Server, der einem Agenten Zugriff auf die Blender-Python-API gibt.

Das kann für unser Spiel später sehr interessant werden.

Zum Beispiel erzeugst du Maschinen nicht direkt als AI-Bild, sondern als simple 3D-Modelle:

Smelter 3D
↓
fixed camera
↓
fixed lighting
↓
render
↓
2D Sprite

Das hätte einen riesigen Vorteil:

Perspektivische Konsistenz

Alle Gebäude besitzen automatisch:

gleiche Kamera
gleiche Perspektive
gleiche Beleuchtung
gleiche Schatten
gleiche Größenrelation

Das löst eines der größten Probleme generierter 2D-Assets.

Ich könnte mir deshalb langfristig sogar vorstellen:

Concept Art
ComfyUI
     ↓
3D interpretation
Blender Agent
     ↓
standardized render pipeline
     ↓
2D game sprites

Für Version 0.1 aber Overkill.

Und beim Blender MCP unbedingt beachten: Blender weist selbst darauf hin, dass vom LLM erzeugter Python-Code ohne Schutzmechanismen ausgeführt werden kann; dafür wäre eine isolierte Umgebung sinnvoll.

6. Audio Agent

Audio würde ich nochmals teilen.

Sound Designer

Erzeugt:

machine loops
belt sounds
clicks
construction
upgrade
power-down
research complete
resource pickup
UI feedback

Hier würde ich zuerst kostenlose Libraries + lokale Generierung + prozedurale Bearbeitung verwenden.

Der Agent braucht nicht zwingend einen spezialisierten MCP.

Er kann Audio-Dateien erzeugen/bearbeiten und anschließend über den Unity MCP importieren.

Struktur beispielsweise:

Assets/Audio/

Machines/
    miner_loop_01.wav
    smelter_loop_01.wav

UI/
    confirm_01.wav
    error_01.wav

Factory/
    belt_loop_01.wav

Environment/
    region_grasslands_ambience.wav
Composer

Musik würde ich erst später angehen.

Da lohnt sich möglicherweise ein kostenpflichtiger Dienst eher als bei kleinen Soundeffekten.

ElevenLabs bietet inzwischen einen gehosteten MCP und hat seinen alten lokalen MCP eingestellt.

Ich würde ElevenLabs deshalb als Fallback, nicht als Grundvoraussetzung einsetzen.

7. QA Agent

Hier brauchst du eigentlich überhaupt kein zusätzliches Produkt.

Der QA-Agent bekommt:

Unity MCP + Test-Projekt + Builds.

Seine Rolle wäre beispielsweise:

Feature Spec
      ↓
derive acceptance tests
      ↓
run edit-mode tests
      ↓
run play-mode tests
      ↓
inspect console
      ↓
create test factory
      ↓
simulate 1h / 24h / 7d
      ↓
report violations

Gerade unser Game eignet sich hervorragend für automatisierte Simulationstests.

Zum Beispiel:

Factory:
Miner 60/min
↓
Smelter consumes 30/min

Run:
24 hours

QA prüft dann:

Expected bottleneck: Smelter
Expected ore storage full: X
Expected production stop: Y

Das wird später wichtiger sein als klassisches „Charakter springt richtig“.

8. Performance Agent

Den würde ich sogar separat vorsehen.

Nicht dauerhaft aktiv, sondern für Reviews.

Er bekommt einen Auftrag:

Generate factory:
10,000 machines
30,000 belt tiles

Simulate:
24 hours

Render:
mobile target

Measure:
simulation time
frame time
GC allocations
memory

Dann soll er nicht automatisch alles optimieren, sondern dir ein Decision Brief liefern:

Problem:
Simulation tick takes 18ms.

Cause:
Graph recalculated entirely each tick.

Option A:
Cache connectivity graph.

Expected:
~70% reduction.

Complexity:
Low.

Recommendation:
Implement.

Das passt hervorragend zu deiner Constitution.

9. Rollen sollten unterschiedliche Berechtigungen haben

Ich würde nicht:

Every Agent
→ every MCP

machen.

Sondern:

Game Designer
├── Specs
└── Research

Architect
├── Source
├── Tests
└── Unity read access

Developer
├── Source
├── Unity MCP
└── Tests

Art Director
├── Art Bible
└── ComfyUI

Asset Artist
├── ComfyUI
└── Assets/Source

Technical Artist
├── Assets
└── Unity MCP

Audio
├── Audio tools
└── Assets/Audio

QA
├── Unity MCP
├── Tests
└── Builds

Damit minimierst du Agent-Chaos.

10. Meine minimale Toolchain zum Start

Ich würde nicht alles sofort installieren.

Für Phase 1 reichen:

Git
│
├── Spec Kit
│
├── Unity 6
│
├── Unity MCP
│
└── ComfyUI

Damit kannst du bereits:

Design
↓
Implement
↓
Generate placeholder graphics
↓
Import automatically
↓
Test

abdecken.

Erst danach:

Blender MCP
Audio generation
Music provider
Cloud backend
Analytics
etc.

Das spart enorm viel Setup-Aufwand.

Mein vorgeschlagenes Agent-Team

Für dein Projekt würde ich zunächst tatsächlich nur 7 Rollen definieren:

                    YOU
                     │
                Human-in-loop
                     │
            ┌────────┴─────────┐
            │                  │
      Game Designer      Software Architect
            │                  │
            └────────┬─────────┘
                     │
             Gameplay Developer
                     │
          ┌──────────┴──────────┐
          │                     │
    Technical Artist           QA
          │
     ┌────┴────┐
     │         │
Art Director  Asset Artist

Audio würde zunächst eine achte Rolle werden, sobald die ersten funktionierenden Factory-Szenen existieren.

Tools dahinter
Game Designer
└── Spec Kit

Software Architect
├── Source Code
└── Unity MCP

Gameplay Developer
├── Source Code
├── Unity MCP
└── Tests

Art Director
└── ComfyUI MCP

Asset Artist
└── ComfyUI MCP

Technical Artist
├── Unity MCP
└── Asset Files

QA
└── Unity MCP

Audio Designer
├── Local Audio Tools
├── optional ElevenLabs
└── Unity MCP