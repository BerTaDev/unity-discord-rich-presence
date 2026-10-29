using System;
using System.Collections.Generic;
using BerTaDev.DiscordRichPresence.Internal;
using NUnit.Framework;

namespace BerTaDev.DiscordRichPresence.Tests
{
    sealed class ActivitySerializerTests
    {
        static Dictionary<string, object> Parse(RichPresence presence, List<string> warnings = null) =>
            (Dictionary<string, object>)Json.Parse(ActivitySerializer.Serialize(presence, warnings));

        [Test]
        public void EmptyPresence_IsEmptyObject()
        {
            Assert.AreEqual("{}", ActivitySerializer.Serialize(new RichPresence()));
        }

        [Test]
        public void WritesAllFields()
        {
            var start = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var presence = new RichPresence()
                .WithType(ActivityType.Competing)
                .WithStatusDisplay(StatusDisplayType.Details)
                .WithDetails("Ranked match")
                .WithState("In queue")
                .WithLargeImage("logo", "My Game")
                .WithSmallImage("rank", "Gold")
                .WithParty(2, 5, "lobby-1")
                .WithStartTime(start)
                .WithButton("Website", "https://example.com");

            var json = Parse(presence);

            Assert.AreEqual(5L, json["type"]);
            Assert.AreEqual(2L, json["status_display_type"]);
            Assert.AreEqual("Ranked match", json["details"]);
            Assert.AreEqual("In queue", json["state"]);

            var assets = json.GetObject("assets");
            Assert.AreEqual("logo", assets["large_image"]);
            Assert.AreEqual("My Game", assets["large_text"]);
            Assert.AreEqual("rank", assets["small_image"]);
            Assert.AreEqual("Gold", assets["small_text"]);

            var party = json.GetObject("party");
            Assert.AreEqual("lobby-1", party["id"]);
            CollectionAssert.AreEqual(new object[] { 2L, 5L }, (List<object>)party["size"]);

            Assert.AreEqual(new DateTimeOffset(start).ToUnixTimeMilliseconds(), json.GetObject("timestamps")["start"]);

            var button = (Dictionary<string, object>)((List<object>)json["buttons"])[0];
            Assert.AreEqual("Website", button["label"]);
            Assert.AreEqual("https://example.com", button["url"]);
        }

        [Test]
        public void DefaultsAndEmptyStrings_AreOmitted()
        {
            var json = Parse(new RichPresence().WithDetails("   ").WithState("Hello").WithLargeImage(""));

            Assert.AreEqual(1, json.Count);
            Assert.AreEqual("Hello", json["state"]);
        }

        [Test]
        public void EscapesSpecialCharacters()
        {
            const string text = "Quote \" slash \\ newline \n tab \t ğüşıöç 🎮";
            Assert.AreEqual(text, Parse(new RichPresence().WithDetails(text))["details"]);
        }

        [Test]
        public void LongText_IsTruncatedTo128()
        {
            var details = (string)Parse(new RichPresence().WithDetails(new string('a', 300)))["details"];

            Assert.AreEqual(ActivitySerializer.MaxTextLength, details.Length);
            Assert.IsTrue(details.EndsWith("…"));
        }

        [Test]
        public void Truncate_DoesNotSplitSurrogatePairs()
        {
            string text = new string('a', 126) + "🎮🎮🎮";
            string result = ActivitySerializer.Truncate(text, ActivitySerializer.MaxTextLength);

            Assert.LessOrEqual(result.Length, ActivitySerializer.MaxTextLength);
            Assert.IsFalse(char.IsHighSurrogate(result[result.Length - 2]));
        }

        [Test]
        public void SingleCharacterText_IsPaddedToTwo()
        {
            Assert.AreEqual(2, ((string)Parse(new RichPresence().WithState("1"))["state"]).Length);
        }

        [Test]
        public void Buttons_InvalidOnesAndExtrasAreDropped()
        {
            var warnings = new List<string>();
            var presence = new RichPresence()
                .WithButton("No scheme", "example.com")
                .WithButton("One", "https://one.example")
                .WithButton("Two", "http://two.example")
                .WithButton("Three", "https://three.example");

            var buttons = (List<object>)Parse(presence, warnings)["buttons"];

            Assert.AreEqual(2, buttons.Count);
            Assert.AreEqual("One", ((Dictionary<string, object>)buttons[0])["label"]);
            Assert.AreEqual("Two", ((Dictionary<string, object>)buttons[1])["label"]);
            Assert.AreEqual(2, warnings.Count);
        }

        [Test]
        public void InvalidPartySize_IsOmitted()
        {
            var warnings = new List<string>();
            var json = Parse(new RichPresence().WithParty(5, 2), warnings);

            Assert.IsFalse(json.ContainsKey("party"));
            Assert.AreEqual(1, warnings.Count);
        }

        [Test]
        public void Countdown_WritesEndOnly()
        {
            var timestamps = Parse(new RichPresence().WithCountdown(TimeSpan.FromMinutes(5))).GetObject("timestamps");

            Assert.IsFalse(timestamps.ContainsKey("start"));
            Assert.IsTrue(timestamps.ContainsKey("end"));
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var original = new RichPresence().WithDetails("A").WithButton("B", "https://b.example");
            var copy = original.Clone().WithDetails("Changed").WithButton("C", "https://c.example");

            Assert.AreEqual("A", original.Details);
            Assert.AreEqual(1, original.Buttons.Count);
            Assert.AreEqual(2, copy.Buttons.Count);
        }
    }
}
