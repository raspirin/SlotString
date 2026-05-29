using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace SlotStrings.Tests
{
    /// <summary>
    /// Randomized invariant checks. Each test takes a seed via [TestCase] so
    /// failures are reproducible: the seed plus the per-iteration counter
    /// printed in the assertion message uniquely identifies the offending
    /// input. Iteration counts are kept modest to keep CI fast.
    /// </summary>
    [TestFixture]
    public class FuzzTests
    {
        // ---- Random raw generators ------------------------------------------

        // Alphabet biased toward characters that interact with the parser:
        // '$', '{', '}', digits provoke real and fake placeholder boundaries;
        // ordinary letters/whitespace produce plain literal runs.
        private static readonly char[] FuzzAlphabet =
            "abcXY 12$${}".ToCharArray();

        // Same alphabet without '$' — used when we want to guarantee no
        // placeholder can possibly be parsed out of the input.
        private static readonly char[] NoDollarAlphabet =
            "abcXY 12{}".ToCharArray();

        private static string RandomRaw(System.Random rng, int maxLen)
        {
            int length = rng.Next(0, maxLen + 1);
            var builder = new StringBuilder(length + 8);

            for (int i = 0; i < length; i++)
            {
                // 10% chance to splice in a well-formed single-digit placeholder.
                // Single-digit keeps the placeholder lossless under
                // re-emission ("${00}" would normalize to "${0}").
                if (rng.Next(10) == 0)
                {
                    builder.Append("${").Append(rng.Next(0, 10)).Append('}');
                }
                else
                {
                    builder.Append(FuzzAlphabet[rng.Next(FuzzAlphabet.Length)]);
                }
            }

            return builder.ToString();
        }

        private static string RandomLiteralOnlyRaw(System.Random rng, int maxLen)
        {
            int length = rng.Next(0, maxLen + 1);
            var builder = new StringBuilder(length);

            for (int i = 0; i < length; i++)
            {
                builder.Append(NoDollarAlphabet[rng.Next(NoDollarAlphabet.Length)]);
            }

            return builder.ToString();
        }

        // ---- Parser invariants ----------------------------------------------

        [TestCase(1)]
        [TestCase(42)]
        [TestCase(1337)]
        [TestCase(2026)]
        public void Parser_NeverThrows_ForRandomInput(int seed)
        {
            var rng = new System.Random(seed);

            for (int iteration = 0; iteration < 1000; iteration++)
            {
                string raw = RandomRaw(rng, maxLen: 64);

                Assert.DoesNotThrow(
                    () => new SlotStringTemplate(raw),
                    $"seed={seed}, iter={iteration}, raw={raw}");
            }
        }

        [TestCase(1)]
        [TestCase(42)]
        [TestCase(1337)]
        public void Parser_IsIdempotentUnderReconstruct(int seed)
        {
            // Property: parse(raw) → segments. If we reconstruct a string by
            // emitting "${N}" for placeholders and verbatim text for literals,
            // re-parsing it must produce the *same* segment sequence.
            //
            // We can't assert "rebuilt == raw" because the parser normalizes
            // leading-zero indices ("${00}" → ${0}). The fixed-point version
            // captures the real semantic invariant: parsing is idempotent.
            var rng = new System.Random(seed);

            for (int iteration = 0; iteration < 500; iteration++)
            {
                string raw = RandomRaw(rng, maxLen: 64);
                var first = new SlotStringTemplate(raw);
                string rebuilt = ReconstructFromSegments(first.Segments);
                var second = new SlotStringTemplate(rebuilt);

                AssertSameSegments(first.Segments, second.Segments,
                    $"seed={seed}, iter={iteration}, raw={raw}, rebuilt={rebuilt}");
            }
        }

        [TestCase(1)]
        [TestCase(42)]
        public void Parser_NoAdjacentLiteralSegments(int seed)
        {
            // The parser only emits a literal segment immediately before a
            // placeholder or at end-of-input, so two literals in a row should
            // never occur in the segment list.
            var rng = new System.Random(seed);

            for (int iteration = 0; iteration < 500; iteration++)
            {
                string raw = RandomRaw(rng, maxLen: 64);
                var template = new SlotStringTemplate(raw);
                var segments = template.Segments;

                for (int j = 1; j < segments.Count; j++)
                {
                    bool adjacentLiterals =
                        !segments[j - 1].IsPlaceholder && !segments[j].IsPlaceholder;

                    Assert.That(adjacentLiterals, Is.False,
                        $"seed={seed}, iter={iteration}, raw={raw}, position={j}");
                }
            }
        }

        [TestCase(1)]
        [TestCase(42)]
        public void Parser_PlaceholderIndicesAreNonNegative(int seed)
        {
            var rng = new System.Random(seed);

            for (int iteration = 0; iteration < 500; iteration++)
            {
                string raw = RandomRaw(rng, maxLen: 64);
                var template = new SlotStringTemplate(raw);

                foreach (var segment in template.Segments)
                {
                    if (segment.IsPlaceholder)
                    {
                        Assert.That(segment.PlaceholderIndex, Is.GreaterThanOrEqualTo(0),
                            $"seed={seed}, iter={iteration}, raw={raw}");
                    }
                }
            }
        }

        // ---- Format invariants ----------------------------------------------

        [TestCase(1)]
        [TestCase(42)]
        [TestCase(1337)]
        public void Format_NeverThrows_WhenHostReturnsNonNull(int seed)
        {
            // Pairs the parser's "never throws" guarantee with the format-time
            // contract: as long as Access returns non-null, Format succeeds.
            var rng = new System.Random(seed);

            for (int iteration = 0; iteration < 500; iteration++)
            {
                string raw = RandomRaw(rng, maxLen: 64);
                var template = new SlotStringTemplate(raw);
                var host = new FakeHost(_ => "v");

                Assert.DoesNotThrow(
                    () => template.Format(host),
                    $"seed={seed}, iter={iteration}, raw={raw}");
            }
        }

        [TestCase(1)]
        [TestCase(42)]
        public void Format_LiteralOnlyRaw_DoesNotCallAccess(int seed)
        {
            // No '$' in input → parser can't produce placeholder segments →
            // Format must not touch the host at all.
            var rng = new System.Random(seed);

            for (int iteration = 0; iteration < 200; iteration++)
            {
                string raw = RandomLiteralOnlyRaw(rng, maxLen: 32);
                var template = new SlotStringTemplate(raw);
                var host = new FakeHost(_ => "v");

                _ = template.Format(host);

                Assert.That(host.AccessCalls, Is.EqualTo(0),
                    $"seed={seed}, iter={iteration}, raw={raw}");
            }
        }

        [TestCase(1)]
        [TestCase(42)]
        public void Format_LiteralOnlyRaw_RoundTrips(int seed)
        {
            // No-placeholder input must format to itself byte-for-byte.
            var rng = new System.Random(seed);

            for (int iteration = 0; iteration < 200; iteration++)
            {
                string raw = RandomLiteralOnlyRaw(rng, maxLen: 32);
                var template = new SlotStringTemplate(raw);
                var host = new FakeHost(_ => "WRONG");

                Assert.That(template.Format(host), Is.EqualTo(raw),
                    $"seed={seed}, iter={iteration}");
            }
        }

        [TestCase(1)]
        [TestCase(42)]
        public void Format_HostReturnsNullOnUsedIndex_Throws(int seed)
        {
            // When the parsed template references at least one placeholder
            // index and the host returns null for it, Format must throw the
            // documented InvalidOperationException — not silently emit a
            // marker string into user-visible output.
            var rng = new System.Random(seed);
            int trials = 0;

            for (int iteration = 0; iteration < 1000 && trials < 50; iteration++)
            {
                string raw = RandomRaw(rng, maxLen: 64);
                var template = new SlotStringTemplate(raw);

                bool hasPlaceholder = false;
                foreach (var segment in template.Segments)
                {
                    if (segment.IsPlaceholder) { hasPlaceholder = true; break; }
                }

                if (!hasPlaceholder) continue;
                trials++;

                var host = new FakeHost(_ => null);

                Assert.That(
                    () => template.Format(host),
                    Throws.InvalidOperationException,
                    $"seed={seed}, iter={iteration}, raw={raw}");
            }

            Assert.That(trials, Is.GreaterThan(0),
                $"seed={seed}: random generator never produced a placeholder; widen alphabet");
        }

        // ---- SlotString cache invariants ------------------------------------

        [TestCase(1)]
        [TestCase(42)]
        [TestCase(1337)]
        public void SlotString_CacheStateMachine_MatchesShadowModel(int seed)
        {
            // Drive a SlotString through random sequences of operations and
            // compare each ToString / ToStringForce call against a shadow
            // model that knows when the cache should be hit vs missed.
            //
            // Operation set:
            //   - ToString
            //   - ToStringForce
            //   - mutate underlying data (no token change)
            //   - bump token
            //   - set token to a previously-seen value (non-monotonic case)
            const int placeholderCount = 2; // raw uses ${0} and ${1}.

            var rng = new System.Random(seed);

            for (int trial = 0; trial < 30; trial++)
            {
                var values = new Dictionary<int, string> { [0] = "v0", [1] = "v1" };
                var host = FakeHost.FromDictionary(values);
                var slot = new SlotString("a=${0},b=${1}", host);

                // Shadow model. The cache-hit predicate is purely equality of
                // the current token against the token recorded at last cache
                // write — not "did the token ever change since". A token that
                // bumps to N and then returns to its previous value still
                // matches: that's the equality-based contract documented for
                // ISlotStringHost.GetStateToken (counters and hashes are both
                // valid). Hence we don't invalidate on token-change ops; we
                // only recompare on read ops.
                bool hasCachedOutput = false;
                int cachedToken = 0;

                for (int step = 0; step < 60; step++)
                {
                    int op = rng.Next(0, 5);

                    switch (op)
                    {
                        case 0: // ToString
                        {
                            int currentToken = host.GetStateToken();
                            int expected = hasCachedOutput && cachedToken == currentToken
                                ? 0
                                : placeholderCount;

                            int before = host.AccessCalls;
                            _ = slot.ToString();
                            int delta = host.AccessCalls - before;

                            Assert.That(delta, Is.EqualTo(expected),
                                $"seed={seed}, trial={trial}, step={step}, op=ToString, " +
                                $"hasCachedOutput={hasCachedOutput}, cachedToken={cachedToken}, currentToken={currentToken}");

                            hasCachedOutput = true;
                            cachedToken = currentToken;
                            break;
                        }

                        case 1: // ToStringForce
                        {
                            int before = host.AccessCalls;
                            _ = slot.ToStringForce();
                            int delta = host.AccessCalls - before;

                            Assert.That(delta, Is.EqualTo(placeholderCount),
                                $"seed={seed}, trial={trial}, step={step}, op=ToStringForce did not access host");

                            // Force must also warm the cache: a follow-up
                            // ToString with no token change must hit the cache.
                            int afterForce = host.AccessCalls;
                            _ = slot.ToString();
                            Assert.That(host.AccessCalls, Is.EqualTo(afterForce),
                                $"seed={seed}, trial={trial}, step={step}, op=ToStringForce did not warm cache");

                            hasCachedOutput = true;
                            cachedToken = host.GetStateToken();
                            break;
                        }

                        case 2: // mutate data without bumping token
                            // Cache state in the protocol sense is unchanged:
                            // the next ToString still hits the cache and
                            // returns the (now stale) cached output. That's
                            // the documented failure mode users must avoid;
                            // here we are verifying the cache really does
                            // hold despite the drift.
                            values[rng.Next(0, placeholderCount)] = "x" + rng.Next();
                            break;

                        case 3: // bump token (monotonic case)
                            host.BumpToken();
                            break;

                        case 4: // arbitrary token change (may go backwards or repeat — non-monotonic case)
                            host.SetToken(rng.Next(-5, 6));
                            break;
                    }
                }
            }
        }

        [TestCase(1)]
        [TestCase(42)]
        public void SlotString_StableTokenStableOutput(int seed)
        {
            // Strong invariant: while the host token is held constant, ToString
            // returns identical output on every call regardless of underlying
            // data churn. (Data drift without a token bump is the documented
            // "stale read" failure mode — and this test verifies it really is
            // a *stable* stale, not "sometimes stale, sometimes fresh".)
            var rng = new System.Random(seed);
            var values = new Dictionary<int, string> { [0] = "alpha" };
            var host = FakeHost.FromDictionary(values);
            var slot = new SlotString("v=${0}", host);

            string first = slot.ToString();

            for (int iteration = 0; iteration < 200; iteration++)
            {
                values[0] = "garbage" + rng.Next();

                Assert.That(slot.ToString(), Is.EqualTo(first),
                    $"seed={seed}, iter={iteration}");
            }
        }

        // ---- Helpers --------------------------------------------------------

        private static string ReconstructFromSegments(
            IReadOnlyList<SlotStringTemplate.Segment> segments)
        {
            var builder = new StringBuilder();

            foreach (var segment in segments)
            {
                if (segment.IsPlaceholder)
                {
                    builder.Append("${").Append(segment.PlaceholderIndex).Append('}');
                }
                else
                {
                    builder.Append(segment.Literal);
                }
            }

            return builder.ToString();
        }

        private static void AssertSameSegments(
            IReadOnlyList<SlotStringTemplate.Segment> expected,
            IReadOnlyList<SlotStringTemplate.Segment> actual,
            string context)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count),
                $"{context}: segment count mismatch");

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(actual[i].IsPlaceholder, Is.EqualTo(expected[i].IsPlaceholder),
                    $"{context}: segment[{i}] kind mismatch");

                if (expected[i].IsPlaceholder)
                {
                    Assert.That(actual[i].PlaceholderIndex, Is.EqualTo(expected[i].PlaceholderIndex),
                        $"{context}: segment[{i}] placeholder index mismatch");
                }
                else
                {
                    Assert.That(actual[i].Literal, Is.EqualTo(expected[i].Literal),
                        $"{context}: segment[{i}] literal text mismatch");
                }
            }
        }
    }
}
