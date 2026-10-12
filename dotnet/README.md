# .NET projects

| Project | Responsibility |
| --- | --- |
| `Sheetloom.Service` | Public HTTP API, private Node process and request limits. |
| `Sheetloom.Renderer` | XML parsing and XLSX creation with ClosedXML. |
| `Sheetloom.Tests` | Service and workbook behavior tests. |

From the repository root:

```sh
npm ci --ignore-scripts
npm start
```

.NET starts Node automatically with the example pack. Run `dotnet test dotnet/Sheetloom.sln` to check the .NET projects.

Follow [the code map](../docs/architecture.md) to read the implementation. See [operations](../docs/operations.md) for deployment and configuration, [the workbook reference](../docs/workbook-format.md) for format behavior, and [testing](../docs/testing.md) for coverage.
