# Source release preparation

Publish only source code, documentation and synthetic examples. Keep credentials, generated reports and local build output outside the release.

## Before making the repository public

- Run all CI jobs on the exact revision to publish: Node/.NET, the public demo, Docker lifecycle, independent XLSX checks, and the full-history secret scan.
- Review every branch and tag being published, including binary files, commit messages and author metadata. Scan findings must be reviewed privately; rotate a real credential before cleaning history.
- Keep [LICENSE](../LICENSE), [SECURITY.md](../SECURITY.md), [CONTRIBUTING.md](../CONTRIBUTING.md), the lockfiles and [third-party inventory](../THIRD_PARTY_NOTICES.md) in the source release. Confirm rights to any newly added code or assets.
- Enable GitHub secret scanning/push protection where available, private vulnerability reporting, and require the CI jobs before merging. These repository settings are not enabled by committing YAML.

CI fetches full history and uses [Gitleaks](https://github.com/gitleaks/gitleaks) with redacted output. To repeat the same check locally:

```sh
docker run --rm -v "$PWD:/repo:ro" ghcr.io/gitleaks/gitleaks:v8.24.3 \
  git /repo --redact --log-opts="--all"
```

This scans committed history reachable from local refs. Fetch the intended branches/tags first; uncommitted or untracked files need a separate review before inclusion. Never publish unredacted scan reports.

## Validation required for the release revision

Run the complete CI workflow on the revision being published. Record results for the Node and .NET suites, the demo invoice, independent XLSX parsing, container lifecycle and full-history secret scanning. A local library test does not replace container or HTTP checks.

Check the empty-invoice case as well as the example request: the empty total must be numeric zero without a formula. Inspect only synthetic workbook evidence in public CI artifacts.

## Before distributing an image

Source publication and image distribution have different contents. Build the exact release image, inventory its OS/runtime dependencies, review vulnerabilities, and retain the relevant license texts and notices. The source dependency table is not an image SBOM. Publish immutable image references and record the source revision only after those checks pass.


## Docker Hub release

The public image is `podkolzzzin1996/sheetloom` (Linux/amd64). Keep release tags immutable and move `latest` only after verification. Build from a clean, committed revision with green CI:

```sh
version=0.3.1
image=podkolzzzin1996/sheetloom
docker build --pull --platform linux/amd64 \
  --build-arg VERSION="$version" --build-arg REVISION="$(git rev-parse HEAD)" \
  -t "$image:$version" .
DEMO_IMAGE="$image:$version" bash scripts/demo.sh
E2E_IMAGE="$image:$version" .venv-e2e/bin/python tests/e2e/test_workbooks.py
```

Before pushing, scan that image with your container scanner, retain an SBOM and review its vulnerability report. Keep runtime NuGet license texts in `licenses/` aligned with the lockfiles; Node, .NET, npm and system-package notices are also retained in the image. Base images are pinned by digest in the Dockerfile. Update those pins deliberately for runtime security releases. System-package updates are resolved during the build, so a rebuild may still have a different digest.

```sh
docker login
docker push "$image:$version"
docker tag "$image:$version" "$image:latest"
docker push "$image:latest"
docker pull "$image:$version"
```

Record the registry digest, source revision, scan results and supported platform in the GitHub release. Verify the downloaded image with `DEMO_IMAGE="$image@sha256:…" bash scripts/demo.sh`. Update the README and Docker Hub overview for the new version. Never commit registry credentials.
