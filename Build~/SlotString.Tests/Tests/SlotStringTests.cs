using NUnit.Framework;

namespace SlotStrings.Tests
{
    [TestFixture]
    public class SlotStringTests
    {
        // ---- Construction guards --------------------------------------------

        [Test]
        public void Ctor_NullHost_Throws()
        {
            Assert.That(
                () => new SlotString("x", null),
                Throws.ArgumentNullException);
        }

        [Test]
        public void Ctor_NullTemplate_Throws()
        {
            var host = new FakeHost(_ => "X");

            Assert.That(
                () => new SlotString((SlotStringTemplate)null, host),
                Throws.ArgumentNullException);
        }

        // ---- Basic rendering ------------------------------------------------

        [Test]
        public void ToString_RendersInitialValue()
        {
            var host = FakeHost.FromDictionary(new() { [0] = "alpha" });
            var s = new SlotString("v=${0}", host);

            Assert.That(s.ToString(), Is.EqualTo("v=alpha"));
        }

        [Test]
        public void Ctor_AcceptsPrebuiltTemplate()
        {
            var template = new SlotStringTemplate("v=${0}");
            var host = FakeHost.FromDictionary(new() { [0] = "alpha" });
            var s = new SlotString(template, host);

            Assert.That(s.ToString(), Is.EqualTo("v=alpha"));
        }

        // ---- Cache: reuses output while host state token is unchanged -------

        [Test]
        public void ToString_SameStateToken_DoesNotReaccessHost()
        {
            var host = FakeHost.FromDictionary(new() { [0] = "alpha" });
            var s = new SlotString("v=${0}", host);

            // First call resolves and primes the cache.
            _ = s.ToString();
            int callsAfterFirst = host.AccessCalls;

            // Second call with the same state token must not re-query the host.
            _ = s.ToString();
            Assert.That(host.AccessCalls, Is.EqualTo(callsAfterFirst));
        }

        [Test]
        public void ToString_StateTokenChanged_RecomputesAndUpdatesCache()
        {
            var values = new System.Collections.Generic.Dictionary<int, string>
            {
                [0] = "alpha",
            };
            var host = FakeHost.FromDictionary(values);
            var s = new SlotString("v=${0}", host);

            Assert.That(s.ToString(), Is.EqualTo("v=alpha"));

            // Mutate the backing data and signal a state change.
            values[0] = "beta";
            host.BumpToken();

            Assert.That(s.ToString(), Is.EqualTo("v=beta"));
            // Subsequent same-token call still reuses cache.
            int callsAfter = host.AccessCalls;
            _ = s.ToString();
            Assert.That(host.AccessCalls, Is.EqualTo(callsAfter));
        }

        // ---- ToStringForce: always recomputes, never updates cache ----------

        [Test]
        public void ToStringForce_AlwaysRecomputes()
        {
            var values = new System.Collections.Generic.Dictionary<int, string>
            {
                [0] = "alpha",
            };
            var host = FakeHost.FromDictionary(values);
            var s = new SlotString("v=${0}", host);

            int callsBefore = host.AccessCalls;
            _ = s.ToStringForce();
            _ = s.ToStringForce();
            // Two force calls → two re-resolves regardless of version state.
            Assert.That(host.AccessCalls, Is.EqualTo(callsBefore + 2));
        }

        [Test]
        public void ToStringForce_UpdatesCache_SoSubsequentToStringSeesNewValue()
        {
            // ToStringForce has just paid the cost of recomputing, so it should
            // also write the fresh value back into the cache. Otherwise the
            // following sequence produces a confusing "newer than newest" state:
            //   1. host data mutated without bumping the state token
            //   2. ToStringForce() returns the new value
            //   3. ToString() still returns the OLD cached value
            // This test pins down the corrected semantics: after a force, a
            // following ToString returns the freshly-forced value (until the
            // next host state-token change invalidates again).
            var values = new System.Collections.Generic.Dictionary<int, string>
            {
                [0] = "alpha",
            };
            var host = FakeHost.FromDictionary(values);
            var s = new SlotString("v=${0}", host);

            Assert.That(s.ToString(), Is.EqualTo("v=alpha"));

            // Mutate without bumping the token (escape-hatch scenario: host
            // forgot to bump, or caller wants to bypass the token protocol).
            values[0] = "beta";

            Assert.That(s.ToStringForce(), Is.EqualTo("v=beta"));
            // Cache was refreshed by force → ToString returns the new value
            // even though the host token was never bumped.
            Assert.That(s.ToString(), Is.EqualTo("v=beta"));

            // And the subsequent ToString must NOT call Access again — it
            // should hit the freshly-warmed cache.
            int callsAfterToString = host.AccessCalls;
            _ = s.ToString();
            Assert.That(host.AccessCalls, Is.EqualTo(callsAfterToString));
        }
    }
}
