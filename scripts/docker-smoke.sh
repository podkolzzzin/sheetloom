#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
smoke_image=${SMOKE_IMAGE:-sheetloom:local}
if [[ -z ${SMOKE_IMAGE:-} && ${SKIP_BUILD:-0} != 1 ]]; then docker build -t "$smoke_image" .; fi
smoke_dir=$(mktemp -d)
chmod 755 "$smoke_dir"
cp -R examples/basic "$smoke_dir/pack"
python3 - "$smoke_dir/pack" <<'PYTHON'
import json, pathlib, sys
pack = pathlib.Path(sys.argv[1])
manifest = json.loads((pack / 'manifest.json').read_text())
manifest['templates']['deadline'] = 'Deadline.vue'
(pack / 'manifest.json').write_text(json.dumps(manifest))
(pack / 'Deadline.vue').write_text("<script setup>while(true){}</script><template><Workbook/></template>")
PYTHON
smoke_container=""
cleanup() {
  if [[ -n "$smoke_container" ]]; then docker rm -f "$smoke_container" >/dev/null 2>&1 || true; fi
  rm -rf "$smoke_dir"
}
trap cleanup EXIT
smoke_container=$(docker run -d -e RENDER_TIMEOUT_MS=5000 --read-only --tmpfs /tmp:rw,nosuid,size=512m -p 127.0.0.1::8080 -v "$smoke_dir/pack:/templates:ro" "$smoke_image")
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
# .NET owns the public API and is the parent of the loopback-only Node process.
docker exec -i "$smoke_container" node --input-type=module <<'JS'
import fs from 'node:fs';
import assert from 'node:assert/strict';
const processes = fs.readdirSync('/proc').filter(pid => /^\d+$/.test(pid)).flatMap(pid => {
  try { return [{ pid: Number(pid), command: fs.readFileSync(`/proc/${pid}/cmdline`, 'utf8'), status: fs.readFileSync(`/proc/${pid}/status`, 'utf8') }]; }
  catch { return []; }
});
const dotnet = processes.find(p => p.command.startsWith('dotnet\0'));
const node = processes.find(p => p.command.startsWith('node\0/app/src/main.js\0'));
assert.ok(dotnet); assert.ok(node);
assert.equal(Number(node.status.match(/^PPid:\s+(\d+)/m)[1]), dotnet.pid);
const listeners = fs.readFileSync('/proc/net/tcp', 'utf8').split('\n').map(line => line.trim().split(/\s+/)).filter(row => row[3] === '0A');
assert.ok(listeners.some(row => row[1] === '0100007F:1F91'));
assert.ok(!listeners.some(row => row[1].endsWith(':1F91') && row[1] !== '0100007F:1F91'));
const ipv6 = fs.readFileSync('/proc/net/tcp6', 'utf8').split('\n').map(line => line.trim().split(/\s+/)).filter(row => row[3] === '0A');
assert.ok(!ipv6.some(row => row[1].endsWith(':1F91')));
JS
[[ $(curl -sS -o /dev/null -w '%{http_code}' -X POST "$smoke_url/render" -H 'Content-Type: application/json' -d '{"xml":"<Workbook/>"}') == 404 ]]

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
# The real public .NET connection must return JSON 504, then recover after Node cancellation.
[[ $(curl --max-time 10 -sS -o "$smoke_dir/deadline.json" -w '%{http_code}' "$smoke_url/v1/render/deadline" \
  -H 'Content-Type: application/json' -d '{"data":null}') == 504 ]]
python3 - "$smoke_dir/deadline.json" <<'PYTHON'
import json, sys
assert json.load(open(sys.argv[1]))['error']['code'] == 'render_timeout'
PYTHON
curl -fsS "$smoke_url/v1/render/invoice" -H 'Content-Type: application/json' \
  -d '{"data":{"customer":"After timeout","items":[]}}' -o "$smoke_dir/after-timeout.xlsx"
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
# Failure of either process must terminate the container with a nonzero status.
for target in node dotnet; do
  docker exec "$smoke_container" node --input-type=module -e 'import fs from "node:fs"; const target=process.argv[1]; for(const pid of fs.readdirSync("/proc")){if(!/^\d+$/.test(pid))continue;try{const command=fs.readFileSync(`/proc/${pid}/cmdline`,"utf8");if(target==="dotnet" ? command.startsWith("dotnet\0") : command.startsWith("node\0/app/src/main.js\0"))process.kill(Number(pid),"SIGKILL");}catch{}}' "$target"
  for attempt in $(seq 1 20); do
    [[ $(docker inspect -f '{{.State.Running}}' "$smoke_container") != true ]] && break
    sleep 1
  done
  [[ $(docker inspect -f '{{.State.Running}}' "$smoke_container") == false ]]
  [[ $(docker inspect -f '{{.State.ExitCode}}' "$smoke_container") != 0 ]]
  docker start "$smoke_container" >/dev/null
  wait_ready
done
# Normal SIGTERM shuts down both services within Docker's grace period.
docker stop -t 15 "$smoke_container" >/dev/null
[[ $(docker inspect -f '{{.State.ExitCode}}' "$smoke_container") == 0 ]]
docker rm "$smoke_container" >/dev/null
smoke_container=""
printf '{"apiVersion":2,"templates":{}}' > "$smoke_dir/pack/manifest.json"
if docker run --rm -v "$smoke_dir/pack:/templates:ro" "$smoke_image"; then
  echo 'Invalid pack unexpectedly started' >&2; exit 1
fi
echo 'Docker smoke passed: XLSX, precision, read-only mounts/rootfs, non-root, restart update, .NET parent, private Node, crash supervision, graceful shutdown, invalid startup.'
