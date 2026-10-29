using System;
using System.Collections.Generic;
using BerTaDev.DiscordRichPresence.Internal;
using NUnit.Framework;

namespace BerTaDev.DiscordRichPresence.Tests
{
    sealed class ProtocolTests
    {
        [Test]
        public void Json_ParsesReadyPayload()
        {
            const string payload = "{\"cmd\":\"DISPATCH\",\"data\":{\"v\":1,\"config\":{\"cdn_host\":\"cdn.discordapp.com\"},\"user\":{\"id\":\"80351110224678912\",\"username\":\"nelly\",\"global_name\":\"Nelly\",\"avatar\":\"a_8342729096ea3675442027381ff50dfe\",\"bot\":false,\"flags\":32,\"premium_type\":null}},\"evt\":\"READY\",\"nonce\":null}";

            var message = (Dictionary<string, object>)Json.Parse(payload);
            var user = message.GetObject("data").GetObject("user");

            Assert.AreEqual("READY", message.GetString("evt"));
            Assert.AreEqual("80351110224678912", user.GetString("id"));
            Assert.AreEqual("Nelly", user.GetString("global_name"));
            Assert.AreEqual(32L, user.GetLong("flags"));
            Assert.IsNull(user["premium_type"]);
            Assert.AreEqual(false, user["bot"]);
        }

        [Test]
        public void Json_ParsesNumbersArraysAndEscapes()
        {
            var value = (List<object>)Json.Parse(" [ -1, 2.5, 1e3, \"a\\u00e7\\n\", [], {} ] ");

            Assert.AreEqual(-1L, value[0]);
            Assert.AreEqual(2.5, value[1]);
            Assert.AreEqual(1000.0, value[2]);
            Assert.AreEqual("aç\n", value[3]);
            Assert.AreEqual(0, ((List<object>)value[4]).Count);
            Assert.AreEqual(0, ((Dictionary<string, object>)value[5]).Count);
        }

        [Test]
        public void Json_RejectsMalformedInput()
        {
            Assert.Throws<FormatException>(() => Json.Parse("{\"a\":"));
            Assert.Throws<FormatException>(() => Json.Parse("{\"a\":1} x"));
            Assert.Throws<FormatException>(() => Json.Parse("\"unterminated"));
        }

        [Test]
        public void FrameReader_RoundTripsFramesSplitAcrossChunks()
        {
            byte[] first = FrameReader.Encode(Opcode.Frame, "{\"a\":\"ğ\"}", out int firstLength);
            byte[] second = FrameReader.Encode(Opcode.Ping, "{}", out int secondLength);
            var stream = new byte[firstLength + secondLength];
            Buffer.BlockCopy(first, 0, stream, 0, firstLength);
            Buffer.BlockCopy(second, 0, stream, firstLength, secondLength);

            var reader = new FrameReader();
            var frames = new List<(Opcode, string)>();
            for (int i = 0; i < stream.Length; i += 3)
            {
                int length = Math.Min(3, stream.Length - i);
                var chunk = new byte[length];
                Buffer.BlockCopy(stream, i, chunk, 0, length);
                reader.Append(chunk, length);
                while (reader.TryRead(out Opcode opcode, out string payload))
                    frames.Add((opcode, payload));
            }

            Assert.AreEqual(2, frames.Count);
            Assert.AreEqual((Opcode.Frame, "{\"a\":\"ğ\"}"), frames[0]);
            Assert.AreEqual((Opcode.Ping, "{}"), frames[1]);
        }

        [Test]
        public void ApplicationIdValidation()
        {
            Assert.IsTrue(DiscordPresence.IsValidApplicationId("1234567890123456789"));
            Assert.IsFalse(DiscordPresence.IsValidApplicationId(null));
            Assert.IsFalse(DiscordPresence.IsValidApplicationId("12345"));
            Assert.IsFalse(DiscordPresence.IsValidApplicationId("12345678901234567a9"));
        }

        [Test]
        public void DiscordUser_AvatarUrls()
        {
            Assert.AreEqual(
                "https://cdn.discordapp.com/avatars/1/a_abc.gif?size=64",
                new DiscordUser("1", "u", null, "a_abc").GetAvatarUrl(64));
            StringAssert.StartsWith(
                "https://cdn.discordapp.com/embed/avatars/",
                new DiscordUser("80351110224678912", "u", null, null).GetAvatarUrl());
            Assert.AreEqual("u", new DiscordUser("1", "u", "", null).DisplayName);
        }
    }
}
