# Sheetloom

*Excel files from templates and data.*

**JSON + a Vue template → a ready-to-use `.xlsx`.** Define worksheets, cell types, formulas and print layout with Vue components. One container includes Node.js and the .NET renderer; no Excel installation is needed on the server.

## Quick start

You need Docker, Bash and curl. Run from the repository root:

```bash
docker build -t sheetloom:local .
docker run --rm -p 127.0.0.1:8080:8080 \
  -v "$PWD/examples/basic:/templates:ro" sheetloom:local
```

After the `ready` message, run in another terminal:

```bash
curl --fail-with-body http://localhost:8080/v1/render/invoice \
  -H 'Content-Type: application/json' \
  --data-binary @examples/basic/request.json -o invoice.xlsx
```

Open `invoice.xlsx`: the **Invoice** sheet contains a customer, two items and a total formula that evaluates to **50**. Excel or a compatible application calculates formulas when the file opens. To start and stop the demo automatically, run `bash scripts/demo.sh`; the workbook stays in `.artifacts/demo/`.

## How it works

In [manifest.json](examples/basic/manifest.json), the `invoice` ID points to [Invoice.vue](examples/basic/Invoice.vue). Send `{"data": {...}}`; the template receives `data` through `inject('ssrRequest').body`. Vue produces `Workbook → Worksheet → Row → Cell` markup, which ClosedXML converts to Excel.

Templates are mounted separately from the engine. Restart the container after changing a pack; no image rebuild is needed. Each request runs in a fresh Vue worker.

| Goal | Short guide |
| --- | --- |
| Create your own export | [Template packs](docs/how-to/01-template-pack.md) |
| Preserve numbers, dates and leading zeros | [Cell types and styles](docs/how-to/02-cell-types.md) |
| Add totals | [Formulas and empty lists](docs/how-to/03-formulas.md) |
| Set up paper and headers | [Printing and frozen rows](docs/how-to/04-printing.md) |
| Call the service from your backend | [HTTP clients and errors](docs/how-to/05-http-client.md) |

Packs are **trusted executable code**. The service has no authentication: use a private network or your authenticated gateway.

[XML reference](docs/workbook-format.md) · [API and configuration](docs/operations.md) · [Contributing](CONTRIBUTING.md) · [Security](SECURITY.md)

Licensed under [MIT](LICENSE). Dependencies retain [their own licenses](THIRD_PARTY_NOTICES.md).
