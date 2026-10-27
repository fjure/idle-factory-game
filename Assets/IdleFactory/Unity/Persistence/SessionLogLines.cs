using System;
using IdleFactory.Run.Application;

namespace IdleFactory.Unity.Persistence
{
    /// <summary>
    /// The enum tokens of the session log (data-model "Session log"). Hand-written switches: no <c>Enum.ToString</c>, no reflection,
    /// no allocation. An undefined value returns null and the writer drops the record (SEC-1: only tokens from this table are written).
    /// </summary>
    public static class SessionLogTokens
    {
        public static string BuildKind(RunBuildKind kind)
        {
            switch (kind)
            {
                case RunBuildKind.Release: return "release";
                case RunBuildKind.Development: return "development";
                case RunBuildKind.Editor: return "editor";
                default: return null;
            }
        }

        public static string PlatformClass(RunPlatformClass kind)
        {
            switch (kind)
            {
                case RunPlatformClass.Desktop: return "desktop";
                case RunPlatformClass.Mobile: return "mobile";
                default: return null;
            }
        }

        public static string Milestone(RunMilestoneKind kind)
        {
            switch (kind)
            {
                case RunMilestoneKind.FirstGear: return "firstGear";
                case RunMilestoneKind.FirstContract: return "firstContract";
                case RunMilestoneKind.FirstItem: return "firstItem";
                case RunMilestoneKind.FirstPrestige: return "firstPrestige";
                case RunMilestoneKind.Run2Eligible: return "run2Eligible";
                default: return null;
            }
        }

        public static string Contract(RunContractEventKind kind)
        {
            switch (kind)
            {
                case RunContractEventKind.Accept: return "accept";
                case RunContractEventKind.Deliver: return "deliver";
                case RunContractEventKind.AutoDeliver: return "autoDeliver";
                case RunContractEventKind.ProduceComplete: return "produceComplete";
                case RunContractEventKind.Expire: return "expire";
                case RunContractEventKind.Abandon: return "abandon";
                case RunContractEventKind.Blocked: return "blocked";
                default: return null;
            }
        }

        public static string Purchase(RunPurchaseKind kind)
        {
            switch (kind)
            {
                case RunPurchaseKind.Build: return "build";
                case RunPurchaseKind.Upgrade: return "upgrade";
                case RunPurchaseKind.Storage: return "storage";
                case RunPurchaseKind.Factory: return "factory";
                case RunPurchaseKind.Research: return "research";
                case RunPurchaseKind.Industry: return "industry";
                default: return null;
            }
        }

        public static string Clock(RunClockKind kind)
        {
            switch (kind)
            {
                case RunClockKind.Backward: return "backward";
                case RunClockKind.ForwardJump: return "forwardJump";
                case RunClockKind.LiveGap: return "liveGap";
                default: return null;
            }
        }

        public static string Lifecycle(RunLifecycleKind kind)
        {
            switch (kind)
            {
                case RunLifecycleKind.Start: return "start";
                case RunLifecycleKind.Quit: return "quit";
                case RunLifecycleKind.FocusLossCheckpoint: return "focusLossCheckpoint";
                case RunLifecycleKind.LogToggled: return "logToggled";
                case RunLifecycleKind.Cleared: return "cleared";
                default: return null;
            }
        }
    }

    /// <summary>The strict content-ID pattern (S3, SEC-1): <c>[a-z0-9][a-z0-9._-]{0,63}</c>, and never a GUID-shaped value.</summary>
    public static class SessionLogIds
    {
        public const int MaxLength = 64;

        public static bool IsValid(string id)
        {
            if (id == null || id.Length == 0 || id.Length > MaxLength) return false;
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                bool lower = c >= 'a' && c <= 'z';
                bool digit = c >= '0' && c <= '9';
                if (i == 0 ? !(lower || digit) : !(lower || digit || c == '.' || c == '_' || c == '-')) return false;
            }

            return !LooksLikeGuid(id);
        }

        /// <summary>True for the "D" form (8-4-4-4-12 hex digits) and the "N" form (32 hex digits), the shapes a generated identifier takes.</summary>
        public static bool LooksLikeGuid(string id)
        {
            if (id.Length == 32)
            {
                for (int i = 0; i < 32; i++) if (!IsHex(id[i])) return false;
                return true;
            }

            if (id.Length != 36) return false;
            for (int i = 0; i < 36; i++)
            {
                bool dash = i == 8 || i == 13 || i == 18 || i == 23;
                if (dash ? id[i] != '-' : !IsHex(id[i])) return false;
            }

            return true;
        }

        private static bool IsHex(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }

