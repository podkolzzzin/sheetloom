# Renderer

`dotnet test dotnet/Sheetloom.sln` runs the library and real in-memory HTTP integration tests. `dotnet run --project dotnet/Sheetloom.Service` starts the private renderer on `127.0.0.1:8081`.

The renderer converts the workbook XML model to XLSX using ClosedXML 0.105.1. XML parsing forbids DTDs/external entities and limits document size. Failed renders dispose allocated workbooks. The serialization wrapper changes `calcPr.fullPrecision` only when requested.

## Internal service contract

- `GET /health`: `{ "status": "ok" }`.
- `POST /render`: `{ "xml": "<Workbook>...</Workbook>", "precisionAsDisplayed": false }`; returns XLSX.
- `RENDERER_PORT` defaults to 8081; binding always stays on loopback.
- `RENDERER_CONCURRENCY` defaults to 2. Busy requests receive 503 without joining an unbounded queue.
- `RENDERER_FALLBACK_FONT` defaults to `DejaVu Sans`; install production fonts if exact font metrics are required.
- JSON request limit is 16 MiB; XML limit is 8 Mi characters. Malformed JSON/schema is 400, excessive size 413, wrong media type 415, rendering failure 422. Errors have `{ "error": { "code", "message" } }`; report content and exception messages are not logged.

Each render allocates its own workbook, formula tracking, and models. In-flight synchronous ClosedXML work is bounded by concurrency; public request deadlines are enforced by the Node supervisor. Work already inside ClosedXML cannot be preempted by an HTTP cancellation.

## Format behavior

The workbook format has the following behavior: formula reference names span worksheets inside one workbook; named range notation requires a colon range; CSS declarations split on colons/semicolons; text is HTML-decoded after XML parsing; image dimensions are derived from cell/merge geometry instead of width/height attributes; print ranges select page-break-preview in serialized XML. The temporary `__scale_probe__` worksheet name and existing pagination/fit-row ordering remain unchanged. These are compatibility constraints for this first release, not promises for a future dialect version.

Regression tests cover typed/blank/invalid values, Ukrainian and escaped text, culture independence, formulas and names, merge occupancy, styles, dimensions, print setup, freeze variants, filters and breaks, pagination, images, precision in serialized OOXML, malformed XML/DTD rejection, request isolation, HTTP validation/limits, capacity saturation, and recovery after errors.
