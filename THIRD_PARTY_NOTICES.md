# Third-party dependency inventory

The engine source is MIT-licensed. Dependencies retain their own licenses.
This inventory records package metadata, not a replacement for upstream license texts or notices.
Preserve those texts when redistributing dependencies or container images.

Regenerate after `npm ci --ignore-scripts` and a locked .NET restore:

```sh
python3 scripts/dependency-notices.py
```

## npm lockfile

Includes transitive and platform-optional packages from `package-lock.json`; not every package is installed on every platform.

| Package | Version | Declared license |
| --- | --- | --- |
| [@babel/helper-string-parser](https://www.npmjs.com/package/@babel/helper-string-parser/v/7.29.7) | 7.29.7 | MIT |
| [@babel/helper-validator-identifier](https://www.npmjs.com/package/@babel/helper-validator-identifier/v/7.29.7) | 7.29.7 | MIT |
| [@babel/parser](https://www.npmjs.com/package/@babel/parser/v/7.29.9) | 7.29.9 | MIT |
| [@babel/types](https://www.npmjs.com/package/@babel/types/v/7.29.8) | 7.29.8 | MIT |
| [@fastify/ajv-compiler](https://www.npmjs.com/package/@fastify/ajv-compiler/v/4.0.7) | 4.0.7 | MIT |
| [@fastify/deepmerge](https://www.npmjs.com/package/@fastify/deepmerge/v/3.2.2) | 3.2.2 | MIT |
| [@fastify/error](https://www.npmjs.com/package/@fastify/error/v/4.2.1) | 4.2.1 | MIT |
| [@fastify/fast-json-stringify-compiler](https://www.npmjs.com/package/@fastify/fast-json-stringify-compiler/v/5.1.1) | 5.1.1 | MIT |
| [@fastify/forwarded](https://www.npmjs.com/package/@fastify/forwarded/v/3.1.1) | 3.1.1 | MIT |
| [@fastify/merge-json-schemas](https://www.npmjs.com/package/@fastify/merge-json-schemas/v/0.2.2) | 0.2.2 | MIT |
| [@fastify/proxy-addr](https://www.npmjs.com/package/@fastify/proxy-addr/v/5.1.2) | 5.1.2 | MIT |
| [@jridgewell/gen-mapping](https://www.npmjs.com/package/@jridgewell/gen-mapping/v/0.3.13) | 0.3.13 | MIT |
| [@jridgewell/remapping](https://www.npmjs.com/package/@jridgewell/remapping/v/2.3.5) | 2.3.5 | MIT |
| [@jridgewell/resolve-uri](https://www.npmjs.com/package/@jridgewell/resolve-uri/v/3.1.2) | 3.1.2 | MIT |
| [@jridgewell/sourcemap-codec](https://www.npmjs.com/package/@jridgewell/sourcemap-codec/v/1.6.0) | 1.6.0 | MIT |
| [@jridgewell/trace-mapping](https://www.npmjs.com/package/@jridgewell/trace-mapping/v/0.3.31) | 0.3.31 | MIT |
| [@oxc-project/types](https://www.npmjs.com/package/@oxc-project/types/v/0.153.0) | 0.153.0 | MIT |
| [@pinojs/redact](https://www.npmjs.com/package/@pinojs/redact/v/0.4.0) | 0.4.0 | MIT |
| [@rolldown/binding-android-arm-eabi](https://www.npmjs.com/package/@rolldown/binding-android-arm-eabi/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/binding-android-arm64](https://www.npmjs.com/package/@rolldown/binding-android-arm64/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/binding-darwin-arm64](https://www.npmjs.com/package/@rolldown/binding-darwin-arm64/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/binding-darwin-x64](https://www.npmjs.com/package/@rolldown/binding-darwin-x64/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/binding-freebsd-x64](https://www.npmjs.com/package/@rolldown/binding-freebsd-x64/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/binding-linux-arm-gnueabihf](https://www.npmjs.com/package/@rolldown/binding-linux-arm-gnueabihf/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/binding-linux-arm64-gnu](https://www.npmjs.com/package/@rolldown/binding-linux-arm64-gnu/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/binding-linux-arm64-musl](https://www.npmjs.com/package/@rolldown/binding-linux-arm64-musl/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/binding-linux-ppc64-gnu](https://www.npmjs.com/package/@rolldown/binding-linux-ppc64-gnu/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/binding-linux-s390x-gnu](https://www.npmjs.com/package/@rolldown/binding-linux-s390x-gnu/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/binding-linux-x64-gnu](https://www.npmjs.com/package/@rolldown/binding-linux-x64-gnu/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/binding-linux-x64-musl](https://www.npmjs.com/package/@rolldown/binding-linux-x64-musl/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/binding-openharmony-arm64](https://www.npmjs.com/package/@rolldown/binding-openharmony-arm64/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/binding-win32-arm64-msvc](https://www.npmjs.com/package/@rolldown/binding-win32-arm64-msvc/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/binding-win32-x64-msvc](https://www.npmjs.com/package/@rolldown/binding-win32-x64-msvc/v/1.2.13) | 1.2.13 | MIT |
| [@rolldown/pluginutils](https://www.npmjs.com/package/@rolldown/pluginutils/v/1.0.1) | 1.0.1 | MIT |
| [@types/estree](https://www.npmjs.com/package/@types/estree/v/1.0.9) | 1.0.9 | MIT |
| [@vitejs/plugin-vue](https://www.npmjs.com/package/@vitejs/plugin-vue/v/6.0.9) | 6.0.9 | MIT |
| [@vue-macros/common](https://www.npmjs.com/package/@vue-macros/common/v/3.1.4) | 3.1.4 | MIT |
| [@vue/compiler-core](https://www.npmjs.com/package/@vue/compiler-core/v/3.5.43) | 3.5.43 | MIT |
| [@vue/compiler-dom](https://www.npmjs.com/package/@vue/compiler-dom/v/3.5.43) | 3.5.43 | MIT |
| [@vue/compiler-sfc](https://www.npmjs.com/package/@vue/compiler-sfc/v/3.5.43) | 3.5.43 | MIT |
| [@vue/compiler-ssr](https://www.npmjs.com/package/@vue/compiler-ssr/v/3.5.43) | 3.5.43 | MIT |
| [@vue/devtools-api](https://www.npmjs.com/package/@vue/devtools-api/v/8.2.1) | 8.2.1 | MIT |
| [@vue/devtools-kit](https://www.npmjs.com/package/@vue/devtools-kit/v/8.2.1) | 8.2.1 | MIT |
| [@vue/devtools-shared](https://www.npmjs.com/package/@vue/devtools-shared/v/8.2.1) | 8.2.1 | MIT |
| [@vue/reactivity](https://www.npmjs.com/package/@vue/reactivity/v/3.5.43) | 3.5.43 | MIT |
| [@vue/runtime-core](https://www.npmjs.com/package/@vue/runtime-core/v/3.5.43) | 3.5.43 | MIT |
| [@vue/runtime-dom](https://www.npmjs.com/package/@vue/runtime-dom/v/3.5.43) | 3.5.43 | MIT |
| [@vue/server-renderer](https://www.npmjs.com/package/@vue/server-renderer/v/3.5.43) | 3.5.43 | MIT |
| [@vue/shared](https://www.npmjs.com/package/@vue/shared/v/3.5.43) | 3.5.43 | MIT |
| [abstract-logging](https://www.npmjs.com/package/abstract-logging/v/2.0.1) | 2.0.1 | MIT |
| [acorn](https://www.npmjs.com/package/acorn/v/8.19.0) | 8.19.0 | MIT |
| [ajv](https://www.npmjs.com/package/ajv/v/8.20.0) | 8.20.0 | MIT |
| [ajv-formats](https://www.npmjs.com/package/ajv-formats/v/3.0.1) | 3.0.1 | MIT |
| [fast-uri](https://www.npmjs.com/package/fast-uri/v/3.1.8) | 3.1.8 | BSD-3-Clause |
| [ast-kit](https://www.npmjs.com/package/ast-kit/v/2.2.0) | 2.2.0 | MIT |
| [ast-walker-scope](https://www.npmjs.com/package/ast-walker-scope/v/0.10.0) | 0.10.0 | MIT |
| [@babel/helper-string-parser](https://www.npmjs.com/package/@babel/helper-string-parser/v/8.0.6) | 8.0.6 | MIT |
| [@babel/helper-validator-identifier](https://www.npmjs.com/package/@babel/helper-validator-identifier/v/8.0.6) | 8.0.6 | MIT |
| [@babel/parser](https://www.npmjs.com/package/@babel/parser/v/8.0.7) | 8.0.7 | MIT |
| [@babel/types](https://www.npmjs.com/package/@babel/types/v/8.0.6) | 8.0.6 | MIT |
| [ast-kit](https://www.npmjs.com/package/ast-kit/v/3.0.0) | 3.0.0 | MIT |
| [estree-walker](https://www.npmjs.com/package/estree-walker/v/3.0.3) | 3.0.3 | MIT |
| [atomic-sleep](https://www.npmjs.com/package/atomic-sleep/v/1.0.0) | 1.0.0 | MIT |
| [avvio](https://www.npmjs.com/package/avvio/v/9.3.1) | 9.3.1 | MIT |
| [birpc](https://www.npmjs.com/package/birpc/v/2.9.0) | 2.9.0 | MIT |
| [chokidar](https://www.npmjs.com/package/chokidar/v/5.0.0) | 5.0.0 | MIT |
| [confbox](https://www.npmjs.com/package/confbox/v/0.3.1) | 0.3.1 | MIT |
| [cookie](https://www.npmjs.com/package/cookie/v/2.0.1) | 2.0.1 | MIT |
| [csstype](https://www.npmjs.com/package/csstype/v/3.2.3) | 3.2.3 | MIT |
| [dequal](https://www.npmjs.com/package/dequal/v/2.0.3) | 2.0.3 | MIT |
| [detect-libc](https://www.npmjs.com/package/detect-libc/v/2.1.2) | 2.1.2 | Apache-2.0 |
| [entities](https://www.npmjs.com/package/entities/v/7.0.1) | 7.0.1 | BSD-2-Clause |
| [estree-walker](https://www.npmjs.com/package/estree-walker/v/2.0.2) | 2.0.2 | MIT |
| [exsolve](https://www.npmjs.com/package/exsolve/v/1.1.3) | 1.1.3 | MIT |
| [fast-decode-uri-component](https://www.npmjs.com/package/fast-decode-uri-component/v/1.0.1) | 1.0.1 | MIT |
| [fast-deep-equal](https://www.npmjs.com/package/fast-deep-equal/v/3.1.3) | 3.1.3 | MIT |
| [fast-json-stringify](https://www.npmjs.com/package/fast-json-stringify/v/7.1.0) | 7.1.0 | MIT |
| [fast-querystring](https://www.npmjs.com/package/fast-querystring/v/1.1.2) | 1.1.2 | MIT |
| [fast-uri](https://www.npmjs.com/package/fast-uri/v/4.2.2) | 4.2.2 | BSD-3-Clause |
| [fastify](https://www.npmjs.com/package/fastify/v/5.12.5) | 5.12.5 | MIT |
| [fastq](https://www.npmjs.com/package/fastq/v/1.20.3) | 1.20.3 | ISC |
| [fdir](https://www.npmjs.com/package/fdir/v/6.5.0) | 6.5.0 | MIT |
| [find-my-way](https://www.npmjs.com/package/find-my-way/v/9.10.0) | 9.10.0 | MIT |
| [fsevents](https://www.npmjs.com/package/fsevents/v/2.3.3) | 2.3.3 | MIT |
| [hookable](https://www.npmjs.com/package/hookable/v/5.5.3) | 5.5.3 | MIT |
| [ipaddr.js](https://www.npmjs.com/package/ipaddr.js/v/2.5.0) | 2.5.0 | MIT |
| [json-schema-ref-resolver](https://www.npmjs.com/package/json-schema-ref-resolver/v/3.0.1) | 3.0.1 | MIT |
| [json-schema-traverse](https://www.npmjs.com/package/json-schema-traverse/v/1.0.0) | 1.0.0 | MIT |
| [light-my-request](https://www.npmjs.com/package/light-my-request/v/6.8.0) | 6.8.0 | BSD-3-Clause |
| [lightningcss](https://www.npmjs.com/package/lightningcss/v/1.33.0) | 1.33.0 | MPL-2.0 |
| [lightningcss-android-arm64](https://www.npmjs.com/package/lightningcss-android-arm64/v/1.33.0) | 1.33.0 | MPL-2.0 |
| [lightningcss-darwin-arm64](https://www.npmjs.com/package/lightningcss-darwin-arm64/v/1.33.0) | 1.33.0 | MPL-2.0 |
| [lightningcss-darwin-x64](https://www.npmjs.com/package/lightningcss-darwin-x64/v/1.33.0) | 1.33.0 | MPL-2.0 |
| [lightningcss-freebsd-x64](https://www.npmjs.com/package/lightningcss-freebsd-x64/v/1.33.0) | 1.33.0 | MPL-2.0 |
| [lightningcss-linux-arm-gnueabihf](https://www.npmjs.com/package/lightningcss-linux-arm-gnueabihf/v/1.33.0) | 1.33.0 | MPL-2.0 |
| [lightningcss-linux-arm64-gnu](https://www.npmjs.com/package/lightningcss-linux-arm64-gnu/v/1.33.0) | 1.33.0 | MPL-2.0 |
| [lightningcss-linux-arm64-musl](https://www.npmjs.com/package/lightningcss-linux-arm64-musl/v/1.33.0) | 1.33.0 | MPL-2.0 |
| [lightningcss-linux-x64-gnu](https://www.npmjs.com/package/lightningcss-linux-x64-gnu/v/1.33.0) | 1.33.0 | MPL-2.0 |
| [lightningcss-linux-x64-musl](https://www.npmjs.com/package/lightningcss-linux-x64-musl/v/1.33.0) | 1.33.0 | MPL-2.0 |
| [lightningcss-win32-arm64-msvc](https://www.npmjs.com/package/lightningcss-win32-arm64-msvc/v/1.33.0) | 1.33.0 | MPL-2.0 |
| [lightningcss-win32-x64-msvc](https://www.npmjs.com/package/lightningcss-win32-x64-msvc/v/1.33.0) | 1.33.0 | MPL-2.0 |
| [linq](https://www.npmjs.com/package/linq/v/4.0.3) | 4.0.3 | MIT |
| [local-pkg](https://www.npmjs.com/package/local-pkg/v/1.2.1) | 1.2.1 | MIT |
| [magic-string](https://www.npmjs.com/package/magic-string/v/0.30.21) | 0.30.21 | MIT |
| [magic-string-ast](https://www.npmjs.com/package/magic-string-ast/v/1.0.3) | 1.0.3 | MIT |
| [mlly](https://www.npmjs.com/package/mlly/v/1.8.2) | 1.8.2 | MIT |
| [confbox](https://www.npmjs.com/package/confbox/v/0.1.8) | 0.1.8 | MIT |
| [pkg-types](https://www.npmjs.com/package/pkg-types/v/1.3.1) | 1.3.1 | MIT |
| [moment](https://www.npmjs.com/package/moment/v/2.31.0) | 2.31.0 | MIT |
| [muggle-string](https://www.npmjs.com/package/muggle-string/v/0.4.1) | 0.4.1 | MIT |
| [nanoid](https://www.npmjs.com/package/nanoid/v/3.3.20) | 3.3.20 | MIT |
| [nostics](https://www.npmjs.com/package/nostics/v/1.3.0) | 1.3.0 | MIT |
| [on-exit-leak-free](https://www.npmjs.com/package/on-exit-leak-free/v/2.1.2) | 2.1.2 | MIT |
| [pathe](https://www.npmjs.com/package/pathe/v/2.0.3) | 2.0.3 | MIT |
| [perfect-debounce](https://www.npmjs.com/package/perfect-debounce/v/2.1.0) | 2.1.0 | MIT |
| [picocolors](https://www.npmjs.com/package/picocolors/v/1.1.1) | 1.1.1 | ISC |
| [picomatch](https://www.npmjs.com/package/picomatch/v/4.0.7) | 4.0.7 | MIT |
| [pino](https://www.npmjs.com/package/pino/v/10.4.0) | 10.4.0 | MIT |
| [pino-abstract-transport](https://www.npmjs.com/package/pino-abstract-transport/v/3.0.0) | 3.0.0 | MIT |
| [pino-std-serializers](https://www.npmjs.com/package/pino-std-serializers/v/7.1.0) | 7.1.0 | MIT |
| [pkg-types](https://www.npmjs.com/package/pkg-types/v/2.3.3) | 2.3.3 | MIT |
| [postcss](https://www.npmjs.com/package/postcss/v/8.5.29) | 8.5.29 | MIT |
| [process-warning](https://www.npmjs.com/package/process-warning/v/5.1.1) | 5.1.1 | MIT |
| [quansync](https://www.npmjs.com/package/quansync/v/0.2.11) | 0.2.11 | MIT |
| [quick-format-unescaped](https://www.npmjs.com/package/quick-format-unescaped/v/4.0.4) | 4.0.4 | MIT |
| [readdirp](https://www.npmjs.com/package/readdirp/v/5.1.1) | 5.1.1 | MIT |
| [real-require](https://www.npmjs.com/package/real-require/v/1.0.0) | 1.0.0 | MIT |
| [require-from-string](https://www.npmjs.com/package/require-from-string/v/2.0.2) | 2.0.2 | MIT |
| [ret](https://www.npmjs.com/package/ret/v/0.5.0) | 0.5.0 | MIT |
| [reusify](https://www.npmjs.com/package/reusify/v/1.1.0) | 1.1.0 | MIT |
| [rfdc](https://www.npmjs.com/package/rfdc/v/1.4.1) | 1.4.1 | MIT |
| [rolldown](https://www.npmjs.com/package/rolldown/v/1.2.13) | 1.2.13 | MIT |
| [safe-regex2](https://www.npmjs.com/package/safe-regex2/v/5.1.2) | 5.1.2 | MIT |
| [safe-stable-stringify](https://www.npmjs.com/package/safe-stable-stringify/v/2.5.0) | 2.5.0 | MIT |
| [scule](https://www.npmjs.com/package/scule/v/1.3.0) | 1.3.0 | MIT |
| [secure-json-parse](https://www.npmjs.com/package/secure-json-parse/v/4.1.1) | 4.1.1 | BSD-3-Clause |
| [semver](https://www.npmjs.com/package/semver/v/7.8.5) | 7.8.5 | ISC |
| [set-cookie-parser](https://www.npmjs.com/package/set-cookie-parser/v/3.1.3) | 3.1.3 | MIT |
| [sonic-boom](https://www.npmjs.com/package/sonic-boom/v/4.2.1) | 4.2.1 | MIT |
| [source-map-js](https://www.npmjs.com/package/source-map-js/v/1.2.2) | 1.2.2 | BSD-3-Clause |
| [split2](https://www.npmjs.com/package/split2/v/4.2.0) | 4.2.0 | ISC |
| [thread-stream](https://www.npmjs.com/package/thread-stream/v/4.2.0) | 4.2.0 | MIT |
| [tinyglobby](https://www.npmjs.com/package/tinyglobby/v/0.2.17) | 0.2.17 | MIT |
| [toad-cache](https://www.npmjs.com/package/toad-cache/v/3.7.4) | 3.7.4 | MIT |
| [ufo](https://www.npmjs.com/package/ufo/v/1.6.4) | 1.6.4 | MIT |
| [unplugin](https://www.npmjs.com/package/unplugin/v/3.4.0) | 3.4.0 | MIT |
| [unplugin-utils](https://www.npmjs.com/package/unplugin-utils/v/0.3.2) | 0.3.2 | MIT |
| [vite](https://www.npmjs.com/package/vite/v/8.3.4) | 8.3.4 | MIT |
| [vue](https://www.npmjs.com/package/vue/v/3.5.43) | 3.5.43 | MIT |
| [vue-router](https://www.npmjs.com/package/vue-router/v/5.4.0) | 5.4.0 | MIT |
| [magic-string](https://www.npmjs.com/package/magic-string/v/1.4.3) | 1.4.3 | MIT |
| [webpack-virtual-modules](https://www.npmjs.com/package/webpack-virtual-modules/v/0.6.2) | 0.6.2 | MIT |

## NuGet lockfiles

Runtime packages come from the service lockfile. Test-only dependencies are included for development completeness.

| Package | Version | Scope | Declared license |
| --- | --- | --- | --- |
| [ClosedXML](https://www.nuget.org/packages/ClosedXML/0.105.1) | 0.105.1 | Runtime | MIT |
| [ClosedXML.Parser](https://www.nuget.org/packages/ClosedXML.Parser/2.0.0) | 2.0.0 | Runtime | MIT |
| [DocumentFormat.OpenXml](https://www.nuget.org/packages/DocumentFormat.OpenXml/3.1.1) | 3.1.1 | Runtime | MIT |
| [DocumentFormat.OpenXml.Framework](https://www.nuget.org/packages/DocumentFormat.OpenXml.Framework/3.1.1) | 3.1.1 | Runtime | MIT |
| [ExcelNumberFormat](https://www.nuget.org/packages/ExcelNumberFormat/1.1.0) | 1.1.0 | Runtime | MIT |
| [Microsoft.AspNetCore.Mvc.Testing](https://www.nuget.org/packages/Microsoft.AspNetCore.Mvc.Testing/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.AspNetCore.TestHost](https://www.nuget.org/packages/Microsoft.AspNetCore.TestHost/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.CodeCoverage](https://www.nuget.org/packages/Microsoft.CodeCoverage/18.8.1) | 18.8.1 | Test | MIT |
| [Microsoft.Extensions.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Configuration/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Configuration.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Abstractions/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Configuration.Binder](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Binder/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Configuration.CommandLine](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.CommandLine/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Configuration.EnvironmentVariables](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.EnvironmentVariables/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Configuration.FileExtensions](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.FileExtensions/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Configuration.Json](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Json/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Configuration.UserSecrets](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.UserSecrets/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.DependencyInjection.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.DependencyModel](https://www.nuget.org/packages/Microsoft.Extensions.DependencyModel/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Diagnostics](https://www.nuget.org/packages/Microsoft.Extensions.Diagnostics/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Diagnostics.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Diagnostics.Abstractions/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.FileProviders.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.FileProviders.Abstractions/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.FileProviders.Physical](https://www.nuget.org/packages/Microsoft.Extensions.FileProviders.Physical/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.FileSystemGlobbing](https://www.nuget.org/packages/Microsoft.Extensions.FileSystemGlobbing/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Hosting.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Hosting.Abstractions/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Logging](https://www.nuget.org/packages/Microsoft.Extensions.Logging/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Logging.Configuration](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Configuration/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Logging.Console](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Console/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Logging.Debug](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Debug/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Logging.EventLog](https://www.nuget.org/packages/Microsoft.Extensions.Logging.EventLog/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Logging.EventSource](https://www.nuget.org/packages/Microsoft.Extensions.Logging.EventSource/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Options](https://www.nuget.org/packages/Microsoft.Extensions.Options/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Options.ConfigurationExtensions](https://www.nuget.org/packages/Microsoft.Extensions.Options.ConfigurationExtensions/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.Extensions.Primitives](https://www.nuget.org/packages/Microsoft.Extensions.Primitives/10.0.10) | 10.0.10 | Test | MIT |
| [Microsoft.NET.Test.Sdk](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.8.1) | 18.8.1 | Test | MIT |
| [Microsoft.TestPlatform.ObjectModel](https://www.nuget.org/packages/Microsoft.TestPlatform.ObjectModel/18.8.1) | 18.8.1 | Test | MIT |
| [Microsoft.TestPlatform.TestHost](https://www.nuget.org/packages/Microsoft.TestPlatform.TestHost/18.8.1) | 18.8.1 | Test | MIT |
| [RBush.Signed](https://www.nuget.org/packages/RBush.Signed/4.0.0) | 4.0.0 | Runtime | MIT |
| [SixLabors.Fonts](https://www.nuget.org/packages/SixLabors.Fonts/1.0.0) | 1.0.0 | Runtime | Apache-2.0 |
| [System.Diagnostics.EventLog](https://www.nuget.org/packages/System.Diagnostics.EventLog/10.0.10) | 10.0.10 | Test | MIT |
| [System.IO.Packaging](https://www.nuget.org/packages/System.IO.Packaging/8.0.1) | 8.0.1 | Runtime | MIT |
| [xunit](https://www.nuget.org/packages/xunit/2.9.3) | 2.9.3 | Test | Apache-2.0 |
| [xunit.abstractions](https://www.nuget.org/packages/xunit.abstractions/2.0.3) | 2.0.3 | Test | https://raw.githubusercontent.com/xunit/xunit/master/license.txt |
| [xunit.analyzers](https://www.nuget.org/packages/xunit.analyzers/1.18.0) | 1.18.0 | Test | Apache-2.0 |
| [xunit.assert](https://www.nuget.org/packages/xunit.assert/2.9.3) | 2.9.3 | Test | Apache-2.0 |
| [xunit.core](https://www.nuget.org/packages/xunit.core/2.9.3) | 2.9.3 | Test | Apache-2.0 |
| [xunit.extensibility.core](https://www.nuget.org/packages/xunit.extensibility.core/2.9.3) | 2.9.3 | Test | Apache-2.0 |
| [xunit.extensibility.execution](https://www.nuget.org/packages/xunit.extensibility.execution/2.9.3) | 2.9.3 | Test | Apache-2.0 |
| [xunit.runner.visualstudio](https://www.nuget.org/packages/xunit.runner.visualstudio/3.1.5) | 3.1.5 | Test | Apache-2.0 |

## Container components

The Docker image also contains Node.js, .NET/ASP.NET, Debian packages, DejaVu fonts, curl and tini.
Their licenses and notices are separate from the engine license. The base-image and system-package versions
are resolved during the image build; this source-lockfile inventory is not an image SBOM.
Review the exact image and retain its upstream notices before distributing a binary release.
