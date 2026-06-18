# Security Policy

## Supported versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |

## Reporting a vulnerability

If you discover a security issue in Pluck, please **do not** open a public GitHub issue.

Instead, report it privately by emailing the maintainer:

- **Email:** [nhphuc1562002@gmail.com](mailto:nhphuc1562002@gmail.com)

Include:

- A clear description of the vulnerability
- Steps to reproduce
- Potential impact (e.g. local privilege escalation, data exposure)
- Your environment (Windows version, Pluck version)

You should receive a response within **7 days**. If the issue is confirmed, we will work on a fix and coordinate disclosure.

## Scope

Pluck runs locally on Windows and stores clipboard history under `%LocalAppData%\Pluck\`.  
Reports about clipboard data on the same machine, overlay click-through, or paste injection into other applications are in scope.
