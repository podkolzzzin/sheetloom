# Testing

Run checks from the repository root. Tests use synthetic templates and temporary workbooks; no external accounts or production data are needed.

```sh
npm test
dotnet test dotnet/Sheetloom.sln --configuration Release
npm run test:docker
.venv-e2e/bin/python tests/e2e/test_workbooks.py
```

See [Contributing](../CONTRIBUTING.md) for dependency and Python environment setup. Docker checks build the production image; the independent E2E suite reads its HTTP responses with openpyxl and inspects ZIP/XML parts.

## Find a test by behavior

| Behavior | Location |
| --- | --- |
| Manifest validation, compilation, locales and cache cleanup | `tests/node/pack.test.js`, `pack-lifecycle.test.js` |
| Template data, typed quickstart and compatibility with older packs | `tests/node/template-data.test.js` |
| Vue, TypeScript and JSX asset paths | `tests/node/assets.test.js` |
| Worker isolation, failures, cancellation and log privacy | `tests/node/worker-lifecycle.test.js`, `pack.test.js` |
| Private XML API, capacity and byte limits | `tests/node/server.test.js`, `server-limits.test.js` |
| Public request validation, deadlines and recovery | `dotnet/Sheetloom.Tests/ServiceTests.cs`, `ServiceLifecycleTests.cs` |
| Loopback HTTP responses and configuration | `dotnet/Sheetloom.Tests/TemplateClientTests.cs`, `ServiceOptionsTests.cs` |
| Typed values, whitespace, apostrophes and culture independence | `dotnet/Sheetloom.Tests/CellValueTests.cs` |
| Formulas and worksheet-local names | `dotnet/Sheetloom.Tests/FormulaTests.cs` |
| Merges, styles and deferred row sizing | `dotnet/Sheetloom.Tests/CellLayoutTests.cs` |
| Page totals, repeated headers and width probing | `dotnet/Sheetloom.Tests/PaginationTests.cs` |
| Print ranges, frozen panes and filters | `dotnet/Sheetloom.Tests/PrintLayoutTests.cs`, `AutoFilterTests.cs` |
| Image bytes and anchors, including sheet boundaries | `dotnet/Sheetloom.Tests/ImageTests.cs` |
| XML validation, precision and request isolation | `dotnet/Sheetloom.Tests/WorkbookSerializationTests.cs`, `WorkbookIsolationTests.cs` |
| Complete container lifecycle and mounted-pack updates | `scripts/docker-smoke.sh` |
| Actual HTTP workbooks read independently | `tests/e2e/test_workbooks.py` |

Run a focused group while editing:

```sh
node --test tests/node/assets.test.js
dotnet test dotnet/Sheetloom.sln --filter FullyQualifiedName~PaginationTests
```

## What assertions mean

Workbook tests check values, types, formula text, dimensions and serialized XML rather than binary snapshots. Formula tests may evaluate a reopened workbook to verify references; the service itself does not promise calculated formula caches. Excel or a compatible application calculates them when opening the file.

Pagination and fitted text tests preserve the renderer's layout rules. They cannot guarantee identical print output with every Excel version and font installation. Image tests inspect both embedded bytes and drawing anchors.

Failure tests verify recovery as well as errors. A timed-out Vue worker must terminate; synchronous ClosedXML work must retain its capacity slot until it finishes. Negative compilation fixtures intentionally print build diagnostics while their assertions pass.

Keep regressions beside the behavior they protect. Preserve the reproducing input and observable assertion when reorganizing tests.
