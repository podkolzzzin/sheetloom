# Copy-and-run example

Copy this whole folder anywhere. You need Docker with Compose and curl; the engine comes from Docker Hub. The image targets Linux/amd64 and uses emulation on ARM hosts.

```sh
docker compose up --wait
curl --fail-with-body http://localhost:8080/v1/render/report \
  -H 'Content-Type: application/json' \
  -d '{"data":{"items":[{"name":"Consulting","amount":42.5},{"name":"Materials","amount":7.5}]}}' \
  -o report.xlsx
docker compose down
```

`report.xlsx` has a shaded, bold header and amounts formatted as 42.50 and 7.50 on the Report sheet. The bold Total row contains a `SUM` formula using the `firstItem`, `lastItem` and `amount` refs; Excel calculates 50.00 when the file opens. An empty list produces numeric 0.00 without a formula.

Edit `templates/Report.vue` and the inline JSON in curl for your data. Register additional template IDs in `templates/manifest.json`. After changing templates, run `docker compose restart` and wait for the ready message in `docker compose logs`.

`compose.yaml` lists all supported environment settings. If port 8080 is already in use, change the first port in `ports` and the curl URL. Leave the internal port unchanged. Keep Node's port private.
