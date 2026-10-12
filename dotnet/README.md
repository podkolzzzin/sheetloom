# Renderer

`dotnet test dotnet/Sheetloom.sln` runs renderer, public API and private Node transport tests. From the repository root, `npm ci --ignore-scripts` followed by `npm start` starts the public .NET service with the example pack. It launches Node automatically.

## Process ownership and request flow

ASP.NET is the container's main application. It starts Node, waits for template discovery, then exposes the public API on port 8080. Node binds only to `127.0.0.1:8081` and compiles the mounted pack. A Node crash stops the host; normal host shutdown terminates Node.

- Public .NET: `GET /health`, `GET /v1/templates`, `POST /v1/render/{id}` with `{ "data": ..., "options": { "precisionAsDisplayed": false } }`.
- Private Node: `GET /health`, `GET /templates`, `POST /render/{id}` with `{ "data": ... }`; returns UTF-8 XML. This internal protocol is not a public API.
- .NET calls `IWorkbookRenderer.Render` directly with the returned XML. There is no separate renderer HTTP server.
- `PORT` defaults to 8080; `NODE_PORT` defaults to 8081 and must differ. `RENDER_CONCURRENCY` defaults to 2 and bounds the complete pipeline.
- `RENDERER_FALLBACK_FONT` defaults to `DejaVu Sans`; install production fonts if exact font metrics are required.

The renderer uses ClosedXML 0.105.1. XML parsing forbids DTDs/external entities and limits document size. Each render allocates its own workbook, formula tracking and models; failures dispose allocated workbooks. The serialization wrapper changes `calcPr.fullPrecision` only when requested.

Public JSON defaults to a 10 MiB limit; generated XML is limited to 8 MiB in UTF-8. Errors are structured and never include template data or upstream response bodies. One deadline covers template execution and XLSX rendering. Cancelling an HTTP call stops the Vue worker; synchronous ClosedXML work retains its capacity slot until it finishes, even after a 504 response. See [operations](../docs/operations.md) for the complete contract and migration from 0.1.0.

## Format behavior

The workbook format has the following behavior: formula reference names span worksheets inside one workbook; named range notation requires a colon range; CSS declarations split on colons/semicolons; text is HTML-decoded after XML parsing; image dimensions are derived from cell/merge geometry instead of width/height attributes; print ranges select page-break-preview in serialized XML. The temporary `__scale_probe__` worksheet name and existing pagination/fit-row ordering remain unchanged. These are compatibility constraints for this first release, not promises for a future dialect version.

Regression tests cover typed/blank/invalid values, Ukrainian and escaped text, culture independence, formulas and names, merge occupancy, styles, dimensions, print setup, freeze variants, filters and breaks, pagination, images, precision in serialized OOXML, malformed XML/DTD rejection, request isolation, HTTP validation/limits, capacity saturation, and recovery after errors.