    /// <summary>CRC-32 (IEEE 802.3, reflected polynomial 0xEDB88320) of a byte range, table driven, no allocation after the type loads.</summary>
    public static class SessionLogCrc32
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1u) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }

            return table;
        }

        public static uint Compute(byte[] buffer, int offset, int count)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < count; i++) crc = Table[(crc ^ buffer[offset + i]) & 0xFFu] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }

    /// <summary>
    /// The line format of the session log: one JSON object per line, <c>{"t":"&lt;type&gt;","v":1,...fields...,"crc":"&lt;8 lower-case hex&gt;"}</c>,
    /// the CRC-32 taken over every byte of the line before <c>,"crc"</c>. <see cref="Validate"/> is the reader used by the export and the tests.
    /// </summary>
    public static class SessionLogLines
    {
        public const int SchemaVersion = 1;

        // ,"crc":"xxxxxxxx"}
        private const int TrailerLength = 8 + 8 + 2;
        private static readonly byte[] CrcTag = { (byte)',', (byte)'"', (byte)'c', (byte)'r', (byte)'c', (byte)'"', (byte)':', (byte)'"' };

        /// <summary>True when the bytes [start, start + length) (without the newline) are a complete line whose CRC matches.</summary>
        public static bool Validate(byte[] buffer, int start, int length)
        {
            if (buffer == null || length < TrailerLength + 2 || start < 0 || start + length > buffer.Length) return false;
            if (buffer[start] != (byte)'{') return false;
            int tag = start + length - TrailerLength;
            for (int i = 0; i < CrcTag.Length; i++) if (buffer[tag + i] != CrcTag[i]) return false;
            if (buffer[start + length - 2] != (byte)'"' || buffer[start + length - 1] != (byte)'}') return false;
            uint expected = SessionLogCrc32.Compute(buffer, start, tag - start);
            for (int i = 0; i < 8; i++)
            {
                int shift = (7 - i) * 4;
                int digit = (int)((expected >> shift) & 0xFu);
                byte want = (byte)(digit < 10 ? '0' + digit : 'a' + digit - 10);
                if (buffer[tag + CrcTag.Length + i] != want) return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Builds one line into a fixed buffer without allocating: ASCII only (names and tokens are literals, IDs pass <see cref="SessionLogIds"/>), numbers
    /// formatted by hand. Any overflow or invalid ID marks the line <see cref="Failed"/>; the caller discards it and counts a drop. Not thread safe: the
    /// writer owns one instance under its gate.
    /// </summary>
    internal sealed class SessionLogLineBuilder
    {
        public const int Capacity = 8192;

        private const string HexDigits = "0123456789abcdef";

        private readonly byte[] _buffer = new byte[Capacity];
        private int _length;
        private bool _failed = true;
        private bool _needComma;
        private bool _firstElement;

        public byte[] Buffer => _buffer;

        public int Length => _length;

        public bool Failed => _failed;

        public void Begin(string type)
        {
            _length = 0;
            _failed = false;
            _needComma = false;
            Raw("{\"t\":\"");
            Raw(type);
            Raw("\",\"v\":");
            Digits(SessionLogLines.SchemaVersion);
            _needComma = true;
        }

        public void Number(string name, long value)
        {
            Name(name);
            Digits(value);
        }

        public void Bool(string name, bool value)
        {
            Name(name);
            Raw(value ? "true" : "false");
        }

        /// <summary>An enum token from <see cref="SessionLogTokens"/>; null (an undefined enum value) fails the line.</summary>
        public void Token(string name, string token)
        {
            if (token == null)
            {
                _failed = true;
                return;
            }

            Name(name);
            Put((byte)'"');
            Raw(token);
            Put((byte)'"');
        }

        /// <summary>A content ID. Fails the line when it does not match the strict pattern or is not in <paramref name="ids"/>.</summary>
        public void Id(string name, string id, ISessionLogContentIds ids)
        {
            if (!SessionLogIds.IsValid(id) || (ids != null && !ids.Contains(id)))
            {
                _failed = true;
                return;
            }

            Name(name);
            Put((byte)'"');
            Raw(id);
            Put((byte)'"');
        }

        /// <summary>The 128-bit session ID as 32 lower-case hex digits.</summary>
        public void SessionId(string name, ulong high, ulong low)
        {
            Name(name);
            Put((byte)'"');
            Hex(high);
            Hex(low);
            Put((byte)'"');
        }

        public void BeginArray(string name)
        {
            Name(name);
            Put((byte)'[');
            _firstElement = true;
        }

        public void BeginElement()
        {
            if (!_firstElement) Put((byte)',');
            _firstElement = false;
            Put((byte)'{');
            _needComma = false;
        }

        public void EndElement()
        {
            Put((byte)'}');
            _needComma = true;
        }

        public void EndArray()
        {
            Put((byte)']');
            _needComma = true;
        }

        /// <summary>Appends the CRC member, the closing brace and the newline. Returns false when the line failed.</summary>
        public bool End()
        {
            if (_failed) return false;
            uint crc = SessionLogCrc32.Compute(_buffer, 0, _length);
            Raw(",\"crc\":\"");
            for (int shift = 28; shift >= 0; shift -= 4) Put((byte)HexDigits[(int)((crc >> shift) & 0xFu)]);
            Raw("\"}");
            Put((byte)'\n');
            return !_failed;
        }

        private void Name(string name)
        {
            if (_needComma) Put((byte)',');
            _needComma = true;
            Put((byte)'"');
            Raw(name);
            Put((byte)'"');
            Put((byte)':');
        }

        private void Hex(ulong value)
        {
            for (int shift = 60; shift >= 0; shift -= 4) Put((byte)HexDigits[(int)((value >> shift) & 0xFUL)]);
        }

        private void Digits(long value)
        {
            ulong magnitude;
            if (value < 0)
            {
                Put((byte)'-');
                magnitude = (ulong)(-(value + 1)) + 1UL;
            }
            else
            {
                magnitude = (ulong)value;
            }

            int digits = 1;
            for (ulong rest = magnitude; rest >= 10UL; rest /= 10UL) digits++;
            if (_failed || _length + digits > _buffer.Length)
            {
                _failed = true;
                return;
            }

            int at = _length + digits;
            do
            {
                _buffer[--at] = (byte)('0' + (int)(magnitude % 10UL));
                magnitude /= 10UL;
            }
            while (magnitude != 0UL);
            _length += digits;
        }

        private void Raw(string ascii)
        {
            if (_failed) return;
            if (_length + ascii.Length > _buffer.Length)
            {
                _failed = true;
                return;
            }

            for (int i = 0; i < ascii.Length; i++) _buffer[_length++] = (byte)ascii[i];
        }

        private void Put(byte value)
        {
            if (_failed) return;
            if (_length >= _buffer.Length)
            {
                _failed = true;
                return;
            }

            _buffer[_length++] = value;
        }
    }
}
