using System;
using System.Collections.Generic;
using UnityEngine;

namespace BerTaDev.DiscordRichPresence
{
    /// <summary>
    /// Everything Discord shows on your profile while the game runs.
    /// Every field is optional; empty fields are simply left out.
    /// </summary>
    /// <example>
    /// <code>
    /// DiscordPresence.Set(new RichPresence()
    ///     .WithDetails("Exploring the dungeon")
    ///     .WithState("Floor 3")
    ///     .WithLargeImage("logo", "My Game")
    ///     .WithElapsedTime());
    /// </code>
    /// </example>
    [Serializable]
    public sealed class RichPresence
    {
        [Tooltip("The verb shown before the game name: Playing, Listening to, Watching, Competing in.")]
        [SerializeField] ActivityType type = ActivityType.Playing;

        [Tooltip("Which text Discord shows in the member list. Name = the application name.")]
        [SerializeField] StatusDisplayType statusDisplay = StatusDisplayType.Name;

        [Tooltip("First line, e.g. \"Exploring the dungeon\". 2-128 characters.")]
        [SerializeField] string details;

        [Tooltip("Second line, e.g. \"Floor 3\". 2-128 characters.")]
        [SerializeField] string state;

        [Header("Images")]
        [Tooltip("Asset key uploaded under Rich Presence > Art Assets, or an https:// image URL.")]
        [SerializeField] string largeImageKey;

        [Tooltip("Tooltip shown when hovering the large image.")]
        [SerializeField] string largeImageText;

        [Tooltip("Asset key or https:// URL for the small round badge.")]
        [SerializeField] string smallImageKey;

        [Tooltip("Tooltip shown when hovering the small image.")]
        [SerializeField] string smallImageText;

        [Header("Party")]
        [Tooltip("Optional unique id of the player's party/lobby.")]
        [SerializeField] string partyId;

        [Tooltip("Current party size. Shown as \"(size of max)\" when both are set.")]
        [SerializeField, Min(0)] int partySize;

        [Tooltip("Maximum party size.")]
        [SerializeField, Min(0)] int partyMax;

        [Header("Buttons")]
        [Tooltip("Up to two link buttons. Note: Discord does not show them to yourself, only to others.")]
        [SerializeField] List<PresenceButton> buttons = new List<PresenceButton>();

        // Timestamps are runtime values; components set them through TimestampMode.
        [NonSerialized] long startTimestamp;
        [NonSerialized] long endTimestamp;

        public ActivityType Type { get => type; set => type = value; }
        public StatusDisplayType StatusDisplay { get => statusDisplay; set => statusDisplay = value; }
        public string Details { get => details; set => details = value; }
        public string State { get => state; set => state = value; }
        public string LargeImageKey { get => largeImageKey; set => largeImageKey = value; }
        public string LargeImageText { get => largeImageText; set => largeImageText = value; }
        public string SmallImageKey { get => smallImageKey; set => smallImageKey = value; }
        public string SmallImageText { get => smallImageText; set => smallImageText = value; }
        public string PartyId { get => partyId; set => partyId = value; }
        public int PartySize { get => partySize; set => partySize = value; }
        public int PartyMax { get => partyMax; set => partyMax = value; }

        /// <summary>Link buttons (max two are sent).</summary>
        public List<PresenceButton> Buttons => buttons ?? (buttons = new List<PresenceButton>());

        /// <summary>When set, Discord shows "XX:XX elapsed" counting from this moment.</summary>
        public DateTime? StartTime
        {
            get => FromUnixMs(startTimestamp);
            set => startTimestamp = ToUnixMs(value);
        }

        /// <summary>When set, Discord shows "XX:XX left" counting down to this moment.</summary>
        public DateTime? EndTime
        {
            get => FromUnixMs(endTimestamp);
            set => endTimestamp = ToUnixMs(value);
        }

        internal long StartTimestamp => startTimestamp;
        internal long EndTimestamp => endTimestamp;

        public RichPresence WithType(ActivityType value) { type = value; return this; }
        public RichPresence WithStatusDisplay(StatusDisplayType value) { statusDisplay = value; return this; }
        public RichPresence WithDetails(string value) { details = value; return this; }
        public RichPresence WithState(string value) { state = value; return this; }

        public RichPresence WithLargeImage(string key, string text = null)
        {
            largeImageKey = key;
            largeImageText = text;
            return this;
        }

        public RichPresence WithSmallImage(string key, string text = null)
        {
            smallImageKey = key;
            smallImageText = text;
            return this;
        }

        /// <summary>Shows "(size of max)" next to the state.</summary>
        public RichPresence WithParty(int size, int max, string id = null)
        {
            partySize = size;
            partyMax = max;
            partyId = id;
            return this;
        }

        /// <summary>Adds a link button. Discord shows at most two.</summary>
        public RichPresence WithButton(string label, string url)
        {
            Buttons.Add(new PresenceButton(label, url));
            return this;
        }

        /// <summary>Starts an "elapsed" timer from now.</summary>
        public RichPresence WithElapsedTime() => WithStartTime(DateTime.UtcNow);

        /// <summary>Starts an "elapsed" timer from the given moment.</summary>
        public RichPresence WithStartTime(DateTime start)
        {
            StartTime = start;
            endTimestamp = 0;
            return this;
        }

        /// <summary>Shows a "left" countdown that ends after <paramref name="remaining"/>.</summary>
        public RichPresence WithCountdown(TimeSpan remaining) => WithEndTime(DateTime.UtcNow + remaining);

        /// <summary>Shows a "left" countdown that ends at the given moment.</summary>
        public RichPresence WithEndTime(DateTime end)
        {
            startTimestamp = 0;
            EndTime = end;
            return this;
        }

        public RichPresence ClearTimestamps()
        {
            startTimestamp = 0;
            endTimestamp = 0;
            return this;
        }

        public RichPresence Clone()
        {
            var copy = (RichPresence)MemberwiseClone();
            copy.buttons = buttons != null ? new List<PresenceButton>(buttons) : new List<PresenceButton>();
            return copy;
        }

        static long ToUnixMs(DateTime? value) =>
            value.HasValue ? new DateTimeOffset(value.Value.ToUniversalTime()).ToUnixTimeMilliseconds() : 0;

        static DateTime? FromUnixMs(long value) =>
            value > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime : (DateTime?)null;
    }
}
