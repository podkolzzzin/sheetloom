#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

demo_container=""
cleanup() {
  if [[ -n "$demo_container" ]]; then docker rm -f "$demo_container" >/dev/null 2>&1 || true; fi
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

if [[ ${SKIP_BUILD:-0} != 1 ]]; then docker build -t excel-template-engine:local .; fi
demo_container=$(docker run -d --read-only --tmpfs /tmp:rw,nosuid,size=512m \
  -p 127.0.0.1::8080 -v "$PWD/examples/basic:/templates:ro" excel-template-engine:local)
demo_port=$(docker port "$demo_container" 8080/tcp | cut -d: -f2)
demo_url="http://127.0.0.1:$demo_port"
demo_ready=0
for attempt in $(seq 1 90); do
  if curl -fsS "$demo_url/health" >/dev/null 2>&1; then demo_ready=1; break; fi
  if [[ $(docker inspect -f '{{.State.Running}}' "$demo_container") != true ]]; then break; fi
  sleep 1
done
if [[ $demo_ready != 1 ]]; then
  docker logs "$demo_container"
  echo 'Demo service did not become ready.' >&2
  exit 1
fi
mkdir -p .artifacts/demo
curl --fail-with-body "$demo_url/v1/render/invoice" \
  -H 'Content-Type: application/json' --data-binary @examples/basic/request.json \
  -o .artifacts/demo/invoice.xlsx
echo "Created $PWD/.artifacts/demo/invoice.xlsx (two items; total formula evaluates to 50)."
