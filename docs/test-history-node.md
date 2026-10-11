# Node regression history and failure boundaries

`npm test` runs 72 tests without Docker or external accounts. The original 23 tests were expanded with 41 historical/edge cases, three subsequent static-asset regressions and five public-example checks. Fixtures use temporary synthetic Vue packs, actual worker threads, compiled production SSR bundles and loopback HTTP servers. The API tests use a controlled private renderer; they do not replace the real .NET or Docker suites.

## Regression coverage

| Failure case | Regression and observable assertion |
| --- | --- |
| Moment imported twice through CJS/ESM paths, so Ukrainian locale registration affected the wrong instance | `pack-history.test.js`: setup imports only `moment/locale/uk`, a separate helper imports bare `moment`, rendered January must be `січень`; no explicit `moment.locale()` hides the failure |
| Vue swallowed production component failures and emitted incomplete XML | Existing synchronous failure tests plus async setup, rejected setup-module import, render-expression and falsy thrown-value tests require safe 422 errors |
| Asset regex matched examples inside strings, comments and cells | Existing text-example regression remains; AST extraction additionally distinguishes real JSX/TSX expressions and ignores their text content |
| BOM manifest failed JSON parsing | Existing compiled-pack test and malformed BOM-prefixed JSON rejection exercise both acceptance and failure paths |
| HTTP connection timeout reset sockets before the promised 504 | Existing live HTTP CPU-loop test plus a new live HTTP test with a stalled private renderer response require a structured 504 body |

## Bugs exposed and fixed by this expansion

1. **TypeScript interpreted as JSX.** `sourceAssets` enabled both Babel plugins for all sources. A valid `const label = <string>'TS'` module containing `import.meta.url` failed compilation. Parser plugins now follow the file extension or Vue script language. Tests compile both `.ts` helpers and Vue `lang="ts"`, and retain JSX/TSX parsing coverage.
2. **URL syntax interpreted as filesystem syntax.** A literal `./assets/a%20b.txt?version=1#part` looked for a file literally containing `%20`, `?` and `#`. Asset resolution now applies standard URL decoding, validates the decoded file against pack containment, and preserves search/hash metadata. Encoded traversal is still rejected with incomplete cache cleanup.
3. **Async Vue failures still swallowed.** `throwUnhandledErrorInProduction` alone did not reject a component that threw after `await`. A production SSR regression first returned partial workbook markup. The generated entry now captures Vue error-handler invocations and rejects after SSR, including falsy exceptions such as `throw undefined`.
4. **Failed readiness retained upstream response bodies.** Non-2xx health responses threw before cancellation, leaving an unfinished body/socket alive until the fetch deadline. Readiness now cancels the body before checking status. A regression observes actual response closure promptly after a 503 readiness result.

A subsequent independent black-box audit found that an equivalent static backtick URL failed while a quoted URL worked. The asset extractor now recognizes substitution-free, untagged template literals and uses their cooked value. Three permanent tests cover mounted assets in Vue and TypeScript helpers, escaped characters, and exclusion of dynamic/tagged forms. All three failed before the correction.

## Additional edge coverage

- JS/MJS/TS helper assets, Vue normal-plus-setup script offsets, contained asset/template symlinks and escaped manifest symlinks.
- Recompilation after source changes, independent compiled caches, idempotent disposal, malformed JSON, wrong entry-file types, and incomplete build cleanup.
- Setup state resets across different template IDs and repeated requests. Each successful render terminates its worker, including outstanding timers that could otherwise write files later.
- Broken workers: syntax errors, missing exports/imports, import-time rejection, clean/error process exit and uncloneable output. A healthy request still succeeds afterward.
- Privacy: an isolated child process captures stdout and stderr while templates log and throw a request-data canary. Neither output may contain the canary.
- Renderer unavailability, non-2xx readiness, unfinished upstream bodies, pre-header and partial-body render deadlines, total SSR-plus-renderer deadline, and render-slot recovery after several failure classes.
- Exact 8 MiB XML boundary and oversized multibyte output; no oversized XML reaches the private renderer. Primitive/null/array data retains its JSON meaning. Invalid media types and scalar request envelopes fail before rendering.

## Running and interpreting results

```sh
npm test
node --test test/asset-regressions.test.js test/pack-history.test.js
node --test test/api-failure-history.test.js test/worker-history.test.js
```

Negative compilation fixtures deliberately produce Vite `Build failed` diagnostics; their Node assertions must pass. The local full run passed **67/67**, with zero skips. HTTP timeout assertions verify status/recovery rather than a brittle exact elapsed duration. Tests clean up temporary files, worker processes and loopback sockets.

A Node fetch timeout cancels transport; it cannot forcibly interrupt synchronous ClosedXML computation already running in .NET. That renderer lifecycle limitation remains separate from the worker termination guarantees tested here.
