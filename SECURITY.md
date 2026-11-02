# Security

Saldo has no backend or telemetry. The balance client sends a session only to a fixed OpenAI HTTPS URL, does not follow redirects, and never reads browser profiles. Request bodies and credentials are not logged. Network failure messages are sanitized.

Runtime secrets are stored outside the repository with Windows DPAPI CurrentUser encryption. This protects the file at rest, not from malicious software running as the same user. Settings are separate and contain no token. Disconnect removes Saldo's local copy only.

The dashboard billing endpoint is undocumented and can change without notice. Do not use Saldo's display as a financial ledger, a spending hard limit or a guarantee of service availability. Other grants can have restrictions.

For a vulnerability, use GitHub's private vulnerability reporting if enabled on the published repository. Otherwise request a private reporting channel without disclosing the exploit or credentials in a public issue. Do not attach sessions, account exports or personal balance payloads.
