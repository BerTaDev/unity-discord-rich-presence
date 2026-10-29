using System;
using UnityEngine;

namespace BerTaDev.DiscordRichPresence
{
    /// <summary>
    /// Sets the presence while this object is enabled. Put one in each scene
    /// ("Main Menu", "Level 1", ...) to describe what the player is doing there, no code needed.
    /// Works together with a <see cref="DiscordPresenceManager"/>, which holds the Application ID.
    /// </summary>
    [AddComponentMenu("Discord/Discord Scene Presence")]
    public sealed class DiscordScenePresence : MonoBehaviour
    {
        [Tooltip("Timer shown with this presence.")]
        [SerializeField] TimestampMode timestamp = TimestampMode.SinceSceneLoad;

        [SerializeField] RichPresence presence = new RichPresence()
            .WithDetails("Playing")
            .WithLargeImage("logo");

        DateTime appliedAt;

        /// <summary>The inspector presence. Call <see cref="Apply"/> after changing it from code.</summary>
        public RichPresence Presence => presence;

        void OnEnable()
        {
            appliedAt = DateTime.UtcNow;
            Apply();
        }

        /// <summary>Pushes this component's presence to Discord.</summary>
        public void Apply() => DiscordPresence.SetFrom(this, Timestamps.Apply(presence.Clone(), timestamp, appliedAt));

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled && ReferenceEquals(DiscordPresence.Owner, this)) Apply();
        }
#endif
    }

    internal static class Timestamps
    {
        public static RichPresence Apply(RichPresence presence, TimestampMode mode, DateTime appliedAt)
        {
            switch (mode)
            {
                case TimestampMode.SinceGameStart:
                    return presence.WithStartTime(DiscordPresence.GameStartTime);
                case TimestampMode.SinceSceneLoad:
                    return presence.WithStartTime(appliedAt);
                default:
                    return presence.ClearTimestamps();
            }
        }
    }
}
