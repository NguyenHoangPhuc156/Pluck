# Pluck

**Your clipboard, alive on screen.** — A floating clipboard manager for Windows 10/11.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

![Pluck](Icon.png)

Pluck shows lightweight **floating bubbles** whenever you copy something, so recent clips stay visible at the edge of your screen. Drag a bubble onto any window to paste, browse full history from the tray, and tune behavior in settings.

**Author:** [Nguyen Hoang Phuc](AUTHORS.md) · **License:** [MIT](LICENSE) · **Version:** 1.0.1 · **Repository:** [github.com/NguyenHoangPhuc156/Pluck](https://github.com/NguyenHoangPhuc156/Pluck)

## Demo

https://github.com/user-attachments/assets/ddf69d1d-0a45-4cf9-aba1-a8eb63e22546

## Features

- **Floating bubbles** stacked on the right edge for every copy
- **System tray** app with history panel and separate settings window
- **Drag-to-paste** onto any window (with target highlight)
- **Configurable mouse bindings** — left / right / middle click + modifiers
- **Resize bubbles** from the bottom-right grip; **Ctrl + drag** to reposition
- **Clipboard history** in SQLite (`%LocalAppData%\Pluck\`)
- **Global hotkey** (default `Ctrl+Shift+V`) to open the main dialog
- **Launch at Windows startup** (optional)
- Pin, delete, copy again, auto-dismiss, floating animation, pop effect on paste

## Requirements

- Windows 10 1903+ or Windows 11 (64-bit)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) for building from source

Pre-built releases bundle the runtime (portable / installer).

## Download

| Package | Description |
|---------|-------------|
| **Portable** | Self-contained folder — run `Pluck.exe` directly |
| **Setup** | Inno Setup installer with Start Menu shortcut |

Build both locally:

```powershell
Product\build.bat
```

Output:

- `Product/Pluck-1.0.1-Portable/`
- `Product/Pluck-1.0.1-Setup.exe`

## Build & run (development)

```powershell
git clone https://github.com/NguyenHoangPhuc156/Pluck.git
cd Pluck
dotnet build Pluck.slnx
dotnet run --project Pluck.UI
```

### Publish (portable, single command)

```powershell
dotnet publish Pluck.UI -c Release -p:PublishProfile=Portable
```

Output: `Pluck.UI\bin\Release\net10.0-windows\win-x64\publish\Pluck.exe`

## Usage

1. Run Pluck — it starts in the **system tray**.
2. Copy text or an image; a **bubble** appears on the right.
3. **Drag** a bubble onto another app to paste.
4. **Double-click** the tray icon (or press `Ctrl+Shift+V`) to open **history**.
5. Open **Settings** from the tray menu to change hotkeys, mouse actions, and startup behavior.

### Mouse actions (defaults)

| Action | Default |
|--------|---------|
| Left click | Paste at cursor |
| Right click | Copy again |
| Middle click | Delete bubble (release inside bubble) |
| Ctrl + drag | Move bubble |

All bindings are configurable in Settings.

## Architecture

| Project | Role |
|---------|------|
| `Pluck.Data` | Models, SQLite history, JSON settings |
| `Pluck.Core` | Win32 clipboard hook, hotkey, paste, thumbnails |
| `Pluck.UI` | WPF tray app, overlay bubbles, dialogs |

All bubbles render in a **single transparent overlay window** (one HWND) for performance.

```
Clipboard change → ClipboardMonitor → ClipboardCaptureService
                                              ↓
                                    BubbleManager → BubbleOverlayWindow
                                              ↓
                              Drag session → PasteService → target window
```

## Data & privacy

Pluck runs **entirely on your machine**. History and settings are stored locally:

| Path | Contents |
|------|----------|
| `%LocalAppData%\Pluck\history.db` | Clipboard history (SQLite) |
| `%LocalAppData%\Pluck\settings.json` | User preferences |

No network calls are made by the application.

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) and our [Code of Conduct](CODE_OF_CONDUCT.md) before opening a pull request.

## Security

To report a vulnerability, see [SECURITY.md](SECURITY.md).

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for release notes.

## License

Copyright © 2026 [Nguyen Hoang Phuc](AUTHORS.md)

Released under the [MIT License](LICENSE).

## Acknowledgments

Product specification and design notes: [Pluck Product Specification](Pluck_Product_Specification_v1.0.pdf) (if included in your distribution).
