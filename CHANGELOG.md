# Changelog

All notable changes to this package are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package uses [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-10-30

### Added

- `DiscordPresence` static API: `Initialize`, `Set`, `Update`, `Clear`, `Shutdown`, connection state and `Connected` / `Disconnected` / `Error` events on the main thread.
- `RichPresence` with a fluent builder: details, state, images, timers, party, buttons, activity type and status display type.
- Pure C# IPC transport for Windows (named pipes) and macOS/Linux (Unix sockets, including Flatpak, Snap and Vesktop paths).
- Background worker with latest-wins coalescing, duplicate skipping, rate limiting and automatic reconnect.
- Validation of values Discord would reject, with Console warnings.
- `Discord Presence Manager` and `Discord Scene Presence` components, Inspector status and GameObject menu items.
- Basic Usage sample and EditMode tests.
