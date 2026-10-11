#!/usr/bin/env python3
"""Inventory pinned dependency license metadata; never infer licensing terms."""
import argparse
import json
import os
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def generate(nuget_cache):
    lines = [
        '# Third-party dependency inventory', '',
        'The engine source is MIT-licensed. Dependencies retain their own licenses.',
        'This inventory records package metadata, not a replacement for upstream license texts or notices.',
        'Preserve those texts when redistributing dependencies or container images.', '',
        'Regenerate after `npm ci --ignore-scripts` and a locked .NET restore:', '',
        '```sh', 'python3 scripts/dependency-notices.py', '```', '',
        '## npm lockfile', '',
        'Includes transitive and platform-optional packages from `package-lock.json`; not every package is installed on every platform.', '',
        '| Package | Version | Declared license |', '| --- | --- | --- |',
    ]
    packages = json.loads((ROOT / 'package-lock.json').read_text())['packages']
    seen = set()
    for location, package in sorted(packages.items()):
        if not location:
            continue
        name = location.split('node_modules/')[-1]
        version = package['version']
        if (name, version) in seen:
            continue
        seen.add((name, version))
        license_id = package.get('license')
        if not isinstance(license_id, str) or not license_id:
            raise ValueError(f'Missing license metadata: {name}@{version}')
        lines.append(f'| [{name}](https://www.npmjs.com/package/{name}/v/{version}) | {version} | {license_id} |')
    lines += ['', '## NuGet lockfiles', '',
              'Runtime packages come from the service lockfile. Test-only dependencies are included for development completeness.', '',
              '| Package | Version | Scope | Declared license |', '| --- | --- | --- | --- |']
    resolved = {}
    for lock in sorted((ROOT / 'dotnet').glob('*/packages.lock.json')):
        for framework in json.loads(lock.read_text())['dependencies'].values():
            for name, package in framework.items():
                if package['type'] == 'Project':
                    continue
                key = (name, package['resolved'])
                scope = 'Test' if '.Tests' in lock.parent.name else 'Runtime'
                if key not in resolved or scope == 'Runtime':
                    resolved[key] = scope
    for (name, version), scope in sorted(resolved.items()):
        spec = nuget_cache / name.lower() / version / f'{name.lower()}.nuspec'
        tree = ET.parse(spec)
        metadata = {element.tag.rsplit('}', 1)[-1]: element.text for element in tree.iter()}
        license_id = metadata.get('license') or metadata.get('licenseUrl')
        if not license_id:
            raise ValueError(f'Missing license metadata: {name}@{version}')
        lines.append(f'| [{name}](https://www.nuget.org/packages/{name}/{version}) | {version} | {scope} | {license_id} |')
    lines += ['', '## Container components', '',
              'The Docker image also contains Node.js, .NET/ASP.NET, Debian packages, DejaVu fonts, curl and tini.',
              'Their licenses and notices are separate from the engine license. The base-image and system-package versions',
              'are resolved during the image build; this source-lockfile inventory is not an image SBOM.',
              'Review the exact image and retain its upstream notices before distributing a binary release.', '']
    return '\n'.join(lines)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--nuget-cache', type=Path, default=Path(os.environ.get('NUGET_PACKAGES', Path.home() / '.nuget/packages')))
    args = parser.parse_args()
    try:
        content = generate(args.nuget_cache)
        target = ROOT / 'THIRD_PARTY_NOTICES.md'
        if args.check:
            if not target.exists() or target.read_text() != content:
                sys.exit('Dependency inventory is stale; run python3 scripts/dependency-notices.py')
            print('Dependency inventory matches lockfiles and restored NuGet metadata.')
        else:
            target.write_text(content)
            print('Wrote THIRD_PARTY_NOTICES.md')
    except (OSError, ValueError, ET.ParseError) as error:
        sys.exit(f'Cannot generate dependency inventory: {error}')
