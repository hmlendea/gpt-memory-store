# Documentation Index

This directory contains comprehensive technical documentation for GPT Memory Store, complementing the root-level documentation.

## Document Map

| Document | Purpose | Audience |
|----------|---------|----------|
| [API Reference](api-reference.md) | Complete REST API specification with request/response formats, authentication, error codes | API consumers, frontend developers, integrators |
| [Configuration Reference](configuration.md) | All settings, environment variables, binding behavior, secret management | DevOps, platform engineers, operators |
| [Deployment Guide](deployment.md) | Deployment options (bare metal, Docker, Kubernetes, systemd), TLS, storage, scaling | DevOps, platform engineers, operators |
| [Development Guide](development.md) | Setup, workflows, code conventions, architecture boundaries, debugging | Contributors, maintainers |
| [Data Model Reference](data-model.md) | Domain models, persistence models, mapping, JSON formats, ordering rules, concurrency | Developers, architects, data engineers |
| [Testing Reference](testing.md) | Test structure, patterns, coverage, running tests, CI integration | Contributors, maintainers, QA |
| [Operations Guide](operations.md) | Service management, monitoring, backup/restore, log management, incident response | Operators, SREs, on-call engineers |

## Root-Level Documentation

| Document | Location | Purpose |
|----------|----------|---------|
| [README.md](../README.md) | Repository root | Project overview, quick start, usage examples |
| [ARCHITECTURE.md](../ARCHITECTURE.md) | Repository root | System architecture, components, flows, decisions |
| [PRIVACY.md](../PRIVACY.md) | Repository root | Data handling, privacy, operator responsibilities |
| [SECURITY.md](../SECURITY.md) | Repository root | Vulnerability reporting, supported versions, disclosure |
| [LICENSE](../LICENSE) | Repository root | GNU GPL v3.0 license terms |

## Quick Navigation by Role

### API Consumer / Integrator
1. [API Reference](api-reference.md) - Start here
2. [README.md](../README.md) - Usage examples
3. [Configuration Reference](configuration.md) - Client-relevant settings

### Developer / Contributor
1. [Development Guide](development.md) - Setup and workflows
2. [Architecture](../ARCHITECTURE.md) - System design
3. [Data Model Reference](data-model.md) - Internal models
4. [Testing Reference](testing.md) - Test patterns
5. [API Reference](api-reference.md) - Contract details

### DevOps / Platform Engineer
1. [Deployment Guide](deployment.md) - Deployment options
2. [Configuration Reference](configuration.md) - All settings
3. [Operations Guide](operations.md) - Day-to-day operations
4. [Security](../SECURITY.md) - Security policy

### Operator / SRE / On-Call
1. [Operations Guide](operations.md) - Runbooks, monitoring, incidents
2. [Deployment Guide](deployment.md) - Upgrade procedures
3. [Configuration Reference](configuration.md) - Runtime config changes
4. [Privacy](../PRIVACY.md) - Data handling responsibilities

### Architect / Technical Lead
1. [Architecture](../ARCHITECTURE.md) - System architecture
2. [Data Model Reference](data-model.md) - Data design
3. [Development Guide](development.md) - Extension points
4. [Testing Reference](testing.md) - Verification strategy

## Documentation Standards

- All documents use Markdown with consistent heading structure
- Code examples are tested against the codebase
- Configuration examples use actual setting names from source
- Architecture diagrams use Mermaid (rendered in GitHub)
- Cross-references use relative links
- No duplication of root-level documentation content

## Maintenance

When modifying the codebase, update relevant documentation:

| Code Change | Update |
|-------------|--------|
| New API endpoint | [API Reference](api-reference.md), [Testing Reference](testing.md) |
| New configuration setting | [Configuration Reference](configuration.md), [Deployment Guide](deployment.md) |
| Data model change | [Data Model Reference](data-model.md), [API Reference](api-reference.md) |
| New test pattern | [Testing Reference](testing.md) |
| Operational procedure | [Operations Guide](operations.md) |
| Architecture change | [Architecture](../ARCHITECTURE.md), [Development Guide](development.md) |

## Version Alignment

Documentation reflects the **current main branch** state. Version-specific documentation is not maintained separately; refer to Git history for historical versions.

---

*Generated as part of repository comprehension effort. For questions or improvements, see [Contributing](../README.md#contributing).*