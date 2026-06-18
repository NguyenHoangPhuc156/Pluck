# Contributing to Pluck

Thank you for your interest in contributing to **Pluck**!

## Getting started

### Prerequisites

- Windows 10 1903+ or Windows 11 (64-bit)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

Optional (for installer builds):

- [Inno Setup 6](https://jrsoftware.org/isinfo.php) — or let `Product/build.bat` download it automatically

### Clone and build

```powershell
git clone https://github.com/NguyenHoangPhuc156/Pluck.git
cd Pluck
dotnet build Pluck.slnx
dotnet run --project Pluck.UI
```

### Release builds

```powershell
Product\build.bat
```

Produces:

- `Product/Pluck-1.0.1-Portable/` — portable, self-contained build
- `Product/Pluck-1.0.1-Setup.exe` — Windows installer

## How to contribute

1. **Fork** the repository and create a branch from `main`
2. **Make your changes** with clear, focused commits
3. **Test** on Windows (build + manual smoke test)
4. **Open a pull request** describing what changed and why

### Branch naming

- `feature/short-description`
- `fix/short-description`
- `docs/short-description`

### Code style

- Follow existing C# conventions in the repository
- Use English XML documentation (`/// <summary>`) on public and internal APIs
- Keep changes minimal and focused — avoid unrelated refactors
- Match WPF/XAML patterns already used in `Pluck.UI`

### Commit messages

Use clear, imperative subjects:

```
Add pin column to history list
Fix bubble position on multi-monitor setups
Document build script in README
```

## Pull request checklist

- [ ] Solution builds without errors (`dotnet build Pluck.slnx`)
- [ ] Changes are documented in code where appropriate
- [ ] User-facing behavior changes are noted in `CHANGELOG.md` (Unreleased section)
- [ ] No secrets, personal paths, or unrelated files committed

## Questions

Open a [GitHub Discussion](https://github.com/NguyenHoangPhuc156/Pluck/discussions) or an issue labeled `question`.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
