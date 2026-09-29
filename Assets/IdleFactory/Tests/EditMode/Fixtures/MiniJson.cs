using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>Serialization layout for <see cref="JToken.ToString(Formatting)"/>; only the compact form exists.</summary>
    public enum Formatting
    {
        None,
    }

    /// <summary>
    /// Minimal test-only JSON tree (objects keep member order, integers are <see cref="long"/>). The EditMode test
    /// assembly does not reference Newtonsoft.Json, and the tests need to parse the schema and the guide example and to
    /// mutate a document before the reader sees it. The API is the small subset of a JSON DOM the tests use.
    /// </summary>
    public abstract class JToken : IEnumerable<JToken>
    {
        public JToken Parent { get; internal set; }

        public virtual JToken this[string name]
        {
            get => throw new InvalidOperationException("Not an object.");
            set => throw new InvalidOperationException("Not an object.");
        }

        public virtual JToken this[int index]
        {
            get => throw new InvalidOperationException("Not an array.");
            set => throw new InvalidOperationException("Not an array.");
        }

        public static implicit operator JToken(string value) => new JValue(value);

        public static implicit operator JToken(int value) => new JValue((long)value);

        public static implicit operator JToken(long value) => new JValue(value);

        public static implicit operator JToken(bool value) => new JValue(value);

        public static explicit operator string(JToken token) => (string)((JValue)token).Value;

        public static explicit operator int(JToken token) => checked((int)(long)((JValue)token).Value);

        public static explicit operator long(JToken token) => (long)((JValue)token).Value;

        public static explicit operator bool(JToken token) => (bool)((JValue)token).Value;

        public abstract JToken DeepClone();

        public void Replace(JToken replacement)
        {
            if (Parent is JObject obj)
            {
                obj.ReplaceChild(this, replacement);
            }
            else if (Parent is JArray array)
            {
                array.ReplaceChild(this, replacement);
            }
            else
            {
                throw new InvalidOperationException("Token has no parent.");
            }
        }

        public string ToString(Formatting formatting)
        {
            var builder = new StringBuilder();
            Write(builder);
            return builder.ToString();
        }

        public override string ToString() => ToString(Formatting.None);

        internal abstract void Write(StringBuilder builder);

        public virtual IEnumerator<JToken> GetEnumerator() => Enumerable.Empty<JToken>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal static JToken From(object value)
        {
            switch (value)
            {
                case null:
                    return JValue.CreateNull();
                case JToken token:
                    return token.Parent == null ? token : token.DeepClone();
                case string text:
                    return new JValue(text);
                case int number:
                    return new JValue((long)number);
                case long number:
                    return new JValue(number);
                case bool flag:
                    return new JValue(flag);
                default:
                    throw new ArgumentException("Unsupported JSON value type " + value.GetType().Name);
            }
        }
    }

    public sealed class JValue : JToken
    {
        public JValue(object value)
        {
            Value = value;
        }

        public object Value { get; }

        public static JValue CreateNull() => new JValue(null);

        public override JToken DeepClone() => new JValue(Value);

        internal override void Write(StringBuilder builder)
        {
            switch (Value)
            {
                case null:
                    builder.Append("null");
                    break;
                case string text:
                    WriteString(builder, text);
                    break;
                case bool flag:
                    builder.Append(flag ? "true" : "false");
                    break;
                default:
                    builder.Append(Convert.ToString(Value, CultureInfo.InvariantCulture));
                    break;
            }
        }

        internal static void WriteString(StringBuilder builder, string text)
        {
            builder.Append('"');
            foreach (var character in text)
            {
                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (character < 0x20)
                        {
                            builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }

    public sealed class JProperty
    {
        public JProperty(string name, JToken value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public JToken Value { get; internal set; }
    }

    public sealed class JObject : JToken
    {
        private readonly List<JProperty> _properties = new List<JProperty>();

        public override JToken this[string name]
        {
            get => _properties.FirstOrDefault(p => p.Name == name)?.Value;
            set
            {
                var stored = From(value);
                stored.Parent = this;
                var existing = _properties.FirstOrDefault(p => p.Name == name);
                if (existing != null)
                {
                    existing.Value = stored;
                }
                else
                {
                    _properties.Add(new JProperty(name, stored));
                }
            }
        }

        public IEnumerable<JProperty> Properties() => _properties;

        public void Add(string name, JToken value)
        {
            if (_properties.Any(p => p.Name == name))
            {
                throw new ArgumentException("Duplicate member " + name);
            }

            this[name] = value;
        }

        public bool Remove(string name) => _properties.RemoveAll(p => p.Name == name) > 0;

        public static JObject Parse(string text)
        {
            var parser = new Parser(text);
            var value = parser.ParseDocument();
            return (JObject)value;
        }

        internal void ReplaceChild(JToken child, JToken replacement)
        {
            var property = _properties.First(p => ReferenceEquals(p.Value, child));
            var stored = From(replacement);
            stored.Parent = this;
            property.Value = stored;
        }

        public override JToken DeepClone()
        {
            var clone = new JObject();
            foreach (var property in _properties)
            {
                clone[property.Name] = property.Value.DeepClone();
            }

            return clone;
        }

        internal override void Write(StringBuilder builder)
        {
            builder.Append('{');
            for (var i = 0; i < _properties.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                JValue.WriteString(builder, _properties[i].Name);
                builder.Append(':');
                _properties[i].Value.Write(builder);
            }

            builder.Append('}');
        }

        internal sealed class Parser
        {
            private readonly string _text;
            private int _position;

            public Parser(string text)
            {
                _text = text;
            }

            public JToken ParseDocument()
            {
                var value = ReadValue();
                SkipWhitespace();
                if (_position != _text.Length)
                {
                    throw new FormatException("Unexpected content after the JSON value.");
                }

                return value;
            }

            private JToken ReadValue()
            {
                SkipWhitespace();
                if (_position >= _text.Length)
                {
                    throw new FormatException("Unexpected end of JSON.");
                }

                switch (_text[_position])
                {
                    case '{':
                        return ReadObject();
                    case '[':
                        return ReadArray();
                    case '"':
                        return new JValue(ReadString());
                    case 't':
                        Literal("true");
                        return new JValue(true);
                    case 'f':
                        Literal("false");
                        return new JValue(false);
                    case 'n':
                        Literal("null");
                        return JValue.CreateNull();
                    default:
                        return ReadNumber();
                }
            }

            private JObject ReadObject()
            {
                Expect('{');
                var result = new JObject();
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    var name = ReadString();
                    SkipWhitespace();
                    Expect(':');
                    result.Add(name, ReadValue());
                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private JArray ReadArray()
            {
                Expect('[');
                var result = new JArray();
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return result;
                }

                while (true)
                {
                    result.Add(ReadValue());
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private string ReadString()
            {
                Expect('"');
                var builder = new StringBuilder();
                while (_position < _text.Length)
                {
                    var character = _text[_position++];
                    if (character == '"')
                    {
                        return builder.ToString();
                    }

                    if (character != '\\')
                    {
                        builder.Append(character);
                        continue;
                    }

                    var escaped = _text[_position++];
                    switch (escaped)
                    {
                        case 'n':
                            builder.Append('\n');
                            break;
                        case 'r':
                            builder.Append('\r');
                            break;
                        case 't':
                            builder.Append('\t');
                            break;
                        case 'b':
                            builder.Append('\b');
                            break;
                        case 'f':
                            builder.Append('\f');
                            break;
                        case 'u':
                            builder.Append((char)int.Parse(_text.Substring(_position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _position += 4;
                            break;
                        default:
                            builder.Append(escaped);
                            break;
                    }
                }

                throw new FormatException("Unterminated JSON string.");
            }

            private JToken ReadNumber()
            {
                var start = _position;
                while (_position < _text.Length && "-0123456789".IndexOf(_text[_position]) >= 0)
                {
                    _position++;
                }

                if (start == _position || !long.TryParse(_text.Substring(start, _position - start), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                {
                    throw new FormatException("Expected an integer at offset " + start.ToString(CultureInfo.InvariantCulture));
                }

                return new JValue(number);
            }

            private void Literal(string expected)
            {
                if (string.CompareOrdinal(_text, _position, expected, 0, expected.Length) != 0)
                {
                    throw new FormatException("Expected " + expected);
                }

                _position += expected.Length;
            }

            private void SkipWhitespace()
            {
                while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
                {
                    _position++;
                }
            }

            private void Expect(char expected)
            {
                SkipWhitespace();
                if (!TryConsume(expected))
                {
                    throw new FormatException("Expected '" + expected + "' at offset " + _position.ToString(CultureInfo.InvariantCulture));
                }
            }

            private bool TryConsume(char expected)
            {
                if (_position < _text.Length && _text[_position] == expected)
                {
                    _position++;
                    return true;
                }

                return false;
            }
        }
    }

    public sealed class JArray : JToken
    {
        private readonly List<JToken> _items = new List<JToken>();

        public JArray(params object[] content)
        {
            foreach (var item in content)
            {
                Add(From(item));
            }
        }

        public override JToken this[int index]
        {
            get => _items[index];
            set
            {
                var stored = From(value);
                stored.Parent = this;
                _items[index] = stored;
            }
        }

        public void Add(JToken item)
        {
            var stored = From(item);
            stored.Parent = this;
            _items.Add(stored);
        }

        public void Clear() => _items.Clear();

        public void RemoveAt(int index) => _items.RemoveAt(index);

        internal void ReplaceChild(JToken child, JToken replacement)
        {
            var index = _items.FindIndex(item => ReferenceEquals(item, child));
            this[index] = replacement;
        }

        public override JToken DeepClone()
        {
            var clone = new JArray();
            foreach (var item in _items)
            {
                clone.Add(item.DeepClone());
            }

            return clone;
        }

        internal override void Write(StringBuilder builder)
        {
            builder.Append('[');
            for (var i = 0; i < _items.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                _items[i].Write(builder);
            }

            builder.Append(']');
        }

        public override IEnumerator<JToken> GetEnumerator() => _items.GetEnumerator();
    }
}
