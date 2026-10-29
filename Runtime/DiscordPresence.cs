using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using BerTaDev.DiscordRichPresence.Internal;
using UnityEngine;

namespace BerTaDev.DiscordRichPresence
{
    /// <summary>
    /// Entry point for Discord Rich Presence. Call <see cref="Initialize"/> once, then
    /// <see cref="Set"/> or <see cref="Update"/> whenever something changes. Updating every
    /// frame is fine: only the newest presence is sent and Discord's rate limit is handled for you.
    /// All members must be used from the main thread; events are raised on the main thread.
    /// </summary>
    public static class DiscordPresence
    {
        static readonly ConcurrentQueue<Action> mainThreadQueue = new ConcurrentQueue<Action>();
        static readonly List<string> warnings = new List<string>();

        static DiscordRpcClient client;
        static DiscordPresenceRunner runner;
        static RichPresence current;
        static bool hooksInstalled;

        /// <summary>Raised when the connection to Discord is ready, with the logged-in user.</summary>
        public static event Action<DiscordUser> Connected;

        /// <summary>Raised when Discord is closed or the connection drops. Reconnects automatically.</summary>
        public static event Action Disconnected;

        /// <summary>Raised when Discord rejects something, e.g. an invalid application id.</summary>
        public static event Action<DiscordError> Error;

        /// <summary>False on platforms without a desktop Discord client (mobile, WebGL, consoles, servers).</summary>
        public static bool IsSupported =>
#if UNITY_SERVER
            false;
#elif UNITY_EDITOR_WIN || UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            true;
#else
            false;
#endif

        /// <summary>True between <see cref="Initialize"/> and <see cref="Shutdown"/>.</summary>
        public static bool IsInitialized => client != null;

        /// <summary>True while connected to a running Discord client.</summary>
        public static bool IsConnected { get; private set; }

        /// <summary>The Discord account we're connected to, or null.</summary>
        public static DiscordUser User { get; private set; }

        /// <summary>The application id passed to <see cref="Initialize"/>.</summary>
        public static string ApplicationId { get; private set; }

        /// <summary>A copy of the presence that is currently set, or null when cleared.</summary>
        public static RichPresence Current => current?.Clone();

        /// <summary>UTC time the game started; used by <see cref="TimestampMode.SinceGameStart"/>.</summary>
        public static DateTime GameStartTime { get; private set; } = DateTime.UtcNow;

        /// <summary>Log connection changes, errors and presence warnings to the Console.</summary>
        public static bool Logging { get; set; } = true;

        /// <summary>The component (or null for code) that set the current presence.</summary>
        internal static object Owner { get; private set; }

        /// <summary>
        /// Starts connecting to Discord in the background. Safe to call when Discord isn't running:
        /// the connection is retried until it opens. Calling again with the same id does nothing.
        /// </summary>
        /// <param name="applicationId">The Application ID from https://discord.com/developers/applications</param>
        /// <returns>False if the id is invalid or the platform is not supported.</returns>
        public static bool Initialize(string applicationId)
        {
            if (!IsSupported) return false;

            applicationId = applicationId?.Trim();
            if (!IsValidApplicationId(applicationId))
            {
                Debug.LogError("[Discord] '" + applicationId + "' is not a valid Application ID. Copy it from https://discord.com/developers/applications (General Information > Application ID).");
                return false;
            }

            if (client != null)
            {
                // Same id and still running: nothing to do. A client that gave up (e.g. after
                // "Invalid Client ID") is restarted.
                if (applicationId == ApplicationId && client.IsRunning) return true;
                StopClient();
            }

            InstallHooks();
            EnsureRunner();

            ApplicationId = applicationId;
            var newClient = new DiscordRpcClient(applicationId, GetProcessId(), CreateTransport);
            newClient.Ready += user => Post(newClient, () => OnReady(user));
            newClient.Disconnected += () => Post(newClient, OnDisconnected);
            newClient.Error += error => Post(newClient, () => OnError(error));
            client = newClient;

            if (current != null) client.SetActivity(Serialize(current));
            client.Start();
            return true;
        }

        /// <summary>Replaces the whole presence. A copy is taken, so you can keep editing your instance.</summary>
        public static void Set(RichPresence presence) => SetFrom(null, presence);

