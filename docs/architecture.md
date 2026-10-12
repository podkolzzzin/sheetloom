# Reading the code

Start with the request path, then open the component you want to change:

```text
Public HTTP → PublicApi → TemplateClient → Node server → Vue worker
                  ↓             XML comes back              ↑
           WorkbookRenderer                         compiled template pack
                  ↓
                 XLSX
```

.NET starts the private Node process before accepting requests. In Docker, `tini` forwards stop signals and reaps orphaned processes. Only .NET exposes a public port.

| Start here | Responsibility |
| --- | --- |
| [`Program.cs`](../dotnet/Sheetloom.Service/Program.cs) | Configure the public host and connect its components. |
| [`PublicApi.cs`](../dotnet/Sheetloom.Service/PublicApi.cs) | Validate a request, render it and return the workbook. |
| [`RenderOperation.cs`](../dotnet/Sheetloom.Service/RenderOperation.cs) | Keep the shared deadline and capacity slot across both rendering stages. |
| [`NodeProcess.cs`](../dotnet/Sheetloom.Service/NodeProcess.cs) | Start Node, wait for readiness and stop it with the host. |
| [`TemplateClient.cs`](../dotnet/Sheetloom.Service/TemplateClient.cs) | Discover templates and request XML over loopback HTTP. |
| [`src/main.js`](../src/main.js) | Compile the template pack and start the private server. |
| [`src/pack.js`](../src/pack.js) | Validate the manifest and build the Vue bundle. |
| [`src/render.js`](../src/render.js) | Run each render in a fresh, cancellable worker. |
| [`src/template-runtime.js`](../src/template-runtime.js) | Provide request data to Vue and render the XML. |
| [`src/template-data.js`](../src/template-data.js) | Expose request data to templates through `getTemplateData<T>()`. |
| [`WorkbookRenderer.cs`](../dotnet/Sheetloom.Renderer/WorkbookRenderer.cs) | Parse XML, build a workbook and serialize XLSX. |

## Inside the workbook renderer

`WorkbookXml.cs` describes the incoming XML. `WorkbookParser` converts it into the typed records in `WorkbookModel.cs`. `ExcelTemplateRenderer` creates the workbook and shares formula references across its worksheets.

`ExcelWorksheetRenderer` controls row order and page totals. It delegates cell writing and sizing to `ExcelCellRenderer`, image placement to `ExcelImageRenderer`, and printing and frozen panes to `WorksheetPageLayout`. Styles and formula substitutions have their own existing renderers.

Column widths must settle before wrapped rows and images receive their final sizes. Fit-to-page pagination first renders a temporary worksheet to measure widths. Keep this ordering when changing layout code.

## Boundaries to preserve

- Each request gets a fresh Vue worker and a separate ClosedXML workbook.
- One deadline covers Vue rendering and XLSX creation. Disconnecting cancels the Vue worker.
- ClosedXML is synchronous: after an HTTP timeout, its capacity slot remains occupied until the render finishes.
- JSON and XML have byte limits. Public errors exclude request data and internal exception details.
- Template packs are trusted executable code. File containment checks protect pack boundaries; workers are not a security sandbox.

See [testing](testing.md) for checks grouped by behavior and [operations](operations.md) for configuration and the HTTP contract.
