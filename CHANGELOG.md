# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.2] - 2026-06-18

### Fixed

- Tray icon not responding to left/right click after v1.0.1 size optimization
- Restored `NotifyIcon` and `ContextMenuStrip` for reliable tray interaction

### Changed

- Re-enabled Windows Forms in `Pluck.UI` for tray only; monitor layout still uses Win32 APIs

## [1.0.1] - 2026-06-18

### Changed

- Reduced install size (~174 MB → ~63 MB) with single-file compressed publish
- Removed Windows Forms dependency; tray icon uses native Shell notification API
- Monitor layout uses Win32 APIs instead of `System.Windows.Forms.Screen`
- Release builds no longer ship PDB files

## [1.0.0] - 2026-06-18

### Added

- Floating clipboard bubbles on the primary monitor (right edge stack)
- System tray integration with history panel and detached settings window
- Drag-to-paste with ghost bubble and cross-application targeting
- Configurable mouse bindings (left / right / middle + modifiers)
- Bubble resize grip, stack collapse, floating animation, pop effect
- Clipboard history in SQLite with search and filters
- Global hotkey (default `Ctrl+Shift+V`)
- Portable and Windows installer build scripts (`Product/build.bat`)
- XML documentation comments across the solution

### Changed

- N/A (initial public release)

### Fixed

- N/A (initial public release)

[1.0.2]: https://github.com/NguyenHoangPhuc156/Pluck/releases/tag/v1.0.2
[1.0.1]: https://github.com/NguyenHoangPhuc156/Pluck/releases/tag/v1.0.1
[1.0.0]: https://github.com/NguyenHoangPhuc156/Pluck/releases/tag/v1.0.0
