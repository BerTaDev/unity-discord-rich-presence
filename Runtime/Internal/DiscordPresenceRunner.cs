using UnityEngine;

namespace BerTaDev.DiscordRichPresence.Internal
{
    /// <summary>Hidden object that delivers Discord events on the main thread.</summary>
    [AddComponentMenu("")]
    internal sealed class DiscordPresenceRunner : MonoBehaviour
    {
        void Update() => DiscordPresence.DispatchMainThread();

        void OnDestroy() => DiscordPresence.OnRunnerDestroyed(this);
    }
}
