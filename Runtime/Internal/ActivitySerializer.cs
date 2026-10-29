using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BerTaDev.DiscordRichPresence.Internal
{
    /// <summary>
    /// Turns a <see cref="RichPresence"/> into the "activity" JSON Discord expects.
    /// Values Discord would reject (and that would make it drop the whole presence)
    /// are fixed up or left out, with a warning.
    /// </summary>
    internal static class ActivitySerializer
    {
        public const int MaxTextLength = 128;
        public const int MaxButtonLabelLength = 32;
        public const int MaxButtonUrlLength = 512;
        public const int MaxButtons = 2;

        public static string Serialize(RichPresence presence, List<string> warnings = null)
        {
            if (presence == null) return null;

            var sb = new StringBuilder(256);
            sb.Append('{');
            bool first = true;

            if (presence.Type != ActivityType.Playing)
                WriteNumber(sb, ref first, "type", (int)presence.Type);
            if (presence.StatusDisplay != StatusDisplayType.Name)
                WriteNumber(sb, ref first, "status_display_type", (int)presence.StatusDisplay);

            WriteString(sb, ref first, "details", Text(presence.Details));
            WriteString(sb, ref first, "state", Text(presence.State));

            if (presence.StartTimestamp > 0 || presence.EndTimestamp > 0)
            {
                WriteName(sb, ref first, "timestamps");
                sb.Append('{');
                bool inner = true;
                if (presence.StartTimestamp > 0) WriteNumber(sb, ref inner, "start", presence.StartTimestamp);
                if (presence.EndTimestamp > 0) WriteNumber(sb, ref inner, "end", presence.EndTimestamp);
                sb.Append('}');
            }

            string largeImage = NonEmpty(presence.LargeImageKey);
            string smallImage = NonEmpty(presence.SmallImageKey);
            string largeText = Text(presence.LargeImageText);
            string smallText = Text(presence.SmallImageText);
            if (largeImage != null || smallImage != null || largeText != null || smallText != null)
            {
                WriteName(sb, ref first, "assets");
                sb.Append('{');
                bool inner = true;
                WriteString(sb, ref inner, "large_image", largeImage);
                WriteString(sb, ref inner, "large_text", largeText);
                WriteString(sb, ref inner, "small_image", smallImage);
                WriteString(sb, ref inner, "small_text", smallText);
                sb.Append('}');
            }

            WriteParty(sb, ref first, presence, warnings);
            WriteButtons(sb, ref first, presence.Buttons, warnings);

            sb.Append('}');
            return sb.ToString();
        }

        static void WriteParty(StringBuilder sb, ref bool first, RichPresence presence, List<string> warnings)
        {
            string id = NonEmpty(presence.PartyId);
            bool hasSize = presence.PartySize > 0 || presence.PartyMax > 0;
            bool validSize = presence.PartySize >= 1 && presence.PartyMax >= presence.PartySize;
            if (hasSize && !validSize)
                warnings?.Add("Party size ignored: needs 1 <= size <= max (got " + presence.PartySize + " of " + presence.PartyMax + ").");

            if (id == null && !validSize) return;

            WriteName(sb, ref first, "party");
            sb.Append('{');
            bool inner = true;
            WriteString(sb, ref inner, "id", id);
            if (validSize)
            {
                WriteName(sb, ref inner, "size");
                sb.Append('[')
                  .Append(presence.PartySize.ToString(CultureInfo.InvariantCulture))
                  .Append(',')
                  .Append(presence.PartyMax.ToString(CultureInfo.InvariantCulture))
                  .Append(']');
            }
            sb.Append('}');
        }

        static void WriteButtons(StringBuilder sb, ref bool first, List<PresenceButton> buttons, List<string> warnings)
        {
            if (buttons == null || buttons.Count == 0) return;

            int written = 0;
            foreach (PresenceButton button in buttons)
            {
                string label = button.Label?.Trim();
                string url = button.Url?.Trim();
                if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(url)) continue;

                if (written == MaxButtons)
                {
                    warnings?.Add("Discord shows at most " + MaxButtons + " buttons; extra buttons were ignored.");
                    break;
                }
                if (string.IsNullOrEmpty(label))
                {
                    warnings?.Add("Button ignored: label is empty.");
                    continue;
                }
                if (!IsHttpUrl(url) || url.Length > MaxButtonUrlLength)
                {
                    warnings?.Add("Button '" + label + "' ignored: URL must start with http:// or https:// and be at most " + MaxButtonUrlLength + " characters.");
                    continue;
                }

                if (written == 0)
                {
                    WriteName(sb, ref first, "buttons");
                    sb.Append('[');
                }
                else
                {
                    sb.Append(',');
                }

                sb.Append('{');
                bool inner = true;
                WriteString(sb, ref inner, "label", Truncate(label, MaxButtonLabelLength));
                WriteString(sb, ref inner, "url", url);
                sb.Append('}');
                written++;
            }

            if (written > 0) sb.Append(']');
        }

        /// <summary>Discord rejects text shorter than 2 or longer than 128 characters.</summary>
        internal static string Text(string value)
        {
            if (value == null) return null;
            value = value.Trim();
            if (value.Length == 0) return null;
            if (value.Length == 1) return value + "​"; // invisible padding up to the 2 character minimum
            return Truncate(value, MaxTextLength);
        }

        static string NonEmpty(string value)
        {
            value = value?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        internal static string Truncate(string value, int maxLength)
        {
            if (value.Length <= maxLength) return value;
            int cut = maxLength - 1;
            if (char.IsHighSurrogate(value[cut - 1])) cut--; // don't split an emoji in half
            return value.Substring(0, cut) + "…";
        }

        static bool IsHttpUrl(string url) =>
            url != null &&
            (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
             url.StartsWith("http://", StringComparison.OrdinalIgnoreCase));

        static void WriteName(StringBuilder sb, ref bool first, string key)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(key).Append("\":");
        }

        static void WriteString(StringBuilder sb, ref bool first, string key, string value)
        {
            if (value == null) return;
            WriteName(sb, ref first, key);
            Json.WriteString(sb, value);
        }

        static void WriteNumber(StringBuilder sb, ref bool first, string key, long value)
        {
            WriteName(sb, ref first, key);
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }
    }
}
