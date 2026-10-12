# Contributing

Use Node.js 24, the .NET 10 SDK and DejaVu fonts. Docker and Python 3 are needed for independent XLSX checks. Run commands from the repository root:

```sh
npm ci --ignore-scripts
npm test
dotnet restore dotnet/Sheetloom.sln --locked-mode
python3 scripts/dependency-notices.py --check
dotnet test dotnet/Sheetloom.sln --no-restore --configuration Release
npm run test:docker
python3 -m venv .venv-e2e
.venv-e2e/bin/python -m pip install -r tests/e2e/requirements.txt
.venv-e2e/bin/python tests/e2e/test_workbooks.py
```

Node tests and their helpers live in `tests/node/`; container E2E tests and synthetic packs live in `tests/e2e/`. Renderer tests stay with the .NET solution in `dotnet/Sheetloom.Tests/`. CI runs the Node suite through `npm test`.

Keep changes focused. For a bug, add a small synthetic template and an assertion that fails before the fix. Workbook assertions should check values, types, formulas or layout rather than ZIP-byte snapshots. Never add business reports, personal records, credentials or production configuration.

Describe the problem, resulting behavior and checks in your PR. Update [the format reference](docs/workbook-format.md) for dialect changes and [operations](docs/operations.md) for HTTP or configuration changes. CI covers Node, .NET, Docker, independent openpyxl checks and Git history secret scanning.

Contributions are under the project's [MIT license](LICENSE). Preserve third-party notices and update [the dependency inventory](THIRD_PARTY_NOTICES.md) when changing lockfiles. Report vulnerabilities privately using [SECURITY.md](SECURITY.md).
