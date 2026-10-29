namespace BerTaDev.DiscordRichPresence
{
    /// <summary>The verb Discord shows in front of your game's name.</summary>
    public enum ActivityType
    {
        /// <summary>"Playing {name}"</summary>
        Playing = 0,
        /// <summary>"Listening to {name}"</summary>
        Listening = 2,
        /// <summary>"Watching {name}"</summary>
        Watching = 3,
        /// <summary>"Competing in {name}"</summary>
        Competing = 5,
    }

    /// <summary>Which field Discord shows in the member list / status line.</summary>
    public enum StatusDisplayType
    {
        /// <summary>The application name (Discord's default).</summary>
        Name = 0,
        /// <summary>The <see cref="RichPresence.State"/> text.</summary>
        State = 1,
        /// <summary>The <see cref="RichPresence.Details"/> text.</summary>
        Details = 2,
    }

    /// <summary>How the components fill in the elapsed-time timer.</summary>
    public enum TimestampMode
    {
        /// <summary>No timer.</summary>
        None,
        /// <summary>Time since the game was launched. Survives scene changes.</summary>
        SinceGameStart,
        /// <summary>Time since the component applied its presence (usually the scene load).</summary>
        SinceSceneLoad,
    }
}
