using System.Globalization;

namespace BerTaDev.DiscordRichPresence
{
    /// <summary>The Discord account the game connected to.</summary>
    public sealed class DiscordUser
    {
        public DiscordUser(string id, string username, string globalName, string avatarHash)
        {
            Id = id;
            Username = username;
            GlobalName = globalName;
            AvatarHash = avatarHash;
        }

        /// <summary>Snowflake id of the user.</summary>
        public string Id { get; }

        /// <summary>Unique @username.</summary>
        public string Username { get; }

        /// <summary>Display name chosen by the user, or null.</summary>
        public string GlobalName { get; }

        /// <summary>Avatar hash, or null when the user has the default avatar.</summary>
        public string AvatarHash { get; }

        /// <summary>The name Discord shows: the global name if set, otherwise the username.</summary>
        public string DisplayName => string.IsNullOrEmpty(GlobalName) ? Username : GlobalName;

        /// <summary>CDN URL of the user's avatar (animated avatars return a GIF).</summary>
        /// <param name="size">Power of two between 16 and 4096.</param>
        public string GetAvatarUrl(int size = 128)
        {
            if (string.IsNullOrEmpty(AvatarHash))
            {
                ulong.TryParse(Id, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id);
                return "https://cdn.discordapp.com/embed/avatars/" + ((id >> 22) % 6) + ".png";
            }

            string extension = AvatarHash.StartsWith("a_") ? "gif" : "png";
            return "https://cdn.discordapp.com/avatars/" + Id + "/" + AvatarHash + "." + extension + "?size=" + size;
        }

        public override string ToString() => DisplayName + " (" + Id + ")";
    }

    /// <summary>An error reported by Discord, e.g. an invalid application id.</summary>
    public sealed class DiscordError
    {
        public DiscordError(int code, string message)
        {
            Code = code;
            Message = message;
        }

        /// <summary>Discord RPC error or close code. -1 for errors raised by this package.</summary>
        public int Code { get; }

        public string Message { get; }

        public override string ToString() => Message + " (code " + Code + ")";
    }
}
