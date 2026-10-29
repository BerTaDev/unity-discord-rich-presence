<p align="center">
  <img src="Documentation~/images/banner.svg" alt="Discord Rich Presence for Unity: an example Discord activity card and the line of code that updates it" width="100%">
</p>

<p align="center">
  <a href="https://unity.com"><img alt="Unity 2021.3+" src="https://img.shields.io/badge/Unity-2021.3%2B-000000?logo=unity&logoColor=white"></a>
  <img alt="Windows, macOS, Linux" src="https://img.shields.io/badge/platforms-Windows%20%7C%20macOS%20%7C%20Linux-5865F2">
  <img alt="Zero dependencies" src="https://img.shields.io/badge/dependencies-none-57F287">
  <a href="LICENSE.md"><img alt="MIT License" src="https://img.shields.io/badge/license-MIT-FF8A3D"></a>
  <a href="https://github.com/BerTaDev/unity-discord-rich-presence/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/BerTaDev/unity-discord-rich-presence?color=A78BFA"></a>
</p>

<p align="center"><b>English</b> · <a href="README.tr.md">Türkçe</a></p>

# Discord Rich Presence for Unity

Show what players are doing in your game on their Discord profile. Zero dependencies, no native DLLs, set up in under a minute.

```csharp
DiscordPresence.Initialize("YOUR_APPLICATION_ID");
DiscordPresence.Set(new RichPresence().WithDetails("Exploring the dungeon").WithState("Floor 3").WithElapsedTime());
```

<p align="center">
  <img src="Documentation~/images/anatomy.svg" alt="A Discord profile card with eight numbered parts, each mapped to the RichPresence method that sets it" width="100%">
</p>

## Features

- **Pure C#.** Talks to the Discord desktop app directly over its local IPC pipe/socket. No Discord SDK, no native plugins, nothing extra to ship with your build.
- **Editor + Windows, macOS and Linux builds**, Mono and IL2CPP. Flatpak, Snap and Vesktop installs are found too.
- **No-code option.** Drop a `Discord Presence Manager` into your first scene, add a `Discord Scene Presence` to each scene, done.
- **Fast and safe to call often.** Everything runs on a background thread and never blocks the main thread. Call `Update` every frame if you like: only the newest presence is sent, duplicates are skipped and Discord's rate limit (5 updates per 20 s) is handled for you.
- **Auto reconnect.** Start Discord after the game, or restart it: the connection comes back and your presence is restored.
- **Validation.** Values Discord would reject (and silently drop your whole presence for) are fixed or left out, with a clear Console warning.
- **Live edit.** Change the presence in the Inspector during Play Mode and see it update on Discord.
- Details, state, large/small images (asset keys or image URLs), elapsed timer or countdown, party size, up to 2 buttons, activity type (Playing / Listening / Watching / Competing) and status display type.

## Installation

**Window → Package Manager → + → Add package from git URL…**

```
https://github.com/BerTaDev/unity-discord-rich-presence.git
```

To lock a version, add a tag: `https://github.com/BerTaDev/unity-discord-rich-presence.git#v1.0.0`

Or add it to `Packages/manifest.json`:

```json
"com.bertadev.discord-rich-presence": "https://github.com/BerTaDev/unity-discord-rich-presence.git#v1.0.0"
```

## 1. Create a Discord application

