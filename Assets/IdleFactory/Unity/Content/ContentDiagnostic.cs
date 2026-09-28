using System;

namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// One diagnostic from <see cref="GameplayContentReader"/> (or the T073 semantic
    /// validator). Never includes absolute file-system paths or player data
    /// (contract "Diagnostics").
    /// </summary>
    public sealed class ContentDiagnostic
    {
        public ContentDiagnostic(string code, string path, string entryId, int? line, int? column, string message)
        {
            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentException("Diagnostic code is required.", nameof(code));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentException("Diagnostic message is required.", nameof(message));
            }

            Code = code;
            Path = path;
            EntryId = entryId;
            Line = line;
            Column = column;
            Message = message;
        }

        /// <summary>Stable rule code, e.g. "value.type" (see data-model.md).</summary>
        public string Code { get; }

        /// <summary>A JSON Pointer (RFC 6901) into the document, e.g. "/buildings/0/inputBuffer/1/capacity".</summary>
        public string Path { get; }

        /// <summary>The entry's authored ID when known at the point of failure; otherwise null.</summary>
        public string EntryId { get; }

        /// <summary>1-based source line, from IJsonLineInfo, when available.</summary>
        public int? Line { get; }

        /// <summary>1-based source column, from IJsonLineInfo, when available.</summary>
        public int? Column { get; }

        /// <summary>One line of human-readable text.</summary>
        public string Message { get; }
    }
}
