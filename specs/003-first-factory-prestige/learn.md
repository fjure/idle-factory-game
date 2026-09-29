# What I Learned: First Iron Factory and Prestige Loop

**Feature**: First Iron Factory and Prestige Loop
**Generated**: 2026-09-29
**Scope**: T093 / Issue #175 — checkpoint generation extraction
**Implementation status**: 7/53 tasks completed; 5/46 implementation Issues closed

---

## Key Decisions

### 1. Share the generation transaction, inject the save format

**What we did**: `CheckpointGenerationFiles<TCheckpoint>` owns the durable pending → read-back → retain-previous → promote → verify protocol. An `ICheckpointGenerationCodec<TCheckpoint>` supplies format-specific serialization and validation, and `JsonCheckpointStore` delegates to the shared protocol.

**Why**: Save V1 and Save V2 need the same crash-safe slot protocol even though their bytes and validation rules differ. Keeping one transaction implementation avoids letting the formats drift on interruption and recovery behavior.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Copy the V1 store into a new V2 store | Two copies could disagree about promotion or recovery after a crash. |
| Put JSON parsing inside the generation store | That would couple durability mechanics to one format and make protocol tests depend on serialization. |

**When you'd choose differently**: Separate stores make sense if formats genuinely require different durability guarantees or storage media. Here they share the same slot protocol.

### 2. Give the legacy runtime source read capability only

**What we did**: `SaveV1CheckpointSource` uses `CheckpointGenerationReader<Checkpoint>` through `ICheckpointStorageReader`. The old `JsonCheckpointStore` keeps its writer for compatibility suites, but Feature 003 runtime import uses the reader-only source.

**Why**: The V1 file is migration input that must remain intact. A narrow interface makes accidental writes unavailable through the import source, while the legacy adapter preserves existing tests and file compatibility.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Pass the full read/write backend to the importer | The importer would have write capability it never needs. |
| Delete the V1 writer during extraction | Existing compatibility suites still characterize the old format and behavior. |

**When you'd choose differently**: A migration that intentionally upgrades files in place would need a writer, but that write should be an explicit migration operation with its own recovery contract.

### 3. Distinguish a missing save from a failed filesystem query

**What we did**: `SystemIoCheckpointStorageReader.Exists` uses `File.GetAttributes`; only file-not-found and directory-not-found mean absence. Access or query errors flow to the persistence-failure result.

**Why**: Absence permits cold start, while an inaccessible save must stop import with a typed failure. Treating every failed existence check as absence could silently skip a recoverable legacy save.

**Alternatives considered**:

| Approach | Why it wasn't chosen |
|----------|---------------------|
| Use `Directory.Exists` as an absence check | It can collapse filesystem query failures into `false`, hiding the difference between absent and inaccessible storage. |
| Create the directory while checking it | A read-only import should not mutate legacy storage just to determine whether data exists. |

**When you'd choose differently**: A non-authoritative cache can treat an inaccessible path as a cache miss. Durable player saves need an explicit failure.

---

## Concepts to Know

### Capability interfaces

**What it is**: An interface can describe exactly which operations a component is allowed to perform. `ICheckpointStorageReader` exposes reads and metadata, while write operations live on the wider backend interface.

**Where we used it**: `CheckpointGenerationReader<TCheckpoint>` and `SaveV1CheckpointSource` in `Assets/IdleFactory/Unity/Persistence/CheckpointGenerationFiles.cs` and `SaveV1CheckpointSource.cs`.

**Why it matters**: A read-only migration boundary is easier to audit when the source cannot call write operations at all; a test fake can enforce the same rule.

### Crash-safe promotion

**What it is**: A durable update is staged and verified before the active pointer changes, while the previous valid generation is retained for recovery.

**Where we used it**: `CheckpointGenerationFiles<TCheckpoint>.Promote` and the two-manifest selection logic.

**Why it matters**: A process or device failure at any intermediate step should leave a readable generation or a retryable operation, rather than a half-published checkpoint.

### Characterization tests

**What it is**: A characterization test records behavior that must stay compatible while internals are refactored.

**Where we used it**: `CheckpointGenerationFilesTests.cs` checks manifest selection and byte hashes across the six committed Save V1 fixtures; `SaveV1Mapper.cs` and the fixtures stay untouched.

**Why it matters**: The refactor can change where the protocol lives while still proving that existing V1 data selects and decodes the same way.

---

## Architecture Overview

The Unity persistence layer owns the filesystem protocol, while a format codec owns checkpoint bytes. `JsonCheckpointStore` remains the legacy writer adapter; `SaveV1CheckpointSource` composes the shared reader with the V1 codec and exposes the Core `ILegacyCheckpointSource` port. This keeps migration read-only and leaves the domain model independent of file operations.
