using System;
using System.Collections.Generic;

namespace SlotStrings.Tests
{
    /// <summary>
    /// Minimal ISlotStringHost double for tests. Wraps a value lookup and a
    /// manually-controlled state token so we can assert caching behaviour
    /// without depending on real game state.
    /// </summary>
    internal sealed class FakeHost : ISlotStringHost
    {
        private readonly Func<int, string> _accessor;
        private int _token;
        public int AccessCalls { get; private set; }

        public FakeHost(Func<int, string> accessor, int initialToken = 0)
        {
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            _token = initialToken;
        }

        // Concrete Dictionary<> so callers can use target-typed `new() { [k] = v }`.
        public static FakeHost FromDictionary(Dictionary<int, string> values, int initialToken = 0)
        {
            return new FakeHost(i => values.TryGetValue(i, out var v) ? v : null, initialToken);
        }

        public string Access(int slot)
        {
            AccessCalls++;
            return _accessor(slot);
        }

        // Counter-style implementation; the production contract also accepts
        // hash-style implementations, but a counter is the simplest mock.
        public int GetStateToken() => _token;

        public void BumpToken() => _token++;

        // Set the token to an arbitrary value, including a previously-seen one
        // or a smaller number. Used by fuzz tests to exercise the equality-based
        // (non-monotonic) cache-invalidation contract.
        public void SetToken(int value) => _token = value;
    }
}
