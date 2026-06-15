# Pluck

**Your clipboard, alive on screen.** — Floating clipboard manager for Windows 10/11.

![Pluck](Icon.png)

## Features

- **Floating bubbles** on the right edge of the screen for every copy
- **System tray** app with history panel and settings
- **Drag a bubble** onto any window to paste (with target highlight)
- **Ctrl + drag** to reposition a bubble
- **Clipboard history** stored in SQLite (`%LocalAppData%\Pluck\`)
- **Global hotkey** (default `Ctrl+Shift+V`) to open the main dialog
- **Launch at Windows startup** (optional)
- Pin, delete, copy-again, auto-dismiss, floating animation, pop effect on paste

## Requirements

- Windows 10 1903+ / Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or .NET 8+ with adjusted `TargetFramework`)

## Build & run

```powershell
cd Pluck
dotnet build Pluck.slnx
dotnet run --project Pluck.UI
```

## Publish (portable single-file)

```powershell
dotnet publish Pluck.UI -c Release -p:PublishProfile=Portable
```

Output: `Pluck.UI\bin\Release\net10.0-windows\win-x64\publish\Pluck.exe`

## Architecture

| Project | Role |
|---------|------|
| `Pluck.Data` | Models, SQLite history, JSON settings |
| `Pluck.Core` | Win32 clipboard hook, hotkey, paste, thumbnails |
| `Pluck.UI` | WPF tray app, overlay bubbles, main dialog |

All bubbles render in a **single transparent overlay window** (one HWND) for performance.

## Settings

Stored at `%LocalAppData%\Pluck\settings.json`

## License

MIT — see [Pluck Product Specification](Pluck_Product_Specification_v1.0.pdf).
