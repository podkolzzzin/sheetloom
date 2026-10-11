#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ ${SKIP_BUILD:-0} != 1 ]]; then docker build -t sheetloom:local .; fi
smoke_dir=$(mktemp -d)
chmod 755 "$smoke_dir"
cp -R examples/basic "$smoke_dir/pack"
smoke_container=""
cleanup() {
  if [[ -n "$smoke_container" ]]; then docker rm -f "$smoke_container" >/dev/null 2>&1 || true; fi
  rm -rf "$smoke_dir"
}
trap cleanup EXIT
smoke_container=$(docker run -d --read-only --tmpfs /tmp:rw,nosuid,size=512m -p 127.0.0.1::8080 -v "$smoke_dir/pack:/templates:ro" sheetloom:local)
smoke_port=$(docker port "$smoke_container" 8080/tcp | cut -d: -f2)
smoke_url="http://127.0.0.1:$smoke_port"
wait_ready() {
  smoke_port=$(docker port "$smoke_container" 8080/tcp | cut -d: -f2)
  smoke_url="http://127.0.0.1:$smoke_port"
  for attempt in $(seq 1 120); do
    if curl -fsS "$smoke_url/health" > /dev/null 2>&1; then return; fi
    if [[ $(docker inspect -f '{{.State.Running}}' "$smoke_container") != true ]]; then docker logs "$smoke_container"; return 1; fi
    sleep 1
  done
  docker logs "$smoke_container"
  return 1
}
wait_ready
[[ $(docker exec "$smoke_container" id -u) != 0 ]]
curl -fsS "$smoke_url/v1/render/invoice" -H 'Content-Type: application/json' \
  -d '{"data":{"customer":"Україна & <example>","items":[{"name":"Service","amount":42.5}]},"options":{"precisionAsDisplayed":true}}' -o "$smoke_dir/first.xlsx"
python3 - "$smoke_dir/first.xlsx" <<'PY'
import sys,zipfile,xml.etree.ElementTree as E
with zipfile.ZipFile(sys.argv[1]) as z:
 ns={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
 workbook=E.fromstring(z.read('xl/workbook.xml'))
 assert workbook.find('s:calcPr',ns).get('fullPrecision') in ('0','false')
 assert workbook.find('s:sheets/s:sheet',ns).get('name')=='Invoice'
 strings=E.fromstring(z.read('xl/sharedStrings.xml'))
 assert 'Україна & <example>' in ''.join(strings.itertext())
 sheet=E.fromstring(z.read('xl/worksheets/sheet1.xml'))
 assert sheet.find("s:sheetData/s:row/s:c[@r='B2']/s:v",ns).text=='42.5'
 assert sheet.find(".//s:f",ns) is not None
PY
# Empty input must produce a numeric zero, not a circular total formula.
curl -fsS "$smoke_url/v1/render/invoice" -H 'Content-Type: application/json' \
  -d '{"data":{"customer":"Empty","items":[]}}' -o "$smoke_dir/empty.xlsx"
python3 - "$smoke_dir/empty.xlsx" <<'PY'
import sys,zipfile,xml.etree.ElementTree as E
with zipfile.ZipFile(sys.argv[1]) as z:
 ns={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
 sheet=E.fromstring(z.read('xl/worksheets/sheet1.xml'))
 assert sheet.find('.//s:f',ns) is None
 assert sheet.find("s:sheetData/s:row/s:c[@r='B2']/s:v",ns).text=='0'
PY
[[ $(curl -sS -o "$smoke_dir/invalid.json" -w '%{http_code}' "$smoke_url/v1/render/invoice" \
  -H 'Content-Type: application/json' -d '{"data":{"customer":"Invalid","items":null}}') == 422 ]]
# A new pack version takes effect after restart, without an image rebuild.
python3 - "$smoke_dir/pack/Invoice.vue" <<'PY'
import sys
p=sys.argv[1]
s=open(p).read().replace('name="Invoice"','name="Updated"')
open(p,'w').write(s)
PY
docker restart -t 15 "$smoke_container" >/dev/null
wait_ready
curl -fsS "$smoke_url/v1/render/invoice" -H 'Content-Type: application/json' \
  -d '{"data":{"customer":"Restart","items":[{"name":"Item","amount":1}]}}' -o "$smoke_dir/second.xlsx"
python3 - "$smoke_dir/second.xlsx" <<'PY'
import sys,zipfile,xml.etree.ElementTree as E
with zipfile.ZipFile(sys.argv[1]) as z:
 n={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
 assert E.fromstring(z.read('xl/workbook.xml')).find('s:sheets/s:sheet',n).get('name')=='Updated'
PY
# Renderer failure must stop the whole container, not leave a falsely healthy API.
docker exec "$smoke_container" node --input-type=module -e 'import fs from "node:fs"; for(const pid of fs.readdirSync("/proc")){if(!/^\d+$/.test(pid))continue;try{if(fs.readFileSync(`/proc/${pid}/comm`,"utf8").trim()==="dotnet")process.kill(Number(pid),"SIGKILL");}catch{}}'
for attempt in $(seq 1 20); do
  [[ $(docker inspect -f '{{.State.Running}}' "$smoke_container") != true ]] && break
  sleep 1
done
[[ $(docker inspect -f '{{.State.Running}}' "$smoke_container") == false ]]
[[ $(docker inspect -f '{{.State.ExitCode}}' "$smoke_container") != 0 ]]
docker rm "$smoke_container" >/dev/null
smoke_container=""
printf '{"apiVersion":2,"templates":{}}' > "$smoke_dir/pack/manifest.json"
if docker run --rm -v "$smoke_dir/pack:/templates:ro" sheetloom:local; then
  echo 'Invalid pack unexpectedly started' >&2; exit 1
fi
echo 'Docker smoke passed: XLSX, precision, read-only mounts/rootfs, non-root, restart update, supervision, invalid startup.'
