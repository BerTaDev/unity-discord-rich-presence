using UnityEngine;
using UnityEngine.Events;

namespace BerTaDev.DiscordRichPresence
{
    /// <summary>
    /// Drop this on a GameObject in your first scene, paste your Application ID and you're done.
    /// It connects to Discord, shows the default presence and survives scene loads.
    /// Use <see cref="DiscordScenePresence"/> or <see cref="DiscordPresence"/> to change the presence later.
    /// </summary>
    [AddComponentMenu("Discord/Discord Presence Manager")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class DiscordPresenceManager : MonoBehaviour
    {
        [Tooltip("Application ID from https://discord.com/developers/applications (General Information).")]
        [SerializeField] string applicationId = "";

        [Tooltip("Keep this object and the Discord connection alive across scene loads.")]
        [SerializeField] bool persistAcrossScenes = true;

        [Tooltip("Log connection changes and errors to the Console.")]
        [SerializeField] bool logToConsole = true;

        [Tooltip("Timer shown with the default presence.")]
        [SerializeField] TimestampMode timestamp = TimestampMode.SinceGameStart;

        [Tooltip("Shown on start. A DiscordScenePresence in the scene, or code, can replace it.")]
        [SerializeField] RichPresence defaultPresence = new RichPresence()
            .WithDetails("In the main menu")
            .WithLargeImage("logo");

        [Tooltip("Called with the user's display name when Discord connects.")]
        [SerializeField] UnityEvent<string> onConnected = new UnityEvent<string>();

        [Tooltip("Called when Discord is closed or the connection drops.")]
        [SerializeField] UnityEvent onDisconnected = new UnityEvent();

        System.DateTime appliedAt;

        /// <summary>The active manager, or null.</summary>
        public static DiscordPresenceManager Instance { get; private set; }

        public string ApplicationId => applicationId;

        /// <summary>The inspector presence. Call <see cref="ApplyDefault"/> after changing it.</summary>
        public RichPresence DefaultPresence => defaultPresence;

        public UnityEvent<string> OnConnected => onConnected;
        public UnityEvent OnDisconnected => onDisconnected;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A persistent manager already exists (e.g. the first scene was loaded again).
                Destroy(this);
                return;
            }

            Instance = this;
            if (persistAcrossScenes)
            {
                if (transform.parent == null) DontDestroyOnLoad(gameObject);
                else Debug.LogWarning("[Discord] Persist Across Scenes only works on root GameObjects. Move the Discord Presence Manager to the root of the scene.", this);
            }

            DiscordPresence.Logging = logToConsole;
            DiscordPresence.Connected += HandleConnected;
            DiscordPresence.Disconnected += HandleDisconnected;
            DiscordPresence.Initialize(applicationId);

            // A DiscordScenePresence may have set something already; don't overwrite it.
            if (DiscordPresence.Current == null) ApplyDefault();
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            DiscordPresence.Connected -= HandleConnected;
            DiscordPresence.Disconnected -= HandleDisconnected;
            DiscordPresence.Shutdown();
        }

        /// <summary>Shows <see cref="DefaultPresence"/> again, e.g. when returning to the main menu.</summary>
        public void ApplyDefault()
        {
            appliedAt = System.DateTime.UtcNow;
            DiscordPresence.SetFrom(this, Timestamps.Apply(defaultPresence.Clone(), timestamp, appliedAt));
        }

        void HandleConnected(DiscordUser user) => onConnected.Invoke(user.DisplayName);

        void HandleDisconnected() => onDisconnected.Invoke();

#if UNITY_EDITOR
        // Live-edit: tweak the presence in the Inspector during Play Mode and see it on Discord.
        void OnValidate()
        {
            if (!Application.isPlaying || Instance != this) return;
            DiscordPresence.Logging = logToConsole;
            if (ReferenceEquals(DiscordPresence.Owner, this))
                DiscordPresence.SetFrom(this, Timestamps.Apply(defaultPresence.Clone(), timestamp, appliedAt));
        }
#endif
    }
}