        /// <summary>
        /// Changes part of the current presence, e.g. <c>DiscordPresence.Update(p => p.State = "Wave 3");</c>
        /// Starts from an empty presence if none is set.
        /// </summary>
        public static void Update(Action<RichPresence> change)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));
            RichPresence next = current?.Clone() ?? new RichPresence();
            change(next);
            SetFrom(Owner, next);
        }

        /// <summary>Removes the presence from the profile but stays connected.</summary>
        public static void Clear() => SetFrom(null, null);

        /// <summary>Clears the presence and closes the connection. Called automatically when the game quits.</summary>
        public static void Shutdown()
        {
            StopClient();
            current = null;
            Owner = null;
            ApplicationId = null;
        }

        /// <summary>Discord application ids are numeric snowflakes.</summary>
        public static bool IsValidApplicationId(string applicationId)
        {
            if (string.IsNullOrEmpty(applicationId) || applicationId.Length < 15 || applicationId.Length > 20) return false;
            foreach (char c in applicationId)
                if (c < '0' || c > '9') return false;
            return true;
        }

        internal static void SetFrom(object owner, RichPresence presence)
        {
            Owner = owner;
            current = presence?.Clone();
            client?.SetActivity(current != null ? Serialize(current) : null);
        }

        /// <summary>Runs queued callbacks. Called every frame by the hidden runner object.</summary>
        internal static void DispatchMainThread()
        {
            while (mainThreadQueue.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        internal static void OnRunnerDestroyed(DiscordPresenceRunner destroyed)
        {
            if (runner == destroyed) runner = null;
        }

        static string Serialize(RichPresence presence)
        {
            warnings.Clear();
            string json = ActivitySerializer.Serialize(presence, warnings);
            if (Logging)
                foreach (string warning in warnings)
                    Debug.LogWarning("[Discord] " + warning);
            return json;
        }

        static void OnReady(DiscordUser user)
        {
            IsConnected = true;
            User = user;
            if (Logging) Debug.Log("[Discord] Connected as " + user.DisplayName + ".");
            Connected?.Invoke(user);
        }

        static void OnDisconnected()
        {
            IsConnected = false;
            User = null;
            if (Logging) Debug.Log("[Discord] Disconnected. Reconnecting when Discord is available.");
            Disconnected?.Invoke();
        }

        static void OnError(DiscordError error)
        {
            if (Logging) Debug.LogWarning("[Discord] " + error);
            Error?.Invoke(error);
        }

        // Drops callbacks from clients that were shut down in the meantime.
        static void Post(DiscordRpcClient source, Action action) =>
            mainThreadQueue.Enqueue(() =>
            {
                if (client == source) action();
            });

        static void StopClient()
        {
            DiscordRpcClient old = client;
            client = null;
            old?.Dispose();
            if (IsConnected && Logging) Debug.Log("[Discord] Disconnected.");
            IsConnected = false;
            User = null;
        }

        static void EnsureRunner()
        {
            if (runner != null) return;
            var go = new GameObject("[Discord Rich Presence]");
            if (Application.isPlaying)
            {
                go.hideFlags = HideFlags.HideInHierarchy;
                UnityEngine.Object.DontDestroyOnLoad(go);
            }
            else
            {
                go.hideFlags = HideFlags.HideAndDontSave; // never end up saved in an edited scene
            }
            runner = go.AddComponent<DiscordPresenceRunner>();
        }

        static void InstallHooks()
        {
            if (hooksInstalled) return;
            hooksInstalled = true;
            Application.quitting += Shutdown;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
#endif
        }

        static IIpcTransport CreateTransport()
        {
            // Editor defines come first: a Mac editor targeting Windows defines UNITY_STANDALONE_WIN too.
#if UNITY_EDITOR_WIN
            return new WindowsPipeTransport();
#elif UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            return new UnixSocketTransport();
#elif UNITY_STANDALONE_WIN
            return new WindowsPipeTransport();
#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            return new UnixSocketTransport();
#else
            throw new PlatformNotSupportedException("Discord Rich Presence needs a desktop platform.");
#endif
        }

        static int GetProcessId()
        {
            try
            {
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                    return process.Id;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // Keeps "Enter Play Mode Options" (no domain reload) from carrying state between sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Shutdown();
            while (mainThreadQueue.TryDequeue(out _)) { }
            Connected = null;
            Disconnected = null;
            Error = null;
            runner = null;
            Logging = true;
            GameStartTime = DateTime.UtcNow;
        }
    }
}
