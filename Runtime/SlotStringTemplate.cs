namespace SlotStrings
{
    /// <summary>Immutable parsed form of a raw template string; can be shared across many <see cref="SlotString"/> instances.</summary>
    public sealed class SlotStringTemplate
    {
        private readonly System.Collections.Generic.IReadOnlyList<Segment> _segments;

        /// <summary>Parses <paramref name="raw"/> into immutable segments; <paramref name="raw"/> is read once and not retained.</summary>
        public SlotStringTemplate(string raw)
        {
            _segments = ParseSegments(raw);
        }

        /// <summary>The parsed segments (literals and placeholders) in source order.</summary>
        public System.Collections.Generic.IReadOnlyList<Segment> Segments => _segments;

        private static System.Collections.Generic.IReadOnlyList<Segment> ParseSegments(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return System.Array.Empty<Segment>();
            }

            var segments = new System.Collections.Generic.List<Segment>();
            int literalStart = 0;
            int index = 0;

            while (index < raw.Length)
            {
                if (raw[index] != '$' || index + 1 >= raw.Length || raw[index + 1] != '{')
                {
                    index++;
                    continue;
                }

                int placeholderStart = index + 2;
                int placeholderEnd = placeholderStart;

                while (placeholderEnd < raw.Length && IsAsciiDigit(raw[placeholderEnd]))
                {
                    placeholderEnd++;
                }

                if (placeholderEnd == placeholderStart || placeholderEnd >= raw.Length || raw[placeholderEnd] != '}')
                {
                    index++;
                    continue;
                }

                if (!TryParseNonNegativeInt(raw, placeholderStart, placeholderEnd, out int placeholderIndex))
                {
                    index++;
                    continue;
                }

                AddLiteralSegment(segments, raw, literalStart, index);
                segments.Add(new Segment(placeholderIndex));

                index = placeholderEnd + 1;
                literalStart = index;
            }

            AddLiteralSegment(segments, raw, literalStart, raw.Length);

            return segments.AsReadOnly();
        }

        /// <summary>Renders the template using <paramref name="host"/> for placeholder values.</summary>
        public string Format(ISlotStringHost host)
        {
            if (host == null)
            {
                throw new System.ArgumentNullException(nameof(host));
            }

            if (_segments.Count == 0)
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder();

            for (int i = 0; i < _segments.Count; i++)
            {
                Segment segment = _segments[i];

                if (segment.IsPlaceholder)
                {
                    int placeholderIndex = segment.PlaceholderIndex;
                    builder.Append(host.Access(placeholderIndex) ?? throw new System.InvalidOperationException(
                        $"ISlotStringHost.Access({placeholderIndex}) returned null; the host must provide a non-null value for every placeholder index referenced by the template."));
                }
                else
                {
                    builder.Append(segment.Literal);
                }
            }

            return builder.ToString();
        }

        private static void AddLiteralSegment(System.Collections.Generic.List<Segment> segments, string raw,
            int startIndex, int endIndex)
        {
            if (endIndex <= startIndex)
            {
                return;
            }

            segments.Add(new Segment(raw.Substring(startIndex, endIndex - startIndex)));
        }

        private static bool TryParseNonNegativeInt(string raw, int startIndex, int endIndex, out int value)
        {
            value = 0;

            for (int i = startIndex; i < endIndex; i++)
            {
                int digit = raw[i] - '0';

                if (value > (int.MaxValue - digit) / 10)
                {
                    value = 0;
                    return false;
                }

                value = value * 10 + digit;
            }

            return true;
        }

        private static bool IsAsciiDigit(char value)
        {
            return value is >= '0' and <= '9';
        }

        /// <summary>A literal text run or a placeholder reference inside a parsed <see cref="SlotStringTemplate"/>.</summary>
        public readonly struct Segment
        {
            private readonly string _literal;
            private readonly int _placeholderIndex;

            /// <summary>Whether this segment is a literal or a placeholder.</summary>
            public SegmentKind Kind { get; }

            /// <summary>True if this segment is a placeholder.</summary>
            public bool IsPlaceholder => Kind == SegmentKind.Placeholder;

            /// <summary>Constructs a literal segment.</summary>
            public Segment(string literal)
            {
                _literal = literal ?? string.Empty;
                _placeholderIndex = -1;
                Kind = SegmentKind.Literal;
            }

            /// <summary>Constructs a placeholder segment referring to <paramref name="placeholderIndex"/>.</summary>
            public Segment(int placeholderIndex)
            {
                if (placeholderIndex < 0)
                {
                    throw new System.ArgumentOutOfRangeException(nameof(placeholderIndex));
                }

                _literal = null;
                _placeholderIndex = placeholderIndex;
                Kind = SegmentKind.Placeholder;
            }


            /// <summary>The literal text; throws if <see cref="Kind"/> is not <see cref="SegmentKind.Literal"/>.</summary>
            public string Literal
            {
                get
                {
                    if (Kind != SegmentKind.Literal)
                    {
                        throw new System.InvalidOperationException("Segment is not a literal.");
                    }

                    return _literal ?? string.Empty;
                }
            }

            /// <summary>The placeholder index; throws if <see cref="Kind"/> is not <see cref="SegmentKind.Placeholder"/>.</summary>
            public int PlaceholderIndex => Kind != SegmentKind.Placeholder
                ? throw new System.InvalidOperationException("Segment is not a placeholder.")
                : _placeholderIndex;

            /// <summary>Discriminator for <see cref="Segment"/>.</summary>
            public enum SegmentKind
            {
                Literal,
                Placeholder
            }
        }
    }
}