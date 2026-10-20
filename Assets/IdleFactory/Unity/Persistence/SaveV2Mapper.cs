using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using IdleFactory.Core.Numerics;
using IdleFactory.Run.Content;
using IdleFactory.Run.Domain;
using Newtonsoft.Json;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>
    /// Save V2 codec (T104, contracts/save-v2-and-v1-import.md). The reader is a strict token walk: a lexical pre-pass
    /// (size, BOM and UTF-8, depth, canonical integers, no escapes) followed by a Newtonsoft <see cref="JsonTextReader"/>
    /// walk that tracks the members seen per object and rejects a duplicate at token level (SEC-3), never a reflection
    /// deserialization. The structurally valid DTO is then turned into domain state through the constructors and
    /// restore paths, and the cross-field bounds of load rule 4 are checked (SEC-2). Every failure is a static
    /// <see cref="SaveV2Codes"/> code (SEC-5); nothing taken from the file reaches a code, a message or a diagnostic.
    ///
    /// Interpretations where the contract is silent (each is reported to the coordinator):
    /// (1) Optional members (<c>reconciledFromCheckpointId</c>, <c>import</c>, <c>run.previousRunId</c>,
    /// <c>contracts.active</c>, <c>active.deadlineUtcMs</c> and the two Overdrive instants) are always present and are
    /// <c>null</c> when absent, like Save V1's <c>reconciledFromCheckpointId</c>; a missing member is a structure error.
    /// (2) Every list keyed by an ID is written in ordinal order and read strictly ascending (one canonical form, and
    /// a duplicate is rejected); <c>machines</c> are ascending by slot. <c>run.unlocked</c> is the exception: it is in
    /// unlock order (initial set, then unlock order, T140), unique but not sorted. <c>run.priority</c> is any
    /// permutation of the machine IDs.
    /// (3) Strings contain no JSON escapes and no control characters. Every string is a UUID or a catalog ID, so the
    /// writer never needs an escape; a state that would need one is not written.
    /// (4) UUIDs are the lowercase 36-character hyphenated form and never all zero.
    /// (5) <c>run.overdrive</c> and the timers restore exactly as saved. The R03 rescale of a machine's progress when
    /// the effective cycle differs from the saved <c>cycleDurationTicks</c> (load rule 2) needs the composed modifiers
    /// and belongs to the load and offline-return step, not to this mapper.
    /// (6) A fresh, imported or prestige-reset state is not a fixed point of the unlock pass until its first settle
    /// (T140). The mapper restores the saved state as it is and settles nothing; a snapshot taken before the first
    /// settle may list a stale locked set, and it is written and read back unchanged.
    /// (7) <c>run.unlocked</c> may contain the reserved ID <c>overdrive</c> (<see cref="RunState.OverdriveUnlockId"/>).
    /// (8) An active contract's <c>requiredUnits</c> is fixed at acceptance and is authoritative: it is only checked
    /// to be in [1, 10^18], so retuning a quantity curve stays save-compatible (load rule 2). An Item effect's kind
    /// and target must be the Item's own (<c>factory</c>, or an allowed machine type); that is an invariant
    /// (<c>save.v2.invariant</c>), not a rule-4 bound, and has no corrupt fixture.
    /// (9) <c>import.sourceCheckpointId</c> is a canonical lowercase hyphenated UUID like the other UUIDs. An
    /// <c>offlinePolicyRevision</c> that the catalog does not have is reported as <c>save.v2.version</c>.
    ///
    /// Save V3 (T334, specs/005-economy-contracts/contracts/save-v3.md): the mapper is version-parametrized. A peek reads the
    /// root <c>schemaVersion</c> first (2 or 3; any other value is <c>save.v2.version</c>) and selects that version's member
    /// tables; the walk is then strict for that version (an unknown, duplicate or missing member is <c>save.v2.structure</c>).
    /// V3 adds, in the closed list of the contract: <c>run.runCompletionTotal</c>, <c>run.contractCooldowns[]</c> and
    /// <c>run.bonusOffers[]</c> (run-level members, appended after <c>overdrive</c>), and <c>autoDeliver</c> and <c>produced</c>
    /// on <c>run.contracts.active</c> (appended after <c>requiredUnits</c>). Both lists are strictly ascending by offer ID (a
    /// duplicate is <c>save.v2.invariant</c>). A V2 file loads with the V2 defaults: auto-deliver off, nothing produced, no
    /// cooldowns, no bonus state, and the run-wide total derived from the counts. <see cref="Serialize(SaveV2Meta, RunState, PermanentState)"/>
    /// always writes V3; the internal versioned overload writes V2 only for the regression fixtures and for
    /// <see cref="SaveV2CheckpointCodec.ValidateBytes"/>, which compares against the version it read, and it refuses a state
    /// that V2 cannot represent instead of dropping data. Content drift (dropping contract-scoped entries of removed offers,
    /// <c>contract.dropped</c>) is T335: until then such an entry is <c>save.v2.id.unresolved</c>, like a completion count.
    /// </summary>
    public static class SaveV2Mapper
    {
        /// <summary>The version the writer writes (Save V3, T334).</summary>
        public const int SchemaVersion = 3;

        /// <summary>The oldest version the reader accepts (Save V2); it is never written by the store.</summary>
        public const int LegacySchemaVersion = 2;
        public const int MaximumByteCount = 1024 * 1024;
        public const int MaximumDepth = 16;
        public const int MaximumMachineCount = 64;
        public const int MaximumListEntryCount = 256;
        public const int MaximumIdentifierLength = 128;

        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private static readonly string[] RootMembers =
        {
            "schemaVersion", "checkpointId", "generation", "departureUtcMs", "offlinePolicyRevision",
            "reconciledFromCheckpointId", "contentRevision", "import", "permanent", "run",
        };
        private static readonly string[] ImportMembers = { "sourceSchemaVersion", "sourceCheckpointId", "sourceGeneration" };
        private static readonly string[] PermanentMembers = { "ip", "prestigeCount", "industry", "items" };
        private static readonly string[] IdLevelMembers = { "id", "level" };
        private static readonly string[] ItemMembers = { "id", "count" };
        private static readonly string[] RunMembersV2 =
        {
            "runId", "runNumber", "previousRunId", "cash", "rp", "storage", "factory", "inventory", "machines", "priority",
            "autoSell", "unlocked", "contracts", "research", "effects", "overdrive",
        };
        private static readonly string[] RunMembersV3 =
        {
            "runId", "runNumber", "previousRunId", "cash", "rp", "storage", "factory", "inventory", "machines", "priority",
            "autoSell", "unlocked", "contracts", "research", "effects", "overdrive",
            "runCompletionTotal", "contractCooldowns", "bonusOffers",
        };
        private static readonly string[] ExpansionMembers = { "level", "investedCash" };
        private static readonly string[] InventoryMembers = { "resourceId", "units" };
        private static readonly string[] MachineMembers =
        {
            "instanceId", "machineId", "slot", "level", "isActive", "progressTicks", "cycleDurationTicks", "investedCash",
        };
        private static readonly string[] ContractsMembers = { "completions", "active" };
        private static readonly string[] CompletionMembers = { "offerId", "count" };
        private static readonly string[] ActiveMembersV2 = { "offerId", "acceptedUtcMs", "deadlineUtcMs", "requiredUnits" };
        private static readonly string[] ActiveMembersV3 = { "offerId", "acceptedUtcMs", "deadlineUtcMs", "requiredUnits", "autoDeliver", "produced" };
        private static readonly string[] ResearchMembers = { "id", "level" };
        private static readonly string[] EffectMembers = { "itemId", "effectKind", "targetKey", "startUtcMs", "endUtcMs" };
        private static readonly string[] OverdriveMembers = { "activeUntilUtcMs", "cooldownUntilUtcMs" };
        private static readonly string[] CooldownMembers = { "offerId", "readyAtUtcMs" };
        private static readonly string[] BonusMembers = { "offerId", "state", "windowEndUtcMs" };
        private const string BonusTriggered = "triggered";
        private const string BonusConsumed = "consumed";

        /// <summary>The member tables of one schema version, chosen at the <c>schemaVersion</c> peek (T334).</summary>
        private sealed class MemberTables
        {
            public MemberTables(int version, string[] run, string[] active)
            {
                Version = version;
                Run = run;
                Active = active;
            }

            public int Version { get; }
            public string[] Run { get; }
            public string[] Active { get; }
            public bool IsV3 => Version >= 3;
        }

        private static readonly MemberTables TablesV2 = new MemberTables(LegacySchemaVersion, RunMembersV2, ActiveMembersV2);
        private static readonly MemberTables TablesV3 = new MemberTables(SchemaVersion, RunMembersV3, ActiveMembersV3);

        private static MemberTables TablesFor(long version)
        {
            if (version == LegacySchemaVersion) return TablesV2;
            if (version == SchemaVersion) return TablesV3;
            throw new SaveV2Exception(SaveV2Codes.Version);
        }

        // ------------------------------------------------------------------------------------------------------
        // Read
        // ------------------------------------------------------------------------------------------------------

        /// <summary>Reads and validates one Save V2 file against <paramref name="catalog"/>; fails closed with a static code.</summary>
        public static SaveV2ReadResult Deserialize(byte[] bytes, RunCatalog catalog)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumByteCount) return Reject(SaveV2Codes.Size);
            if (catalog == null) return Reject(SaveV2Codes.Invariant);

            SaveV2Dto dto;
            try
            {
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                    throw new SaveV2Exception(SaveV2Codes.Encoding);
                string text;
                try
                {
                    text = StrictUtf8.GetString(bytes);
                }
                catch (DecoderFallbackException)
                {
                    throw new SaveV2Exception(SaveV2Codes.Encoding);
                }

                ValidateLexical(text);
                dto = ReadDocument(text, TablesFor(PeekSchemaVersion(text)));
            }
            catch (SaveV2Exception exception)
            {
                return Reject(exception.Code);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                return Reject(SaveV2Codes.Structure);
            }

            try
            {
                return SaveV2ReadResult.Success(Build(dto, catalog));
            }
            catch (SaveV2Exception exception)
            {
                return Reject(exception.Code);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                return Reject(SaveV2Codes.Invariant);
            }
        }

        private static SaveV2ReadResult Reject(string code)
        {
            CheckpointDiagnostics.Report("SaveV2.Deserialize", code);
            return SaveV2ReadResult.Rejected(code);
        }

        private static SaveV2WriteResult RejectWrite(string code)
        {
            CheckpointDiagnostics.Report("SaveV2.Serialize", code);
            return SaveV2WriteResult.Rejected(code);
        }

        /// <summary>
        /// The lexical pre-pass over the decoded text. It enforces the limits that the token walk cannot: nesting depth
        /// (exactly 16), canonical integers (no float, exponent, leading zero, <c>-0</c> or sign), whitespace of JSON
        /// only, no escapes or control characters in strings, no trailing comma, and only <c>true</c>, <c>false</c> and
        /// <c>null</c> as literals. It rejects an integer above 19 digits without parsing it.
        /// </summary>
        private static void ValidateLexical(string text)
        {
            int depth = 0;
            char previous = '\0';
            int i = 0;
            int n = text.Length;
            while (i < n)
            {
                char c = text[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                {
                    i++;
                    continue;
                }

                switch (c)
                {
                    case '{':
                    case '[':
                        depth++;
                        if (depth > MaximumDepth) throw new SaveV2Exception(SaveV2Codes.Limit);
                        previous = c;
                        i++;
                        break;
                    case '}':
                    case ']':
                        if (previous == ',') throw new SaveV2Exception(SaveV2Codes.Structure);
                        depth--;
                        if (depth < 0) throw new SaveV2Exception(SaveV2Codes.Structure);
                        previous = c;
                        i++;
                        break;
                    case ',':
                        if (previous == ',' || previous == '{' || previous == '[' || previous == ':' || previous == '\0')
                            throw new SaveV2Exception(SaveV2Codes.Structure);
                        previous = c;
                        i++;
                        break;
                    case ':':
                        previous = c;
                        i++;
                        break;
                    case '"':
                        i++;
                        while (true)
                        {
                            if (i >= n) throw new SaveV2Exception(SaveV2Codes.Structure);
                            char s = text[i];
                            if (s == '"')
                            {
                                i++;
                                break;
                            }

                            if (s == '\\' || s < 0x20) throw new SaveV2Exception(SaveV2Codes.Structure);
                            i++;
                        }

                        previous = '"';
                        break;
                    case 't':
                        i = ScanLiteral(text, i, "true");
                        previous = 'l';
                        break;
                    case 'f':
                        i = ScanLiteral(text, i, "false");
                        previous = 'l';
                        break;
                    case 'n':
                        i = ScanLiteral(text, i, "null");
                        previous = 'l';
                        break;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                        {
                            i = ScanInteger(text, i);
                            previous = '0';
                            break;
                        }

                        throw new SaveV2Exception(SaveV2Codes.Structure);
                }
            }

            if (depth != 0) throw new SaveV2Exception(SaveV2Codes.Structure);
        }

        private static int ScanLiteral(string text, int start, string literal)
        {
            if (string.CompareOrdinal(text, start, literal, 0, literal.Length) != 0)
                throw new SaveV2Exception(SaveV2Codes.Structure);
            int end = start + literal.Length;
            if (end < text.Length)
            {
                char d = text[end];
                if (d != ' ' && d != '\t' && d != '\n' && d != '\r' && d != ',' && d != '}' && d != ']')
                    throw new SaveV2Exception(SaveV2Codes.Structure);
            }

            return end;
        }

        private static int ScanInteger(string text, int start)
        {
            int n = text.Length;
            int i = start;
            bool negative = text[i] == '-';
            if (negative) i++;
            if (i >= n || text[i] < '0' || text[i] > '9') throw new SaveV2Exception(SaveV2Codes.Structure);
            if (text[i] == '0')
            {
                i++;
            }
            else
            {
                while (i < n && text[i] >= '0' && text[i] <= '9') i++;
            }

            if (i < n)
            {
                char d = text[i];
                // A leading zero followed by a digit, a fraction, an exponent or a stray sign is not a canonical integer.
                if ((d >= '0' && d <= '9') || d == '.' || d == 'e' || d == 'E' || d == '+' || d == '-')
                    throw new SaveV2Exception(SaveV2Codes.Structure);
            }

            int digits = i - start - (negative ? 1 : 0);
            if (negative && digits == 1 && text[start + 1] == '0') throw new SaveV2Exception(SaveV2Codes.Structure);
            if (digits > 19) throw new SaveV2Exception(SaveV2Codes.Bounds);
            return i;
        }

        private static JsonTextReader NewReader(StringReader stringReader) => new JsonTextReader(stringReader)
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal,
            MaxDepth = 64,
            SupportMultipleContent = false,
        };

        /// <summary>
        /// The <c>schemaVersion</c> peek (T334): finds the root member wherever it stands, skipping the other root values unread, and
        /// returns the integer. A missing member or a non-integer is <c>save.v2.structure</c>, an integer above 10^18
        /// <c>save.v2.bounds</c>, exactly as the full walk reports them; the caller maps the value to its member tables.
        /// </summary>
        private static long PeekSchemaVersion(string text)
        {
            using (var stringReader = new StringReader(text))
            using (JsonTextReader reader = NewReader(stringReader))
            {
                ReadNext(reader);
                if (reader.TokenType != JsonToken.StartObject) throw new SaveV2Exception(SaveV2Codes.Structure);
                while (true)
                {
                    ReadNext(reader);
                    if (reader.TokenType != JsonToken.PropertyName) throw new SaveV2Exception(SaveV2Codes.Structure);
                    string name = (string)reader.Value;
                    ReadNext(reader);
                    if (string.Equals(name, "schemaVersion", StringComparison.Ordinal)) return ReadInteger(reader, int.MinValue, long.MaxValue);
                    reader.Skip();
                }
            }
        }

        /// <summary>
        /// The schema version of a candidate file without building it (<see cref="SaveV2CheckpointCodec.ValidateBytes"/>): false for
        /// bytes that are not strict UTF-8 JSON with a root integer <c>schemaVersion</c> of 2 or 3.
        /// </summary>
        internal static bool TryPeekSchemaVersion(byte[] bytes, out int version)
        {
            version = 0;
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumByteCount) return false;
            try
            {
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return false;
                string text = StrictUtf8.GetString(bytes);
                ValidateLexical(text);
                version = TablesFor(PeekSchemaVersion(text)).Version;
                return true;
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                version = 0;
                return false;
            }
        }

        private static SaveV2Dto ReadDocument(string text, MemberTables tables)
        {
            using (var stringReader = new StringReader(text))
            using (JsonTextReader reader = NewReader(stringReader))
            {
                var dto = new SaveV2Dto { SchemaVersion = tables.Version };
                ReadNext(reader);
                ReadObject(reader, RootMembers, index =>
                {
                    switch (index)
                    {
                        case 0:
                            // The peek chose the tables from this value; reading it again keeps the walk strict (it must still be one integer).
                            if (ReadInteger(reader, int.MinValue, long.MaxValue) != tables.Version)
                                throw new SaveV2Exception(SaveV2Codes.Version);
                            break;
                        case 1: dto.CheckpointId = ReadString(reader); break;
                        case 2: dto.Generation = ReadQuantity(reader); break;
                        case 3: dto.DepartureUtcMs = ReadQuantity(reader); break;
                        case 4: dto.OfflinePolicyRevision = (int)ReadInteger(reader, 1, int.MaxValue); break;
                        case 5: dto.ReconciledFromCheckpointId = ReadNullableString(reader); break;
                        case 6: dto.ContentRevision = (int)ReadInteger(reader, 0, int.MaxValue); break;
                        case 7: dto.Import = IsNull(reader) ? null : ReadImport(reader); break;
                        case 8: dto.Permanent = ReadPermanent(reader); break;
                        default: dto.Run = ReadRun(reader, tables); break;
                    }
                });

                if (reader.Read()) throw new SaveV2Exception(SaveV2Codes.Structure);
                return dto;
            }
        }

        private static ImportV2Dto ReadImport(JsonTextReader reader)
        {
            var dto = new ImportV2Dto();
            ReadObject(reader, ImportMembers, index =>
            {
                switch (index)
                {
                    case 0: dto.SourceSchemaVersion = (int)ReadInteger(reader, 0, int.MaxValue); break;
                    case 1: dto.SourceCheckpointId = ReadString(reader); break;
                    default: dto.SourceGeneration = ReadQuantity(reader); break;
                }
            });
            return dto;
        }

        private static PermanentV2Dto ReadPermanent(JsonTextReader reader)
        {
            var dto = new PermanentV2Dto();
            ReadObject(reader, PermanentMembers, index =>
            {
                switch (index)
                {
                    case 0: dto.Ip = ReadQuantity(reader); break;
                    case 1: dto.PrestigeCount = ReadQuantity(reader); break;
                    case 2: dto.Industry = ReadIdValueList(reader, IdLevelMembers, 1); break;
                    default: dto.Items = ReadIdValueList(reader, ItemMembers, 1); break;
                }
            });
            return dto;
        }

        private static RunV2Dto ReadRun(JsonTextReader reader, MemberTables tables)
        {
            var dto = new RunV2Dto();
            ReadObject(reader, tables.Run, index =>
            {
                switch (index)
                {
                    case 0: dto.RunId = ReadString(reader); break;
                    case 1: dto.RunNumber = ReadInteger(reader, 1, int.MaxValue); break;
                    case 2: dto.PreviousRunId = ReadNullableString(reader); break;
                    case 3: dto.Cash = ReadQuantity(reader); break;
                    case 4: dto.Rp = ReadQuantity(reader); break;
                    case 5: dto.Storage = ReadExpansion(reader); break;
                    case 6: dto.Factory = ReadExpansion(reader); break;
                    case 7: dto.Inventory = ReadIdValueList(reader, InventoryMembers, 1); break;
                    case 8: dto.Machines = ReadList(reader, MaximumMachineCount, () => ReadMachine(reader)); break;
                    case 9: dto.Priority = ReadList(reader, MaximumMachineCount, () => ReadString(reader)); break;
                    case 10: dto.AutoSell = ReadList(reader, MaximumListEntryCount, () => ReadString(reader)); break;
                    case 11: dto.Unlocked = ReadList(reader, MaximumListEntryCount, () => ReadString(reader)); break;
                    case 12: dto.Contracts = ReadContracts(reader, tables); break;
                    case 13: dto.Research = ReadIdValueList(reader, ResearchMembers, 1); break;
                    case 14: dto.Effects = ReadList(reader, MaximumListEntryCount, () => ReadEffect(reader)); break;
                    case 15: dto.Overdrive = ReadOverdrive(reader); break;
                    // Save V3 members (only in the V3 table, so a V2 file never reaches them).
                    case 16: dto.RunCompletionTotal = ReadQuantity(reader); break;
                    case 17: dto.ContractCooldowns = ReadIdValueList(reader, CooldownMembers, 0); break;
                    default: dto.BonusOffers = ReadList(reader, MaximumListEntryCount, () => ReadBonusOffer(reader)); break;
                }
            });
            return dto;
        }

        private static BonusOfferV3Dto ReadBonusOffer(JsonTextReader reader)
        {
            var dto = new BonusOfferV3Dto();
            ReadObject(reader, BonusMembers, index =>
            {
                switch (index)
                {
                    case 0: dto.OfferId = ReadString(reader); break;
                    case 1:
                        string state = ReadString(reader);
                        if (string.Equals(state, BonusTriggered, StringComparison.Ordinal)) dto.Consumed = false;
                        else if (string.Equals(state, BonusConsumed, StringComparison.Ordinal)) dto.Consumed = true;
                        else throw new SaveV2Exception(SaveV2Codes.Structure);
                        break;
                    default: dto.WindowEndUtcMs = ReadQuantity(reader); break;
                }
            });
            return dto;
        }

        private static ExpansionV2Dto ReadExpansion(JsonTextReader reader)
        {
            var dto = new ExpansionV2Dto();
            ReadObject(reader, ExpansionMembers, index =>
            {
                if (index == 0) dto.Level = ReadInteger(reader, 1, int.MaxValue);
                else dto.InvestedCash = ReadQuantity(reader);
            });
            return dto;
        }

        private static MachineV2Dto ReadMachine(JsonTextReader reader)
        {
            var dto = new MachineV2Dto();
            ReadObject(reader, MachineMembers, index =>
            {
                switch (index)
                {
                    case 0: dto.InstanceId = ReadString(reader); break;
                    case 1: dto.MachineId = ReadString(reader); break;
                    case 2: dto.Slot = ReadInteger(reader, 0, int.MaxValue); break;
                    case 3: dto.Level = ReadInteger(reader, 1, int.MaxValue); break;
                    case 4: dto.IsActive = ReadBoolean(reader); break;
                    case 5: dto.ProgressTicks = ReadQuantity(reader); break;
                    case 6: dto.CycleDurationTicks = ReadInteger(reader, 1, ExactBound.Value); break;
                    default: dto.InvestedCash = ReadQuantity(reader); break;
                }
            });
            return dto;
        }

        private static ContractsV2Dto ReadContracts(JsonTextReader reader, MemberTables tables)
        {
            var dto = new ContractsV2Dto();
            ReadObject(reader, ContractsMembers, index =>
            {
                if (index == 0) dto.Completions = ReadIdValueList(reader, CompletionMembers, 1);
                else dto.Active = IsNull(reader) ? null : ReadActiveContract(reader, tables);
            });
            return dto;
        }

        private static ActiveContractV2Dto ReadActiveContract(JsonTextReader reader, MemberTables tables)
        {
            var dto = new ActiveContractV2Dto();
            ReadObject(reader, tables.Active, index =>
            {
                switch (index)
                {
                    case 0: dto.OfferId = ReadString(reader); break;
                    case 1: dto.AcceptedUtcMs = ReadQuantity(reader); break;
                    case 2: dto.DeadlineUtcMs = IsNull(reader) ? (long?)null : ReadQuantity(reader); break;
                    case 3: dto.RequiredUnits = ReadInteger(reader, 1, ExactBound.Value); break;
                    // Save V3 members (only in the V3 table).
                    case 4: dto.AutoDeliver = ReadBoolean(reader); break;
                    default: dto.Produced = ReadQuantity(reader); break;
                }
            });
            return dto;
        }

        private static EffectV2Dto ReadEffect(JsonTextReader reader)
        {
            var dto = new EffectV2Dto();
            ReadObject(reader, EffectMembers, index =>
            {
                switch (index)
                {
                    case 0: dto.ItemId = ReadString(reader); break;
                    case 1: dto.EffectKind = ReadString(reader); break;
                    case 2: dto.TargetKey = ReadString(reader); break;
                    case 3: dto.StartUtcMs = ReadQuantity(reader); break;
                    default: dto.EndUtcMs = ReadQuantity(reader); break;
                }
            });
            return dto;
        }

        private static OverdriveV2Dto ReadOverdrive(JsonTextReader reader)
        {
            var dto = new OverdriveV2Dto();
            ReadObject(reader, OverdriveMembers, index =>
            {
                long? value = IsNull(reader) ? (long?)null : ReadQuantity(reader);
                if (index == 0) dto.ActiveUntilUtcMs = value;
                else dto.CooldownUntilUtcMs = value;
            });
            return dto;
        }

        private static List<IdValueV2Dto> ReadIdValueList(JsonTextReader reader, string[] members, long minimumValue)
        {
            return ReadList(reader, MaximumListEntryCount, () =>
            {
                string id = null;
                long value = 0;
                ReadObject(reader, members, index =>
                {
                    if (index == 0) id = ReadString(reader);
                    else value = ReadInteger(reader, minimumValue, ExactBound.Value);
                });
                return new IdValueV2Dto(id, value);
            });
        }

        // The token-walk primitives. Convention: the reader is positioned on the first token of the value.

        private static void ReadObject(JsonTextReader reader, string[] members, Action<int> readMember)
        {
            if (reader.TokenType != JsonToken.StartObject) throw new SaveV2Exception(SaveV2Codes.Structure);
            var seen = new bool[members.Length];
            int count = 0;
            while (true)
            {
                ReadNext(reader);
                if (reader.TokenType == JsonToken.EndObject) break;
                if (reader.TokenType != JsonToken.PropertyName) throw new SaveV2Exception(SaveV2Codes.Structure);
                string name = (string)reader.Value;
                int index = -1;
                for (int i = 0; i < members.Length; i++)
                {
                    if (string.Equals(members[i], name, StringComparison.Ordinal))
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0 || seen[index]) throw new SaveV2Exception(SaveV2Codes.Structure);
                seen[index] = true;
                count++;
                ReadNext(reader);
                readMember(index);
            }

            if (count != members.Length) throw new SaveV2Exception(SaveV2Codes.Structure);
        }

        private static List<T> ReadList<T>(JsonTextReader reader, int maximum, Func<T> readElement)
        {
            if (reader.TokenType != JsonToken.StartArray) throw new SaveV2Exception(SaveV2Codes.Structure);
            var list = new List<T>();
            while (true)
            {
                ReadNext(reader);
                if (reader.TokenType == JsonToken.EndArray) return list;
                if (list.Count == maximum) throw new SaveV2Exception(SaveV2Codes.Limit);
                list.Add(readElement());
            }
        }

        private static void ReadNext(JsonTextReader reader)
        {
            if (!reader.Read() || reader.TokenType == JsonToken.Comment) throw new SaveV2Exception(SaveV2Codes.Structure);
        }

        private static bool IsNull(JsonTextReader reader) => reader.TokenType == JsonToken.Null;

        private static long ReadInteger(JsonTextReader reader, long minimum, long maximum)
        {
            if (reader.TokenType != JsonToken.Integer) throw new SaveV2Exception(SaveV2Codes.Structure);
            object raw = reader.Value;
            if (raw is BigInteger) throw new SaveV2Exception(SaveV2Codes.Bounds);
            if (!(raw is long value)) throw new SaveV2Exception(SaveV2Codes.Structure);
            if (value < -ExactBound.Value || value > ExactBound.Value) throw new SaveV2Exception(SaveV2Codes.Bounds);
            if (value < minimum || value > maximum) throw new SaveV2Exception(SaveV2Codes.Bounds);
            return value;
        }

        /// <summary>An exact quantity or instant: an integer in [0, 10^18].</summary>
        private static long ReadQuantity(JsonTextReader reader) => ReadInteger(reader, 0, ExactBound.Value);

        private static bool ReadBoolean(JsonTextReader reader)
        {
            if (reader.TokenType != JsonToken.Boolean) throw new SaveV2Exception(SaveV2Codes.Structure);
            return (bool)reader.Value;
        }

        private static string ReadString(JsonTextReader reader)
        {
            if (reader.TokenType != JsonToken.String) throw new SaveV2Exception(SaveV2Codes.Structure);
            string value = (string)reader.Value;
            if (value.Length == 0) throw new SaveV2Exception(SaveV2Codes.Structure);
            if (value.Length > MaximumIdentifierLength) throw new SaveV2Exception(SaveV2Codes.Limit);
            return value;
        }

        private static string ReadNullableString(JsonTextReader reader) => IsNull(reader) ? null : ReadString(reader);

        // ------------------------------------------------------------------------------------------------------
        // Build: DTO -> domain state (constructors and restore paths, then the cross-field bounds)
        // ------------------------------------------------------------------------------------------------------

        private static SaveV2Document Build(SaveV2Dto dto, RunCatalog catalog)
        {
            Guid checkpointId = ParseUuid(dto.CheckpointId);
            Guid? reconciled = dto.ReconciledFromCheckpointId == null ? (Guid?)null : ParseUuid(dto.ReconciledFromCheckpointId);
            if (reconciled.HasValue && reconciled.Value == checkpointId) throw new SaveV2Exception(SaveV2Codes.Invariant);
            if (dto.OfflinePolicyRevision != catalog.OfflinePolicy.Revision) throw new SaveV2Exception(SaveV2Codes.Version);

            SaveV2Import import = null;
            if (dto.Import != null)
            {
                if (dto.Import.SourceSchemaVersion != 1) throw new SaveV2Exception(SaveV2Codes.Invariant);
                import = new SaveV2Import(dto.Import.SourceSchemaVersion, ParseUuid(dto.Import.SourceCheckpointId).ToString("D", CultureInfo.InvariantCulture),
                    dto.Import.SourceGeneration);
            }

            PermanentState permanent = BuildPermanent(dto.Permanent, catalog);
            RunState run = BuildRun(dto.Run, dto.DepartureUtcMs, permanent, catalog);
            var meta = new SaveV2Meta(checkpointId, dto.Generation, dto.DepartureUtcMs, dto.OfflinePolicyRevision,
                reconciled, dto.ContentRevision, import);
            return new SaveV2Document(meta, run, permanent, dto.SchemaVersion);
        }

        private static PermanentState BuildPermanent(PermanentV2Dto dto, RunCatalog catalog)
        {
            var permanent = new PermanentState(catalog, dto.Ip, dto.PrestigeCount);
            string previous = null;
            foreach (IdValueV2Dto entry in dto.Industry)
            {
                RequireAscending(ref previous, entry.Id);
                if (!catalog.IndustryUpgrades.ContainsKey(entry.Id)) throw new SaveV2Exception(SaveV2Codes.Unresolved);
                Require(permanent.TrySetIndustryLevel(entry.Id, entry.Value));
            }

            previous = null;
            foreach (IdValueV2Dto entry in dto.Items)
            {
                RequireAscending(ref previous, entry.Id);
                if (!catalog.Items.ContainsKey(entry.Id)) throw new SaveV2Exception(SaveV2Codes.Unresolved);
                Require(permanent.TryAddItems(entry.Id, entry.Value));
            }

            return permanent;
        }

        private static RunState BuildRun(RunV2Dto dto, long departure, PermanentState permanent, RunCatalog catalog)
        {
            Guid runId = ParseUuid(dto.RunId);
            Guid? previousRunId = dto.PreviousRunId == null ? (Guid?)null : ParseUuid(dto.PreviousRunId);

            // Load rule 4: run.runNumber = permanent.prestigeCount + 1, and run.previousRunId is absent exactly on run 1.
            if (dto.RunNumber != permanent.PrestigeCount + 1) throw new SaveV2Exception(SaveV2Codes.CrossField);
            if ((dto.RunNumber == 1) == previousRunId.HasValue) throw new SaveV2Exception(SaveV2Codes.CrossField);

            var unlockedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in dto.Unlocked)
            {
                if (!IsKnownUnlockId(catalog, id)) throw new SaveV2Exception(SaveV2Codes.Unresolved);
                if (!unlockedIds.Add(id)) throw new SaveV2Exception(SaveV2Codes.Invariant);
            }

            var state = new RunState(catalog, runId, (int)dto.RunNumber, previousRunId, dto.Cash, dto.Rp,
                (int)dto.Storage.Level, (int)dto.Factory.Level, dto.Unlocked);
            Require(state.TryRestoreInvestment(dto.Storage.InvestedCash, dto.Factory.InvestedCash));

            // Inventory: sorted, every resource resolves and is unlocked, the sum fits the capacity (TryImport).
            var amounts = new List<ResourceAmount>(dto.Inventory.Count);
            string previous = null;
            foreach (IdValueV2Dto entry in dto.Inventory)
            {
                RequireAscending(ref previous, entry.Id);
                if (!catalog.Resources.ContainsKey(entry.Id)) throw new SaveV2Exception(SaveV2Codes.Unresolved);
                if (!state.IsUnlocked(entry.Id)) throw new SaveV2Exception(SaveV2Codes.Locked);
                amounts.Add(new ResourceAmount(entry.Id, entry.Value));
            }

            Require(state.Inventory.TryImport(amounts));

            // Machines: ascending slots, catalog type, unlocked, constructor invariants, free slot within SlotCount.
            int previousSlot = -1;
            foreach (MachineV2Dto machine in dto.Machines)
            {
                if (machine.Slot <= previousSlot) throw new SaveV2Exception(SaveV2Codes.Invariant);
                previousSlot = (int)machine.Slot;
                if (!catalog.Machines.TryGetValue(machine.MachineId, out MachineDef def)) throw new SaveV2Exception(SaveV2Codes.Unresolved);
                if (!state.IsUnlocked(machine.MachineId)) throw new SaveV2Exception(SaveV2Codes.Locked);
                var instance = new MachineInstance(ParseUuid(machine.InstanceId), def, (int)machine.Slot, (int)machine.Level,
                    machine.IsActive, machine.ProgressTicks, machine.CycleDurationTicks, machine.InvestedCash);
                Require(state.TryAddMachine(instance));
            }

            var priority = new List<Guid>(dto.Priority.Count);
            foreach (string id in dto.Priority) priority.Add(ParseUuid(id));
            Require(state.TryReorderPriority(priority));

            previous = null;
            foreach (string id in dto.AutoSell)
            {
                RequireAscending(ref previous, id);
                if (!catalog.Resources.ContainsKey(id)) throw new SaveV2Exception(SaveV2Codes.Unresolved);
                if (!state.IsUnlocked(id)) throw new SaveV2Exception(SaveV2Codes.Locked);
                Require(state.TrySetAutoSell(id, true));
            }

            BuildContracts(dto, departure, state, catalog);

            previous = null;
            foreach (IdValueV2Dto entry in dto.Research)
            {
                RequireAscending(ref previous, entry.Id);
                if (!catalog.Research.ContainsKey(entry.Id)) throw new SaveV2Exception(SaveV2Codes.Unresolved);
                Require(state.TrySetResearchLevel(entry.Id, entry.Value));
            }

            BuildEffects(dto.Effects, departure, state, catalog);
            BuildOverdrive(dto.Overdrive, departure, state, catalog);

            Require(state.Validate());
            return state;
        }

        /// <summary>
        /// The contract board: completion counts, then (V3) the run-wide total, the active contract with (V3) its production count and
        /// auto-deliver setting, and (V3) the cooldowns and bonus state, each through the board's public restore surface (T334). A V2 run object
        /// has none of the V3 members and keeps the V2 defaults; its run-wide total is the sum of the counts (<see cref="ContractBoard.TrySetCompletionCount"/>).
        /// </summary>
        private static void BuildContracts(RunV2Dto run, long departure, RunState state, RunCatalog catalog)
        {
            ContractsV2Dto dto = run.Contracts;
            ContractBoard board = state.Contracts;
            string previous = null;
            long sum = 0;
            foreach (IdValueV2Dto entry in dto.Completions)
            {
                RequireAscending(ref previous, entry.Id);
                if (!catalog.ContractOffers.TryGetValue(entry.Id, out ContractOfferDef offer)) throw new SaveV2Exception(SaveV2Codes.Unresolved);
                if (entry.Value > offer.QuantityTable.Count) throw new SaveV2Exception(SaveV2Codes.Invariant);
                Require(board.TrySetCompletionCount(entry.Id, entry.Value));
                if (!CheckedMath.TryAdd(sum, entry.Value, out sum)) throw new SaveV2Exception(SaveV2Codes.Bounds);
            }

            // V3: the persisted run-wide total is at least the sum of the persisted counts (save-v3.md), and the board keeps
            // max(saved, derived), so it is never lowered (SEC-8).
            if (run.RunCompletionTotal.HasValue)
            {
                if (run.RunCompletionTotal.Value < sum) throw new SaveV2Exception(SaveV2Codes.Invariant);
                Require(board.TryRestoreRunCompletionTotal(run.RunCompletionTotal.Value));
            }

            ActiveContractV2Dto active = dto.Active;
            if (active != null)
            {
                if (!catalog.ContractOffers.TryGetValue(active.OfferId, out ContractOfferDef activeOffer)) throw new SaveV2Exception(SaveV2Codes.Unresolved);

                // Load rule 4: acceptedUtcMs is not after the departure. The persisted deadline is authoritative (owner decision 2026-10-01):
                // only 0 < deadline - accepted <= MaxTimedDurationMs is checked, so a content retune stays save-compatible.
                if (active.AcceptedUtcMs > departure) throw new SaveV2Exception(SaveV2Codes.CrossField);
                if (activeOffer.DurationMs.HasValue != active.DeadlineUtcMs.HasValue) throw new SaveV2Exception(SaveV2Codes.CrossField);
                if (active.DeadlineUtcMs.HasValue && !TimedSpanOk(active.AcceptedUtcMs, active.DeadlineUtcMs.Value, false))
                    throw new SaveV2Exception(SaveV2Codes.CrossField);
                Require(board.TrySetActive(new ActiveContract(active.OfferId, active.AcceptedUtcMs, active.DeadlineUtcMs, active.RequiredUnits)));

                // V3 (a V2 object leaves false and 0, which are the defaults the restore would set anyway): the production count lies in
                // [0, requiredUnits] for a production objective and is 0 otherwise; auto-deliver is true only for a delivery objective.
                // Produced == requiredUnits is valid and completes at the first settle (TryRestoreProduced).
                if (active.Produced != 0) Require(board.TryRestoreProduced(active.Produced));
                if (active.AutoDeliver) Require(board.TryRestoreAutoDeliver(true));
            }

            if (run.ContractCooldowns != null)
            {
                previous = null;
                foreach (IdValueV2Dto entry in run.ContractCooldowns)
                {
                    RequireAscending(ref previous, entry.Id);
                    if (!catalog.ContractOffers.ContainsKey(entry.Id)) throw new SaveV2Exception(SaveV2Codes.Unresolved);
                    Require(board.TryRestoreCooldown(entry.Id, entry.Value));
                }
            }

            if (run.BonusOffers != null)
            {
                previous = null;
                foreach (BonusOfferV3Dto entry in run.BonusOffers)
                {
                    RequireAscending(ref previous, entry.OfferId);
                    if (!catalog.ContractOffers.ContainsKey(entry.OfferId)) throw new SaveV2Exception(SaveV2Codes.Unresolved);
                    // A standing offer has no bonus state (invariant); the window end is required for both states and lies in [0, 10^18].
                    Require(board.TryRestoreBonus(entry.OfferId, entry.WindowEndUtcMs, entry.Consumed));
                }
            }
        }

        private static void BuildEffects(List<EffectV2Dto> effects, long departure, RunState state, RunCatalog catalog)
        {
            EffectV2Dto before = null;
            foreach (EffectV2Dto effect in effects)
            {
                if (before != null)
                {
                    int order = string.CompareOrdinal(before.ItemId, effect.ItemId);
                    if (order == 0) order = string.CompareOrdinal(before.EffectKind, effect.EffectKind);
                    if (order == 0) order = string.CompareOrdinal(before.TargetKey, effect.TargetKey);
                    if (order >= 0) throw new SaveV2Exception(SaveV2Codes.Invariant);
                }

                before = effect;
                if (!catalog.Items.TryGetValue(effect.ItemId, out ItemDef item)) throw new SaveV2Exception(SaveV2Codes.Unresolved);
                if (!string.Equals(effect.EffectKind, item.Effect.Kind, StringComparison.Ordinal)) throw new SaveV2Exception(SaveV2Codes.Invariant);
                if (item.TargetKind == "factory")
                {
                    if (!string.Equals(effect.TargetKey, "factory", StringComparison.Ordinal)) throw new SaveV2Exception(SaveV2Codes.Invariant);
                }
                else if (!ContainsOrdinal(item.AllowedMachineIds, effect.TargetKey))
                {
                    throw new SaveV2Exception(SaveV2Codes.Invariant);
                }

                // Load rule 4: startUtcMs is not after the departure and 0 < end - start <= MaxTimedDurationMs (the persisted end is
                // authoritative, owner decision 2026-10-01).
                if (effect.StartUtcMs > departure) throw new SaveV2Exception(SaveV2Codes.CrossField);
                if (!TimedSpanOk(effect.StartUtcMs, effect.EndUtcMs, false)) throw new SaveV2Exception(SaveV2Codes.CrossField);
                Require(state.TryAddEffect(new ActiveEffect(effect.ItemId, new ConflictKey(effect.EffectKind, effect.TargetKey),
                    effect.StartUtcMs, effect.EndUtcMs)));
            }
        }

        private static void BuildOverdrive(OverdriveV2Dto dto, long departure, RunState state, RunCatalog catalog)
        {
            if (dto.ActiveUntilUtcMs.HasValue != dto.CooldownUntilUtcMs.HasValue) throw new SaveV2Exception(SaveV2Codes.Invariant);
            if (!dto.ActiveUntilUtcMs.HasValue) return;

            // Load rule 4: the Overdrive start is not persisted, so its active end is bounded relative to the departure by the cap
            // (activeUntil - departure <= MaxTimedDurationMs) and 0 <= cooldownUntil - activeUntil <= MaxTimedDurationMs. The
            // persisted instants are authoritative (owner decision 2026-10-01; a zero cooldown is valid content), so retuning the
            // Overdrive duration or cooldown, shorter or longer, stays save-compatible.
            if (dto.ActiveUntilUtcMs.Value < 0 || dto.ActiveUntilUtcMs.Value - departure > RunLimits.MaxTimedDurationMs)
                throw new SaveV2Exception(SaveV2Codes.CrossField);
            if (!TimedSpanOk(dto.ActiveUntilUtcMs.Value, dto.CooldownUntilUtcMs.Value, true)) throw new SaveV2Exception(SaveV2Codes.CrossField);
            Require(state.Overdrive.TryRestore(dto.ActiveUntilUtcMs, dto.CooldownUntilUtcMs));
        }

        private static bool TimedSpanOk(long start, long end, bool allowZero)
        {
            if (start < 0 || end < 0) return false;
            long span = end - start;
            return (allowZero ? span >= 0 : span > 0) && span <= RunLimits.MaxTimedDurationMs;
        }

        private static void Require(RunStateResult result)
        {
            if (result != RunStateResult.Ok) throw new SaveV2Exception(SaveV2Codes.Invariant);
        }

        private static void RequireAscending(ref string previous, string id)
        {
            if (previous != null && string.CompareOrdinal(previous, id) >= 0) throw new SaveV2Exception(SaveV2Codes.Invariant);
            previous = id;
        }

        private static bool IsKnownUnlockId(RunCatalog catalog, string id)
        {
            return string.Equals(id, RunState.OverdriveUnlockId, StringComparison.Ordinal) || catalog.Resources.ContainsKey(id) ||
                catalog.Machines.ContainsKey(id) || catalog.ContractOffers.ContainsKey(id) || catalog.Research.ContainsKey(id) ||
                catalog.Items.ContainsKey(id);
        }

        private static bool ContainsOrdinal(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++)
                if (string.Equals(ids[i], id, StringComparison.Ordinal)) return true;
            return false;
        }

        private static Guid ParseUuid(string value)
        {
            if (value == null || value.Length != 36 || !Guid.TryParseExact(value, "D", out Guid guid) || guid == Guid.Empty ||
                !string.Equals(guid.ToString("D", CultureInfo.InvariantCulture), value, StringComparison.Ordinal))
                throw new SaveV2Exception(SaveV2Codes.Structure);
            return guid;
        }

        // ------------------------------------------------------------------------------------------------------
        // Write
        // ------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Writes the canonical Save V3 bytes (<see cref="SchemaVersion"/>, T334) of a run and its permanent state (for the pending
        /// prestige candidate: <c>candidate.Run</c> and <c>candidate.Permanent</c>). The bytes are read back through
        /// <see cref="Deserialize"/> against the run's own catalog before they are returned, so a state that the
        /// reader would reject is never written.
        /// </summary>
        public static SaveV2WriteResult Serialize(SaveV2Meta meta, RunState run, PermanentState permanent) =>
            Serialize(meta, run, permanent, SchemaVersion);

        /// <summary>
        /// Writes the canonical bytes of <paramref name="schemaVersion"/> (2 or 3). Version 2 exists for the V2 regression fixtures and for
        /// <see cref="SaveV2CheckpointCodec.ValidateBytes"/> (which compares against the version it read); it is lossless or refused: a state
        /// with any V3-only data (auto-deliver on, a production count, a cooldown, bonus state, or a run-wide total above the sum of the
        /// counts) is <c>save.v2.write</c>, never silently dropped.
        /// </summary>
        internal static SaveV2WriteResult Serialize(SaveV2Meta meta, RunState run, PermanentState permanent, int schemaVersion)
        {
            if (meta == null || run == null || permanent == null) return RejectWrite(SaveV2Codes.Write);
            if (schemaVersion != LegacySchemaVersion && schemaVersion != SchemaVersion) return RejectWrite(SaveV2Codes.Write);
            if (schemaVersion == LegacySchemaVersion && !IsRepresentableInV2(run.Contracts)) return RejectWrite(SaveV2Codes.Write);
            byte[] bytes;
            try
            {
                var builder = new StringBuilder();
                using (var textWriter = new StringWriter(builder, CultureInfo.InvariantCulture))
                using (var writer = new JsonTextWriter(textWriter)
                {
                    Culture = CultureInfo.InvariantCulture,
                    Formatting = Formatting.None,
                })
                {
                    WriteDocument(writer, meta, run, permanent, TablesFor(schemaVersion));
                    writer.Flush();
                }

                bytes = StrictUtf8.GetBytes(builder.ToString());
            }
            catch (SaveV2Exception exception)
            {
                return RejectWrite(exception.Code);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                return RejectWrite(SaveV2Codes.Write);
            }

            if (bytes.Length > MaximumByteCount) return RejectWrite(SaveV2Codes.Limit);
            SaveV2ReadResult check = Deserialize(bytes, run.Catalog);
            if (!check.IsSuccess) return RejectWrite(check.Code);
            return SaveV2WriteResult.Success(bytes);
        }

        /// <summary>True when the board holds nothing that only V3 can carry (the V2 defaults of save-v3.md).</summary>
        private static bool IsRepresentableInV2(ContractBoard board)
        {
            ActiveContract active = board.Active;
            if (active != null && (active.AutoDeliver || active.Produced != 0)) return false;
            if (board.CooldownStamps().Length != 0 || board.BonusOffers().Length != 0) return false;
            long sum = 0;
            foreach (IdCount count in board.CompletionCounts())
                if (!CheckedMath.TryAdd(sum, count.Count, out sum)) return false;
            return board.RunCompletionTotal == sum;
        }

        private static void WriteDocument(JsonTextWriter w, SaveV2Meta meta, RunState run, PermanentState permanent, MemberTables tables)
        {
            w.WriteStartObject();
            Property(w, "schemaVersion", tables.Version);
            Property(w, "checkpointId", meta.CheckpointId);
            Property(w, "generation", meta.Generation);
            Property(w, "departureUtcMs", meta.DepartureUtcMs);
            Property(w, "offlinePolicyRevision", meta.OfflinePolicyRevision);
            w.WritePropertyName("reconciledFromCheckpointId");
            WriteNullableUuid(w, meta.ReconciledFromCheckpointId);
            Property(w, "contentRevision", meta.ContentRevision);
            w.WritePropertyName("import");
            if (meta.Import == null)
            {
                w.WriteNull();
            }
            else
            {
                w.WriteStartObject();
                Property(w, "sourceSchemaVersion", meta.Import.SourceSchemaVersion);
                Property(w, "sourceCheckpointId", meta.Import.SourceCheckpointId);
                Property(w, "sourceGeneration", meta.Import.SourceGeneration);
                w.WriteEndObject();
            }

            w.WritePropertyName("permanent");
            w.WriteStartObject();
            Property(w, "ip", permanent.Ip);
            Property(w, "prestigeCount", permanent.PrestigeCount);
            w.WritePropertyName("industry");
            WriteIdCounts(w, permanent.IndustryLevels(), "id", "level");
            w.WritePropertyName("items");
            WriteIdCounts(w, permanent.ItemStacks(), "id", "count");
            w.WriteEndObject();

            w.WritePropertyName("run");
            WriteRun(w, run, tables);
            w.WriteEndObject();
        }

        private static void WriteRun(JsonTextWriter w, RunState run, MemberTables tables)
        {
            w.WriteStartObject();
            Property(w, "runId", run.RunId);
            Property(w, "runNumber", run.RunNumber);
            w.WritePropertyName("previousRunId");
            WriteNullableUuid(w, run.PreviousRunId);
            Property(w, "cash", run.Cash);
            Property(w, "rp", run.Rp);
            w.WritePropertyName("storage");
            WriteExpansion(w, run.StorageLevel, run.StorageInvestedCash);
            w.WritePropertyName("factory");
            WriteExpansion(w, run.FactoryLevel, run.FactoryInvestedCash);

            w.WritePropertyName("inventory");
            w.WriteStartArray();
            for (int i = 0; i < run.Inventory.Count; i++)
            {
                if (run.Inventory.UnitsAt(i) <= 0) continue;
                w.WriteStartObject();
                Property(w, "resourceId", run.Inventory.IdAt(i));
                Property(w, "units", run.Inventory.UnitsAt(i));
                w.WriteEndObject();
            }

            w.WriteEndArray();

            w.WritePropertyName("machines");
            w.WriteStartArray();
            foreach (MachineInstance machine in run.Machines)
            {
                w.WriteStartObject();
                Property(w, "instanceId", machine.InstanceId);
                Property(w, "machineId", machine.MachineId);
                Property(w, "slot", machine.SlotIndex);
                Property(w, "level", machine.Level);
                w.WritePropertyName("isActive");
                w.WriteValue(machine.IsActive);
                Property(w, "progressTicks", machine.ProgressTicks);
                Property(w, "cycleDurationTicks", machine.CycleDurationTicks);
                Property(w, "investedCash", machine.InvestedCash);
                w.WriteEndObject();
            }

            w.WriteEndArray();

            w.WritePropertyName("priority");
            w.WriteStartArray();
            foreach (Guid id in run.Priority.Ids) w.WriteValue(id.ToString("D", CultureInfo.InvariantCulture));
            w.WriteEndArray();

            w.WritePropertyName("autoSell");
            WriteStrings(w, run.AutoSell);
            w.WritePropertyName("unlocked");
            WriteStrings(w, run.Unlocked);

            w.WritePropertyName("contracts");
            w.WriteStartObject();
            w.WritePropertyName("completions");
            WriteIdCounts(w, run.Contracts.CompletionCounts(), "offerId", "count");
            w.WritePropertyName("active");
            ActiveContract active = run.Contracts.Active;
            if (active == null)
            {
                w.WriteNull();
            }
            else
            {
                w.WriteStartObject();
                Property(w, "offerId", active.OfferId);
                Property(w, "acceptedUtcMs", active.AcceptedUtcMs);
                w.WritePropertyName("deadlineUtcMs");
                WriteNullableLong(w, active.DeadlineUtcMs);
                Property(w, "requiredUnits", active.RequiredUnits);
                if (tables.IsV3)
                {
                    w.WritePropertyName("autoDeliver");
                    w.WriteValue(active.AutoDeliver);
                    Property(w, "produced", active.Produced);
                }

                w.WriteEndObject();
            }

            w.WriteEndObject();

            w.WritePropertyName("research");
            WriteIdCounts(w, run.ResearchLevels(), "id", "level");

            w.WritePropertyName("effects");
            w.WriteStartArray();
            foreach (ActiveEffect effect in run.Effects.Effects)
            {
                w.WriteStartObject();
                Property(w, "itemId", effect.SourceId);
                Property(w, "effectKind", effect.ConflictKey.EffectKind);
                Property(w, "targetKey", effect.ConflictKey.TargetKey);
                Property(w, "startUtcMs", effect.StartUtcMs);
                Property(w, "endUtcMs", effect.EndUtcMs);
                w.WriteEndObject();
            }

            w.WriteEndArray();

            w.WritePropertyName("overdrive");
            w.WriteStartObject();
            w.WritePropertyName("activeUntilUtcMs");
            WriteNullableLong(w, run.Overdrive.ActiveUntilUtcMs);
            w.WritePropertyName("cooldownUntilUtcMs");
            WriteNullableLong(w, run.Overdrive.CooldownUntilUtcMs);
            w.WriteEndObject();

            if (tables.IsV3)
            {
                // Save V3 members (T334): the run-wide completion total (SEC-8), the cooldown stamps and the bonus state, in ordinal offer order.
                Property(w, "runCompletionTotal", run.Contracts.RunCompletionTotal);
                w.WritePropertyName("contractCooldowns");
                w.WriteStartArray();
                foreach (ContractCooldownStamp stamp in run.Contracts.CooldownStamps())
                {
                    w.WriteStartObject();
                    Property(w, "offerId", stamp.OfferId);
                    Property(w, "readyAtUtcMs", stamp.ReadyAtUtcMs);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WritePropertyName("bonusOffers");
                w.WriteStartArray();
                foreach (BonusOfferRecord bonus in run.Contracts.BonusOffers())
                {
                    w.WriteStartObject();
                    Property(w, "offerId", bonus.OfferId);
                    Property(w, "state", bonus.Consumed ? BonusConsumed : BonusTriggered);
                    Property(w, "windowEndUtcMs", bonus.WindowEndUtcMs);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
            }

            w.WriteEndObject();
        }

        private static void WriteExpansion(JsonTextWriter w, int level, long investedCash)
        {
            w.WriteStartObject();
            Property(w, "level", level);
            Property(w, "investedCash", investedCash);
            w.WriteEndObject();
        }

        private static void WriteIdCounts(JsonTextWriter w, IdCount[] entries, string idName, string valueName)
        {
            w.WriteStartArray();
            foreach (IdCount entry in entries)
            {
                w.WriteStartObject();
                Property(w, idName, entry.Id);
                Property(w, valueName, entry.Count);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        private static void WriteStrings(JsonTextWriter w, IReadOnlyList<string> values)
        {
            w.WriteStartArray();
            for (int i = 0; i < values.Count; i++) w.WriteValue(SafeString(values[i]));
            w.WriteEndArray();
        }

        private static void WriteNullableUuid(JsonTextWriter w, Guid? value)
        {
            if (value.HasValue) w.WriteValue(value.Value.ToString("D", CultureInfo.InvariantCulture));
            else w.WriteNull();
        }

        private static void WriteNullableLong(JsonTextWriter w, long? value)
        {
            if (value.HasValue) w.WriteValue(value.Value);
            else w.WriteNull();
        }

        private static void Property(JsonTextWriter w, string name, long value)
        {
            w.WritePropertyName(name);
            w.WriteValue(value);
        }

        private static void Property(JsonTextWriter w, string name, Guid value)
        {
            w.WritePropertyName(name);
            w.WriteValue(value.ToString("D", CultureInfo.InvariantCulture));
        }

        private static void Property(JsonTextWriter w, string name, string value)
        {
            w.WritePropertyName(name);
            w.WriteValue(SafeString(value));
        }

        /// <summary>A string that the reader accepts as is: no quote, backslash or control character (no escapes are written).</summary>
        private static string SafeString(string value)
        {
            if (value == null || value.Length == 0 || value.Length > MaximumIdentifierLength) throw new SaveV2Exception(SaveV2Codes.Write);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"' || c == '\\' || c < 0x20) throw new SaveV2Exception(SaveV2Codes.Write);
            }

            return value;
        }

        /// <summary>Carries only a static code; there is no message, so nothing file-derived can reach a caller.</summary>
        private sealed class SaveV2Exception : Exception
        {
            public SaveV2Exception(string code)
            {
                Code = code;
            }

            public string Code { get; }
        }
    }
}
