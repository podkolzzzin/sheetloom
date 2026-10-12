# Operations and API reference

A stateless JSON → Vue SSR → workbook XML → XLSX service. The image contains the engine; your trusted Vue templates live in a separate, read-only mounted directory. Template packs are compiled once at startup. Restart after changing a pack.

## Run

From a checkout of [the repository](https://github.com/podkolzzzin/sheetloom), start the published image:

```sh
docker pull podkolzzzin1996/sheetloom:0.2.0
docker run -d --name sheetloom -p 127.0.0.1:8080:8080 \
  -v "$PWD/examples/basic:/templates:ro" podkolzzzin1996/sheetloom:0.2.0
```

After `docker logs sheetloom` shows `ready: public .NET API`:

```sh
curl --fail http://localhost:8080/health
curl --fail http://localhost:8080/v1/render/invoice \
  -H 'Content-Type: application/json' \
  -d '{"data":{"customer":"Example","items":[{"name":"Consulting","amount":42.5}]},"options":{"precisionAsDisplayed":true}}' \
  -o invoice.xlsx
```

Remove the demo container with `docker rm -f sheetloom`. Replace `examples/basic` with an absolute path to your own pack.

The [Docker Hub image](https://hub.docker.com/r/podkolzzzin1996/sheetloom) currently supports **Linux/amd64**. ARM hosts require emulation and `--platform linux/amd64`. Version tags are retained unchanged; `latest` tracks the newest release. For reproducible deployments, use the digest printed by `docker pull`: `podkolzzzin1996/sheetloom@sha256:…`.

To build locally, run `docker build -t sheetloom:local .` and use `sheetloom:local` in the commands above. `bash scripts/demo.sh` builds and exercises the local image; `DEMO_IMAGE=podkolzzzin1996/sheetloom:0.2.0 bash scripts/demo.sh` uses the published release.

The container entry point is `tini → .NET`. ASP.NET starts a child Node process on `127.0.0.1:8081`, waits for pack compilation and template discovery, then opens the public API on port 8080. Node only converts JSON and Vue to XML. .NET renders that XML to XLSX in-process and returns it directly to the client.

```text
Client → .NET :8080 → private Node :8081 (JSON → XML)
         .NET ← XML
Client ← XLSX ← .NET / ClosedXML
```

Node listens only on loopback; publish only port 8080. A Node crash stops the .NET host with a nonzero exit code. A .NET crash ends the container. Docker/orchestrator restart policy owns recovery; normal shutdown stops both processes.

## Template pack v1

```json
{
  "apiVersion": 1,
  "templates": { "invoice": "Invoice.vue" },
  "aliases": { "@": "components" },
  "setup": "setup.ts"
}
```

Only `apiVersion` and nonempty `templates` are required. Paths are relative to the pack directory and must stay inside it, including symlinks. IDs use letters, numbers, `_` and `-`. The optional setup module runs for side effects before template rendering (for example locale registration). Components receive the request data through Vue's `inject('ssrRequest').body`. Each request gets a fresh app and worker, so module state cannot leak between requests.

The engine supplies Vue, Vue Router, Moment and LINQ dependencies at pinned versions. Node built-ins are available. No package installation happens at startup. Relative source imports, TypeScript and Vue SFCs are compiled by Vite. Literal `new URL('./asset.png', import.meta.url)` references retain source-file locations, including substitution-free backtick literals, so filesystem assets remain accessible in a mounted pack.

See [the workbook XML and style reference](workbook-format.md) for cells, formulas, images and printing. Formulas are written for the spreadsheet application to calculate; the service does not promise evaluated formula caches.

**Packs are trusted executable application code.** Path checks catch accidental escapes; they are not a JavaScript sandbox. Do not mount untrusted code or expose this unauthenticated service directly to the Internet. Deploy behind your authenticated application or gateway.

## HTTP contract

`POST /v1/render/{template}` accepts `{"data": <any JSON>, "options": {"precisionAsDisplayed": false}}`. `data` is required; options can be omitted. Success returns the XLSX media type and an attachment filename. `precisionAsDisplayed: true` writes Excel's `fullPrecision=false` calculation setting after serialization. Consumers reopening and saving with another library must preserve it themselves.

`GET /health` checks the private Node service; `GET /v1/templates` lists configured IDs. Failures return `{"error":{"code":"...","message":"..."}}`. Invalid requests return 400, unknown templates 404, oversized bodies 413, invalid workbook/template output 422, capacity exhaustion 503, private service failure 502, and timeouts 504. Request bodies and rendered XML are not logged.

## Configuration and operations

| Variable | Default | Purpose |
|---|---|---|
| `TEMPLATES_DIR` | `/templates` | Read-only template pack directory |
| `CACHE_DIR` | `/tmp/sheetloom` | Writable build cache, outside the pack |
| `PORT` | `8080` | Public HTTP port |
| `RENDER_TIMEOUT_MS` | `120000` | Combined template/render response deadline |
| `BODY_LIMIT_BYTES` | `10485760` | Maximum incoming JSON body |
| `RENDER_CONCURRENCY` | `2` | Combined template/XLSX slots; excess requests receive 503 |
| `NODE_PORT` | `8081` | Private Node loopback port; must differ from `PORT` |
| `NODE_SCRIPT` | `/app/src/main.js` | Node entry point; override for source development |
| `RENDERER_FALLBACK_FONT` | `DejaVu Sans` | Font used when requested fonts are unavailable |

The container runs as non-root. Pack directories must be traversable and files readable by its UID (the supplied image uses 1654). You can run with `--read-only --tmpfs /tmp:rw,nosuid,size=512m`. Set `RENDER_CONCURRENCY` to bound the entire rendering pipeline. Vue workers have a 512 MiB old-generation heap limit. Rendered XML is limited to 8 MiB at the public boundary. Apply container memory/CPU limits suitable for your reports.

The response deadline covers the Node call and XLSX rendering together. Cancelling the Node call terminates its Vue worker. An in-progress ClosedXML operation is synchronous and may finish after the HTTP deadline; its shared rendering slot remains occupied until it completes. This prevents timed-out requests from accumulating unbounded background work. Health checks indicate process availability, not spare render capacity. The host allows up to 10 seconds for shutdown and stops its Node child; Docker may enforce an outer stop timeout.

Font metrics affect row heights and pagination. The image includes DejaVu; mount your licensed fonts read-only under `/usr/local/share/fonts` and restart when exact production typography is required. Use the same fonts for compatibility comparisons.

## Upgrading from 0.1.0

HTTP routes, request bodies and XLSX responses are unchanged. The public port now belongs to .NET. Replace `RENDERER_PORT` with `NODE_PORT` if you customized the private port, and use only `RENDER_CONCURRENCY` to control capacity; `RENDERER_CONCURRENCY` is removed. `RENDERER_FALLBACK_FONT` is unchanged. The old private XML-to-XLSX HTTP endpoint no longer exists.

## Development and verification

Requirements: Node 22.12+ (CI/image use Node 24), .NET 10 SDK, Docker and Python 3 for container checks.

```sh
npm ci --ignore-scripts
npm test
dotnet restore dotnet/Sheetloom.sln --locked-mode
dotnet test dotnet/Sheetloom.sln --no-restore
npm run test:docker
python3 -m venv .venv-e2e
.venv-e2e/bin/python -m pip install -r tests/e2e/requirements.txt
.venv-e2e/bin/python tests/e2e/test_workbooks.py
```

For local development after installing dependencies, `npm start` launches the .NET host with the example pack and its private Node child. Override `TEMPLATES_DIR` with an absolute path to use another pack.

The Node tests exercise pack validation, read-only compilation, imports/assets, locale setup, concurrent state isolation, HTTP validation, error privacy, capacity and real-socket deadlines. .NET tests cover the public HTTP contract, private Node transport, cancellation, capacity, workbook semantics, formatting, printing, images, XML safety and precision. The Docker suite checks real XLSX output, non-root/read-only operation, pack updates after restart, process supervision and invalid-pack startup.

CI also runs a separate **Docker E2E / openpyxl** job. It sends JSON to the production container, reads returned files with an independent XLSX implementation, and checks cells/types, formulas, styles, local names, print/freeze settings, pagination, images and request isolation. It uploads synthetic workbook evidence and logs, including on failure. All gates use the source and synthetic fixtures in this repository. See [independent E2E scenarios and historical evidence](../tests/e2e/README.md), [Node regressions](test-history-node.md), and [renderer regressions](test-history-renderer.md).

## Workbook format and license

The workbook renderer uses ClosedXML 0.105.1 and the documented XML dialect. Template packs are supplied by each application. Regression tests compare workbook semantics rather than ZIP bytes and include all required synthetic fixtures.

The engine is licensed under [MIT](../LICENSE). Third-party dependencies retain their own licenses; see the [dependency inventory](../THIRD_PARTY_NOTICES.md).
