# Security

Report vulnerabilities privately to the repository owner at **podkolzzzin1996@gmail.com**, with subject `excel-template-engine security`. Include the affected revision, a minimal synthetic reproduction and expected impact. Do not open a public issue containing exploits, report data or credentials. No fixed response time is promised.

Security fixes target the latest default-branch revision; older revisions have no separate maintenance commitment.

## Trust boundary

- Vue packs and setup modules execute trusted application code, including Node built-ins. Path checks and fresh workers are not a sandbox for hostile templates.
- The public API has no built-in authentication. Keep it on a private network or behind your application's authenticated gateway. Keep the private .NET renderer on loopback.
- Use read-only pack mounts and a non-root container. Limit CPU, memory, request size and concurrency. A timed-out ClosedXML render may continue until its bounded slot finishes.
- Request data and rendered XML are not intentionally logged by the engine. Audit your gateway and custom template integrations separately.

Before publishing source or release artifacts, scan the complete Git history and inspect included binary files. Rotate any exposed credentials before cleaning history. See [release preparation](docs/releasing.md) for the exact checks and current limitations.
