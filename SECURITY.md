# Security Policy

This policy defines responsible vulnerability reporting for GPT Memory Store and applies to the latest supported release distributed through GitHub Releases.

## 📑 Table of Contents

- [Table of Contents](#table-of-contents)
- [Supported Versions](#supported-versions)
- [Reporting a Vulnerability](#reporting-a-vulnerability)
- [Scope](#scope)
- [Disclosure Policy](#disclosure-policy)

## 🛡️ Supported Versions

Use this table to indicate which project versions currently receive security maintenance.

| Version | Distribution Channel | Supported |
|---------|--------------------|-----------|
| Latest version | GitHub Releases | ✅ |
| Preceding versions | Any distribution channel | ❌ |

## 🚨 Reporting a Vulnerability

Please do not disclose suspected vulnerabilities publicly before maintainers have had an opportunity to validate and remediate them.

To report a vulnerability:
- [GitHub Security Advisories](https://github.com/hmlendea/gpt-memory-store/security/advisories)
- Contact the maintainers directly

## 📌 Scope

The subsequent report categories are in scope for this repository:
- Authentication or authorisation defects affecting the memory API
- Vulnerabilities in HTTP request handling, JSON persistence, dependencies, or configuration that could expose or modify stored memories

The subsequent categories are out of scope unless explicitly stated to the contrary:
- Vulnerabilities in unsupported preceding releases or unofficial distributions
- Deployment security issues external to this repository, including host, network, and secret-provider configuration

## 📢 Disclosure Policy

This project follows coordinated disclosure:
1. Vulnerabilities are investigated privately.
2. A remediation plan is prepared and validated.
3. Public disclosure is published after a fix, mitigation, or agreed risk decision is available.
4. Credit is attributed in accordance with reporter preference and project policy.