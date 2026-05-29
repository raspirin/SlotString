using NUnit.Framework;

namespace SlotStrings.Tests
{
    [TestFixture]
    public class SlotStringTemplateTests
    {
        // ---- Parsing: literals ----------------------------------------------

        [Test]
        public void Format_NullRaw_ReturnsEmpty()
        {
            var template = new SlotStringTemplate(null);
            var host = new FakeHost(_ => "X");

            Assert.That(template.Format(host), Is.EqualTo(string.Empty));
        }

        [Test]
        public void Format_EmptyRaw_ReturnsEmpty()
        {
            var template = new SlotStringTemplate(string.Empty);
            var host = new FakeHost(_ => "X");

            Assert.That(template.Format(host), Is.EqualTo(string.Empty));
        }

        [Test]
        public void Format_LiteralOnly_ReturnsRawUnchanged()
        {
            var template = new SlotStringTemplate("hello world");
            var host = new FakeHost(_ => "UNUSED");

            Assert.That(template.Format(host), Is.EqualTo("hello world"));
        }

        // ---- Parsing: placeholders ------------------------------------------

        [Test]
        public void Format_SinglePlaceholder_SubstitutesHostValue()
        {
            var template = new SlotStringTemplate("hi ${0}");
            var host = FakeHost.FromDictionary(new() { [0] = "Alice" });

            Assert.That(template.Format(host), Is.EqualTo("hi Alice"));
        }

        [Test]
        public void Format_MultiplePlaceholders_SubstitutesAllInOrder()
        {
            var template = new SlotStringTemplate("${0} dealt ${1} damage to ${2}");
            var host = FakeHost.FromDictionary(new()
            {
                [0] = "Alice",
                [1] = "42",
                [2] = "Bob",
            });

            Assert.That(template.Format(host), Is.EqualTo("Alice dealt 42 damage to Bob"));
        }

        [Test]
        public void Format_RepeatedPlaceholder_CallsHostEachOccurrence()
        {
            var template = new SlotStringTemplate("${0}-${0}-${0}");
            var host = FakeHost.FromDictionary(new() { [0] = "x" });

            Assert.That(template.Format(host), Is.EqualTo("x-x-x"));
            // Format does not deduplicate; that's an explicit behaviour.
            Assert.That(host.AccessCalls, Is.EqualTo(3));
        }

        [Test]
        public void Format_MultiDigitSlot_ParsesAsSingleSlot()
        {
            var template = new SlotStringTemplate("v=${42}");
            var host = FakeHost.FromDictionary(new() { [42] = "answer" });

            Assert.That(template.Format(host), Is.EqualTo("v=answer"));
        }

        // ---- Parsing: malformed placeholders are kept as literals -----------

        [Test]
        public void Format_EmptyBraces_KeptAsLiteral()
        {
            var template = new SlotStringTemplate("a${}b");
            var host = new FakeHost(_ => "WRONG");

            Assert.That(template.Format(host), Is.EqualTo("a${}b"));
        }

        [Test]
        public void Format_NonDigitPlaceholder_KeptAsLiteral()
        {
            var template = new SlotStringTemplate("a${abc}b");
            var host = new FakeHost(_ => "WRONG");

            Assert.That(template.Format(host), Is.EqualTo("a${abc}b"));
        }

        [Test]
        public void Format_UnclosedBrace_KeptAsLiteral()
        {
            var template = new SlotStringTemplate("a${0");
            var host = new FakeHost(_ => "WRONG");

            Assert.That(template.Format(host), Is.EqualTo("a${0"));
        }

        [Test]
        public void Format_OverflowingSlot_KeptAsLiteral()
        {
            // 19 nines overflows int.MaxValue and the parser falls back to literal.
            var raw = "x${9999999999999999999}y";
            var template = new SlotStringTemplate(raw);
            var host = new FakeHost(_ => "WRONG");

            Assert.That(template.Format(host), Is.EqualTo(raw));
        }

        // ---- Format-time host contract --------------------------------------

        [Test]
        public void Format_NullHost_Throws()
        {
            var template = new SlotStringTemplate("hi ${0}");

            Assert.That(() => template.Format(null), Throws.ArgumentNullException);
        }

        [Test]
        public void Format_HostReturnsNull_Throws()
        {
            // Host contract: Access must return non-null for any slot referenced
            // by the template. Returning null is a contract violation, not a
            // graceful "data missing" path. The library refuses to silently
            // substitute a marker string into the rendered output (which would
            // otherwise leak into user-visible UI). Consumers that want graceful
            // degradation should implement that policy inside their own Access.
            var template = new SlotStringTemplate("hi ${7}");
            var host = new FakeHost(_ => null);

            Assert.That(
                () => template.Format(host),
                Throws.InvalidOperationException
                    // Slot must appear in the message so the offending
                    // placeholder is identifiable from logs / crash reports.
                    .With.Message.Contains("7"));
        }
    }
}
