using System;
using UnityEngine;

namespace BerTaDev.DiscordRichPresence
{
    /// <summary>A clickable link shown under the presence. Discord allows at most two.</summary>
    [Serializable]
    public struct PresenceButton
    {
        [Tooltip("Button text, 1-32 characters.")]
        [SerializeField] string label;

        [Tooltip("Must start with http:// or https://")]
        [SerializeField] string url;

        public PresenceButton(string label, string url)
        {
            this.label = label;
            this.url = url;
        }

        public string Label { get => label; set => label = value; }
        public string Url { get => url; set => url = value; }
    }
}