1. Open the [Discord Developer Portal](https://discord.com/developers/applications) and click **New Application**. Its name is what Discord shows as "Playing **name**".
2. Copy the **Application ID** from **General Information**.
3. Optional: under **Rich Presence → Art Assets**, upload images (512×512 or larger). The name you give an image is its *key* in Unity. New assets can take a few minutes to appear. You can also use any `https://` image URL as a key.

## 2a. Without code

1. **GameObject → Discord → Discord Presence Manager**, paste your Application ID.
2. Fill in the default presence (details, state, image keys…) and press **Play**.
3. Want different text per scene? Add **GameObject → Discord → Discord Scene Presence** to that scene.

<p align="center">
  <img src="Documentation~/images/inspector.svg" alt="The Discord Presence Manager in the Inspector, and three scenes whose Discord Scene Presence components each show different text on Discord" width="100%">
</p>

The manager keeps the connection alive across scene loads, and its Inspector shows the connection status while playing.

## 2b. From code

```csharp
using BerTaDev.DiscordRichPresence;
using UnityEngine;

public class GamePresence : MonoBehaviour
{
    void Start()
    {
        DiscordPresence.Initialize("YOUR_APPLICATION_ID");

        DiscordPresence.Set(new RichPresence()
            .WithDetails("Exploring the dungeon")
            .WithState("Floor 3")
            .WithLargeImage("logo", "My Game")
            .WithSmallImage("warrior", "Level 12 Warrior")
            .WithParty(1, 4)
            .WithElapsedTime()
            .WithButton("Play for free", "https://example.com"));
    }

    public void OnFloorChanged(int floor)
    {
        // Change only what changed; everything else stays.
        DiscordPresence.Update(p => p.State = "Floor " + floor);
    }
}
```

The connection closes and the presence is cleared automatically when the game quits.

## API

### `DiscordPresence` (static)

| Member | Description |
| --- | --- |
| `Initialize(applicationId)` | Starts connecting in the background. Safe when Discord isn't running yet. Returns `false` for an invalid id or unsupported platform. |
| `Set(presence)` | Replaces the whole presence (a copy is taken). Can be called before `Initialize`. |
| `Update(p => …)` | Changes part of the current presence. |
| `Clear()` | Removes the presence but stays connected. |
| `Shutdown()` | Clears the presence and disconnects. Called automatically on quit. |
| `IsConnected`, `User` | Connection state and the logged-in `DiscordUser` (name, id, `GetAvatarUrl()`). |
| `IsSupported` | `false` on platforms without desktop Discord (mobile, WebGL, consoles, servers). There every call is a safe no-op. |
| `Connected`, `Disconnected`, `Error` | Events, always raised on the main thread. |
| `Logging` | Log connection changes and warnings to the Console (default `true`). |

### `RichPresence` builder

| Method | Discord shows |
| --- | --- |
| `WithDetails(text)` / `WithState(text)` | First and second line (2–128 characters). |
| `WithLargeImage(key, tooltip)` / `WithSmallImage(key, tooltip)` | Big picture and the small round badge. |
| `WithElapsedTime()` / `WithStartTime(time)` | "12:34 elapsed" |
| `WithCountdown(timeSpan)` / `WithEndTime(time)` | "04:59 left" |
| `WithParty(size, max, id)` | "(2 of 4)" |
| `WithButton(label, url)` | Up to two link buttons. |
| `WithType(ActivityType.Listening)` | "Listening to …" instead of "Playing …". |
| `WithStatusDisplay(StatusDisplayType.State)` | Show your state/details text in the member list instead of the app name. |

All values are also plain properties (`presence.State = "…"`), and `RichPresence` is serializable, so you can expose one in your own components.

### Events

```csharp
DiscordPresence.Connected += user => Debug.Log("Hi " + user.DisplayName);
DiscordPresence.Error += error => Debug.LogWarning(error.Message);
```

`Discord Presence Manager` also exposes `On Connected (string)` and `On Disconnected` UnityEvents in the Inspector.

## How it works

<p align="center">
  <img src="Documentation~/images/how-it-works.svg" alt="Calls on the main thread are serialized and handed to a worker thread, which keeps the newest one, rate limits and writes it to the Discord IPC pipe; events come back on the main thread" width="100%">
</p>

`Initialize` starts one background thread that looks for the Discord IPC endpoint (`\\.\pipe\discord-ipc-0..9` on Windows, `discord-ipc-0..9` in the temp/runtime directory on macOS and Linux), performs the handshake and then waits. When you call `Set` or `Update`, the presence is serialized on the main thread and handed over; the worker sends only the newest one, skips it if nothing changed and respects the rate limit. If Discord isn't running it retries with a backoff (1 s up to 15 s), and after a reconnect the current presence is sent again.

<p align="center">
  <img src="Documentation~/images/timeline.svg" alt="Over 20 seconds, 1,200 Update calls result in only 4 messages to Discord, one per real change" width="100%">
</p>

So you never have to track "did this change?" yourself: call `Update` whenever it's convenient, even every frame.

## Troubleshooting

- **Nothing shows up.** The Discord *desktop* app must be running (the browser version has no RPC). In Discord: **User Settings → Activity Privacy → Share your detected activities with others** must be on.
- **"Invalid Client ID"** in the Console: the Application ID is wrong. The package stops retrying until you initialize again.
- **Images are missing.** The key must exactly match the asset name in the Developer Portal. Freshly uploaded assets can take ~10 minutes. Image URLs work immediately.
- **Buttons don't appear.** Discord does not show your own buttons to you. Check from another account.
- **Windows: never connects.** Unity and Discord must run with the same privileges. If one is "Run as administrator", they can't see each other.
- **Updates are delayed.** Discord allows 5 updates per 20 seconds; the latest one is sent as soon as allowed.

## Samples

**Package Manager → Discord Rich Presence → Samples → Basic Usage → Import** adds a script with on-screen buttons that update the presence at runtime.

## License

[MIT](LICENSE.md) © BerTaDev. Not affiliated with Discord Inc.
