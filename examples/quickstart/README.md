# Copy-and-run example

Copy this whole folder anywhere. You need Docker with Compose and curl; the engine comes from Docker Hub. The image targets Linux/amd64 and uses emulation on ARM hosts.

```sh
docker compose up --wait
curl --fail-with-body http://localhost:8080/v1/render/report \
  -H 'Content-Type: application/json' \
  --data-binary @request.json -o report.xlsx
docker compose down
```

`report.xlsx` contains Consulting / 42.5 and Materials / 7.5 on the Report sheet. The amounts are numeric cells.

Edit `templates/Report.vue` and `request.json` for your data. Register additional template IDs in `templates/manifest.json`. After changing templates, run `docker compose restart` and wait for the ready message in `docker compose logs`.

`compose.yaml` lists all supported environment settings. If port 8080 is already in use, change the first port in `ports` and the curl URL. Leave the internal port unchanged. Keep Node's port private.
