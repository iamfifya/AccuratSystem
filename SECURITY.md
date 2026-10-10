# Security Policy

## Supported Versions

We actively maintain and patch security vulnerabilities for the following versions:

| Version | Supported          |
| ------- | ------------------ |
| Latest `main` | ✅ Yes |
| Previous minor release | ✅ Yes (security-only patches) |
| Older releases | ❌ No (please upgrade) |

## Reporting a Vulnerability

**⚠️ DO NOT open a public GitHub issue for security vulnerabilities.**

DaEtoZhe processes financial transactions and personal data. Public disclosure of vulnerabilities puts all commercial customers at risk.

### How to report

Send a detailed report to: **dimakuraedov@gmail.com**

Include:
- Description of the vulnerability
- Steps to reproduce (proof of concept if possible)
- Potential impact assessment
- Affected components (WPF client / MAUI mobile / .NET Framework legacy client / WebAPI / Database)

### What to expect

| Stage | Timeline |
|-------|----------|
| Acknowledgment of receipt | Within **48 hours** |
| Initial triage and severity assessment | Within **7 days** |
| Patch development (critical/high) | Within **30 days** |
| Coordinated public disclosure | After patch is deployed to customers |

### Our commitment

- We will not take legal action against researchers who follow responsible disclosure
- We will credit reporters in release notes (unless anonymity is requested)
- We will coordinate disclosure timing with the reporter
- We will publish a security advisory once the patch is available

### What NOT to do

- ❌ Do not exploit the vulnerability beyond what is needed for proof of concept
- ❌ Do not access, modify, or delete data that does not belong to you
- ❌ Do not disclose the vulnerability to third parties before coordinated disclosure
- ❌ Do not perform DoS attacks, spam, or social engineering against our infrastructure

## Scope

**In scope:**
- `DaEtoZhe.WebAPI` (server-side code, authentication, authorization, API endpoints)
- `DaEtoZhe.Contracts` (shared DTOs and models)
- `DaEtoZhe.Desktop` / `DaEtoZhe.Mobile` (client applications)
- SQL migrations and database schema
- Authentication flows (BCrypt, session management)
- SaaS tenant isolation (`X-Company-Id` header enforcement)

**Out of scope:**
- Third-party dependencies (report to their maintainers; we will update)
- Documentation-only issues
- UI/UX preferences (use regular issues)
- Vulnerabilities in deprecated .NET Framework 4.6.2 client (CarWashing) — migration planned

## Security Best Practices for Contributors

When contributing, please ensure:

- Never log sensitive data (passwords, tokens, payment details)
- Never commit secrets, API keys, or credentials (use `appsettings.Development.json` + user secrets)
- Use parameterized queries (EF Core handles this; avoid raw SQL when possible)
- Validate all user input on the server side (never trust client data)
- Enforce `X-Company-Id` tenant isolation in all new endpoints
- Use `DateTime.UtcNow` for timestamps (never `DateTime.Now`)

## Recognition

Security researchers who responsibly disclose vulnerabilities will be acknowledged in our [Security Hall of Fame](SECURITY_HALL_OF_FAME.md) (unless they request anonymity).

---

**Last updated:** 2026-09-24
**Policy version:** 1.0