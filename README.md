# Sheetloom

*Excel files from templates and data.*

**JSON + a Vue template → a ready-to-use `.xlsx`.** Define worksheets, cell types, formulas and print layout with Vue components. One container runs a public .NET API and a private Node.js template service; no Excel installation is needed on the server.

## Quick start

You need Docker, curl and a shell. The [Docker Hub image](https://hub.docker.com/r/podkolzzzin1996/sheetloom) supports **Linux/amd64** (ARM hosts need emulation).

**1. Create a `templates` folder and a `templates/manifest.json` file** with this content:

```json
{
  "apiVersion": 1,
  "templates": { "report": "Report.vue" }
}
```

**2. Save this template as `templates/Report.vue`.** `templateData` contains the request's `data` directly:

```vue
<script setup lang="ts">
import { inject } from 'vue';
type ReportData = { items: { name: string; amount: number }[] };
const { items } = inject<ReportData>('templateData')!;
</script>
<template>
  <Workbook>
    <Worksheet name="Report" style="number-format:0.00">
      <Row style="font-weight:bold;background-color:#eaf0f6">
        <Cell style="width:24">Item</Cell>
        <Cell style="width:14">Amount</Cell>
      </Row>
      <Row v-for="(item, i) in items" :key="i"
        :excel-row-ref="i === 0 ? 'firstItem lastItem' : 'lastItem'">
        <Cell>{{ item.name }}</Cell>
        <Cell excel-column-ref="amount" data-type="Number">{{ item.amount }}</Cell>
      </Row>
      <Row style="font-weight:bold">
        <Cell>Total</Cell>
        <Cell v-if="items.length">
          <FormulaR1C1>SUM(R{firstItem}C{amount}:R{lastItem}C{amount})</FormulaR1C1>
        </Cell>
        <Cell v-else data-type="Number">0</Cell>
      </Row>
    </Worksheet>
  </Workbook>
</template>
```

`firstItem` and `lastItem` mark the item rows; `amount` marks their numeric column. The formula uses these refs to follow the rendered cells.

**3. Start the published image** from the same directory:

```sh
docker run --rm -p 127.0.0.1:8080:8080 \
  -v "$PWD/templates:/templates:ro" podkolzzzin1996/sheetloom:0.3.0
```

After the `ready: public .NET API` message, open another terminal in that directory:

```sh
curl --fail-with-body http://localhost:8080/v1/render/report \
  -H 'Content-Type: application/json' \
  -d '{"data":{"items":[{"name":"Consulting","amount":42.5},{"name":"Materials","amount":7.5}]}}' \
  -o report.xlsx
```

Open `report.xlsx`: the **Report** sheet has a shaded, bold header, amounts formatted as **42.50** and **7.50**, and a bold **Total** row with a `SUM` formula. Excel calculates **50.00** when the file opens; an empty list produces numeric **0.00**. Press Ctrl+C in the Docker terminal to stop. Restart after changing a template; no image rebuild is needed.

## Docker Compose

Copy [examples/quickstart/](examples/quickstart/) anywhere, or save this as `compose.yaml` beside the `templates/` folder above. It includes every supported configuration variable with its default value:

```yaml
services:
  sheetloom:
    image: podkolzzzin1996/sheetloom:0.3.0
    platform: linux/amd64
    ports: ["127.0.0.1:8080:8080"]
    volumes: ["./templates:/templates:ro"]
    environment:
      PORT: "8080"
      NODE_PORT: "8081"
      TEMPLATES_DIR: /templates
      CACHE_DIR: /tmp/sheetloom
      NODE_SCRIPT: /app/src/main.js
      RENDER_TIMEOUT_MS: "120000"
      BODY_LIMIT_BYTES: "10485760"
      RENDER_CONCURRENCY: "2"
      RENDERER_FALLBACK_FONT: DejaVu Sans
    read_only: true
    tmpfs: ["/tmp:rw,nosuid,size=512m"]
    stop_grace_period: 15s
    restart: unless-stopped
```

Run `docker compose up --wait`, then use the same curl command above. Stop with `docker compose down`. The copied folder includes the template and manifest from this quick start. See [configuration](docs/operations.md#configuration-and-operations) for limits and font options.

## How it works

The manifest maps the `report` ID to `Report.vue`. .NET starts Node on loopback and sends it JSON. A fresh Vue worker produces `Workbook → Worksheet → Row → Cell` XML; .NET converts it to XLSX with ClosedXML. Only the .NET port is published.

| Goal | Short guide |
| --- | --- |
| Create your own export | [Template packs](docs/how-to/01-template-pack.md) |
| Preserve numbers, dates and leading zeros | [Cell types and styles](docs/how-to/02-cell-types.md) |
| Add totals | [Formulas and empty lists](docs/how-to/03-formulas.md) |
| Set up paper and headers | [Printing and frozen rows](docs/how-to/04-printing.md) |
| Call the service from your backend | [HTTP clients and errors](docs/how-to/05-http-client.md) |

Packs are **trusted executable code**. The service has no authentication: use a private network or your authenticated gateway. Pin an image version for deployment; `latest` follows new releases.

[XML reference](docs/workbook-format.md) · [API and configuration](docs/operations.md) · [Code map](docs/architecture.md) · [Contributing](CONTRIBUTING.md) · [Security](SECURITY.md)

Licensed under [MIT](LICENSE). Dependencies retain [their own licenses](THIRD_PARTY_NOTICES.md).
