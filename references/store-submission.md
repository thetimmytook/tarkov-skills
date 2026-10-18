# Microsoft Store Submission

ETW permission recovery implemented in the isolated development checkout on
2026-10-17 is not yet a validated Store candidate. See
[ETW permission setup](etw-permission-setup.md) for the account-change scope,
consent, native helper boundary and remaining signed-flight checks. The existing
`runFullTrust` declarations remain unchanged; no `allowElevation` declaration or
new capability submission has been made.

The 2026-10-18 support revision retains bounded local capture diagnostics and
offers a user-opened GitHub issue with a manually attached sanitized support ZIP.
Nothing is uploaded automatically. Keep Store privacy wording aligned with
[PRIVACY.md](../PRIVACY.md); this source change is not a Store publication.

The next candidates are Toolkit `1.1.0.0` and Benchmark `1.1.0.0`, above the
previous `1.0.3.0` / `1.0.6.0` telemetry flights. They include FPS-only PresentMon
tracking, shared inline Settings/access management and sanitized support archives.
The integrated portable AMD capture passed. Before production, validate the new
permission/helper path and support actions in both Microsoft-signed flight hosts;
previous-flight cancellation checks do not validate this new permission flow.
The recorded public baselines below remain unchanged pending actual publication.

Status: both products publicly published

## Published Products

| Product | Store ID | Current public version |
| --- | --- | --- |
| Tarkov Performance Benchmark | `9PJMPQ06JL21` | `1.0.3.0` |
| Tarkov Performance Toolkit | `9N3L7DZH0K64` | `1.0.0.0` |

## Package

- Product: `Tarkov Performance Benchmark`
- Store ID: `9PJMPQ06JL21`
- Identity: `TimmyTook.TarkovPerformanceBenchmark`
- Package: unsigned x64 MSIX
- First package version: `1.0.0.0`
- Current public version: `1.0.3.0`
- Device family: Windows 10/11 Desktop only
- Minimum version: Windows build `19041`
- Do not let Microsoft add future device families automatically.

Microsoft Store signs the package after certification. Do not use local self-signed certificate installation as a release check.

## Pricing And Audience

- Markets: all available markets
- Base price: free (`0`)
- Current production audience: Public
- Discoverability: available in Microsoft Store
- Publishing hold: publish as soon as certification passes

Use a package flight with a known-user group when an update needs closed Store validation before broad release. Production submissions remain public.

## Properties

- Category: Utilities + tools
- Subcategory: none
- Secondary category: none
- Mixed Reality display mode: neither PC nor HoloLens
- Purchases outside Store commerce: no
- Accessibility compliance claim: no, until dedicated accessibility testing is completed
- Alternate drive/removable storage installation: yes
- Automatic OneDrive backup: no
- Game clip recording and broadcast: no
- Pen and ink: no
- Generative AI: no
- Hardware requirements: leave blank

## Age Rating

- Complete a new IARC questionnaire.
- App type: All Other App Types
- Ratings board or physical-media distribution: no
- The utility contains no violence, sexual content, controlled substances, gambling, strong language, purchases, user-generated content, social communication, or unrestricted web access.

## Language And Listing

MVP listing language: English (United States).

Description:

```text
Tarkov Performance Benchmark measures FPS and frame-time consistency during a real Escape from Tarkov raid.

Run a two-minute capture to record Average FPS, 1% Low, 0.1% Low, and P95 frame time. The app also records relevant system information, graphics settings, and raid context to make benchmark results easier to compare.

Benchmark data is stored locally on your PC. Nothing is uploaded automatically.

The app uses bundled PresentMon for user-initiated performance capture. It does not inject code, read game process memory, automate input, modify game files, or interact with anti-cheat systems.

Tarkov Performance Benchmark is an unofficial community tool and is not affiliated with or endorsed by Battlestate Games.
```

Features are entered as separate fields without bullet characters:

```text
Two-minute FPS and frame-time capture
Average FPS, 1% Low, 0.1% Low, and P95 frame time
Automatic raid and map context detection
Local graphics, PostFX, and system information
Local JSON benchmark history
Bundled PresentMon capture engine
No automatic data upload
```

Search terms: `tarkov`, `benchmark`, `fps`, `frametime`, `performance`, `stutter`.

Copyright: `© 2026 TimmyTook`.

At least one desktop PNG screenshot is required at `1366 x 768` or larger. Keep the application visible in the top two-thirds and do not add marketing overlays.

## Privacy Policy Text

For the next auth-enabled submission, use the wording below and the shared
`PRIVACY.md`. Optional Clerk authentication and explicit Academy publication change
the previous local-only privacy description. This copy has not yet been submitted or
certified for an auth-enabled release.

```text
Privacy Policy for Tarkov Performance Benchmark

Effective date: set when the auth-enabled release is published

Tarkov Performance Benchmark processes performance and diagnostic information locally on the user's device. This may include FPS and frame-time metrics, Windows hardware and driver information, Escape from Tarkov graphics settings, game version, map, raid environment, weather, and time-of-day context.

Updated capture builds also retain summary CPU and logical-processor utilization, whole-adapter graphics utilization and dedicated/shared GPU memory, physical RAM, pagefile allocation/use and system commit. Resource samples are transient; raw time series, process IDs, other application names, paths, drive letters, LUID/PCI/PnP/device identifiers and native error text are not included in saved, exported or submitted summaries. Copied completed runs include this summary. After explicit Send for review, updated builds send the selected run's allowlisted summary to Timmy Academy. CPU and memory describe the whole system; GPU measurements describe the whole selected adapter, including other applications without identifying them. It becomes public only after moderator approval. Signing in does not upload results. Deleting a publication removes telemetry; a smaller measurement without dates, source IDs or the user's account link remains in a closed internal analysis archive and does not retain telemetry. Authentication and ownership information remain separate from the public benchmark DTO.

Generated benchmark and diagnostic reports do not contain names, email addresses, account identifiers, IP addresses, device serial numbers, machine identifiers, or precise location data. The application does not read game process memory, automate input, or modify game files.

Benchmark results are stored locally in the application's private Microsoft Store data folder. No benchmark or diagnostic data is uploaded automatically. Users can open the storage folder from the application and delete the benchmark JSON file, or remove all package-local data by uninstalling the application.

The application uses the bundled open-source PresentMon utility to perform user-initiated FPS and frame-time capture. PresentMon runs locally on the device.

Optional account sign-in opens Clerk authentication in the system browser. Clerk handles email verification and consent and receives information entered there and normal connection information. Benchmark and Toolkit share access and refresh credentials encrypted for the current Windows user and Clerk environment, separately from benchmark reports. Store builds use shared publisher storage, which Windows retains until the last app from that publisher is uninstalled; portable builds use a separate shared user-local folder retained until sign-out or removal. It contacts Clerk to refresh expiring credentials while signed in. Sign out in either app requests revocation for both; if that cannot be confirmed, both apps disable access, retain the encrypted credential for retry, and report incomplete sign-out. Browser and desktop sign-out are independent. The application does not store the user's email or profile, include credentials in reports or logs, or publish results merely because the user signs in. Clerk's privacy terms apply to authentication.

Tarkov Performance Benchmark is an unofficial community tool and is not affiliated with or endorsed by Battlestate Games.

For privacy questions or support:
https://github.com/thetimmytook/tarkov-skills/issues
```

## Restricted Capability

Use this justification for `runFullTrust`:

```text
Tarkov Performance Benchmark is a packaged WPF desktop application that requires full-trust access to perform user-initiated performance measurements.

The capability is used to read Escape from Tarkov graphics settings and log files from their standard user directories, query non-identifying Windows hardware and driver information, launch the bundled PresentMon executable, perform ETW-based FPS and frame-time capture, and save benchmark results locally as JSON.

All capture activity is explicitly started by the user. The application does not inject code, read game process memory, automate input, modify game files, install a service, or interact with anti-cheat components. It runs without elevation by default and does not upload data automatically.
```

## Store Release Check

Execute and record the applicable manual cases in [`store-release-test-cases.md`](store-release-test-cases.md) before each production update. Use the full checklist for changes to capture, storage, packaging, permissions, or execution aliases.

After certification and publishing:

1. Install or update from Microsoft Store.
2. Verify Start menu launch and single-instance behavior.
3. Verify the `tarkov-benchmark.exe` execution alias.
4. Complete a real two-minute PresentMon capture.
5. Verify cancellation discards the incomplete run.
6. Verify metrics and context are saved without user-specific paths.
7. Verify benchmark history is stored in package `LocalState`, survives a Store update, and is exposed to skills only through machine-readable execution-alias output.
8. Verify no data is uploaded without explicit consent.

## GitHub Deployment Pipeline

Store package versions and release tags are independent per product. The version files under each product's `packaging\store-release.json` are the source of truth and must be updated in the release PR before tagging `main`. `publishedPackageVersion` records the current public baseline; `packageVersion` must be higher, every component must be at most 65535, the first component must be nonzero, and the fourth component must remain zero:

| Product | Tag form | Next approved tag | Next package version |
| --- | --- | --- | --- |
| Tarkov Performance Benchmark | `benchmark-vX.Y.Z` | `benchmark-v1.1.0` | `1.1.0.0` |
| Tarkov Performance Toolkit | `toolkit-vX.Y.Z` | `toolkit-v1.1.0` | `1.1.0.0` |

Partner Center package screenshots supplied on 2026-10-09 confirmed uploaded
Benchmark `1.0.5.0` and Toolkit `1.0.2.0`; the user subsequently installed the
`1.0.6.0` / `1.0.3.0` telemetry flights from Store. The capture/access/diagnostics
update therefore uses the higher candidates above. Keep `publishedPackageVersion`
at its recorded public baseline until public publication is confirmed separately.

Do not reuse an x64 package version for changed binaries after that version has been accepted into a flight; raise the candidate version before another flight build. Public promotion is the intentional exception: it reuses the exact flight-tested package bytes and version rather than creating another build. After a public release, update `publishedPackageVersion` and choose the next higher candidate in the next release PR.

### One build, explicit promotion

Every push to `main` runs `Benchmark app` and `Performance toolkit`, each calling
`store-build.yml`. Each product's executable is published once with Production
auth/API configuration (Toolkit has two executables). `build-store-artifacts.ps1`
passes the same publish directory to the MSIX and portable packagers. MakeAppx
semantic validation, release-version checks, application tests, PresentMon checksum
verification, and unsigned package-content inspection run in this build. No later
stage runs `dotnet publish`, MakeAppx packing, or portable compression.

The build uploads one `release-<product>-<run ID>-a<attempt>` bundle containing:

- the unsigned x64 MSIX;
- the self-contained Windows x64 portable ZIP;
- `release-manifest.json` with schema version, product, package version, repository,
  source SHA, workflow path, event, branch, build run ID/attempt, artifact name,
  filenames, byte sizes and SHA-256 for both files.

The artifact ID is returned by GitHub after upload and recorded in the job summary.
The manifest uses the immutable artifact name because its own upload ID does not
exist when the manifest is created. Downstream stages resolve that exact name to
one ID through the API and download by ID. All pipeline bundles are retained for
90 days; repository/organization retention settings must permit at least 90 days.
PR and manually dispatched diagnostic builds use `check-...` names and set
`eligible: false`; they cannot be Store or application-release sources.

### Closed preflight

Dispatch `Microsoft Store flight` on `main`, select `benchmark`, `toolkit`, or
`both`, and supply each selected product's `*_build_run_id` and
`*_build_attempt`. These are separate product workflows, so `both` requires two
explicit build sources. Confirm production API readiness as before.

The shared validator requires a completed successful **selected attempt**, the
registered product workflow ID and path, a `push` event from this repository
(forks are rejected), `main`, and a source SHA equal to both the dispatch SHA and
current remote `main`. It validates the artifact's repository/run/commit metadata,
manifest identity/version/origin and both file checksums. It also repeats PresentMon,
Production configuration, release-version and MSIX-content checks. Missing, deleted,
expired, ambiguous or mismatched artifacts fail with an error; no rebuild fallback
or implicit search for latest is permitted.

`store-submit.yml` validates before any Store credentials are available, then
repeats download and validation after approval in `microsoft-store-flight`. Store
mutations remain serialized per product. It sends only the existing MSIX to the
configured private flight. **After a successful publisher command**, it creates
`flight-receipt.json` and uploads `store-flight-<product>-<flight run ID>-a<attempt>`
with the unchanged build bundle. The receipt records the original build artifact
ID/name and run/attempt, source commit/version/product, manifest and MSIX checksums,
and the submitting flight workflow/run/attempt/artifact name. A staged package
without a successful submission receipt is not a production source. Status is read
once; certification is not polled. A successful workflow means submission succeeded;
Microsoft certification and signed-package manual testing must still complete.

### Production and GitHub Releases

After certification and the applicable signed-package checks, dispatch
`Microsoft Store production promotion` on `main` with `flight_run_id`,
`flight_attempt`, selected product(s), and the explicit public-release confirmation.
The validator requires a successful manual flight attempt from the registered
`store-flight.yml` workflow at the **same current main commit**. It downloads its
submitted bundle, verifies the receipt and checks the original build attempt and
artifact ID again. Both original build and flight artifacts must still be available.
The protected `microsoft-store-production` job repeats validation after approval
and sends the identical MSIX bytes without a flight ID. Only metadata sidecars and
GitHub artifact transport containers are created; the MSIX is never repacked.

For an application GitHub Release, first arrange the existing approved
`benchmark-vX.Y.Z` or `toolkit-vX.Y.Z` tag at current `main` through the normal
manual tagging process, then dispatch `Product release` from `main` with the product,
`flight_run_id` and `flight_attempt`. The tag and version file must match. This workflow
downloads and validates that submitted flight bundle and its original build, attaches
the unchanged MSIX, ZIP, manifest and flight receipt, creates a source archive, and
writes `SHA256SUMS.txt`. App tag pushes
alone no longer build or publish. The workflow never creates or moves application
tags and never submits to Store. The receipt binds the release to the tested build,
including its attempt; no source is inferred from a tag or latest run.
Portable builds remain unsigned by Microsoft Store, do not auto-update, and do not
register Store execution aliases.

Skills publication stays independent: a plugin version change on `main` still
validates and publishes all four skills together with the immutable `skills-v<version>`
tag. Existing skills-tag handling and the skills/source archives are unchanged and
do not depend on any Store build or flight.

### Attempts and transition

Always record both run ID and attempt. Artifact names include the attempt, so a
rerun cannot overwrite an earlier bundle. The API checks the specified historical
attempt, not the latest attempt's status. An explicitly selected earlier successful
attempt remains eligible if its artifacts and commit still pass every check. A
failed-jobs-only rerun that reuses an earlier build/validation job does not create
new build artifacts: selecting its newer attempt fails rather than silently using
old output. To create a new complete build set, rerun all build jobs deliberately.
A rerun of the protected submission job downloads the explicit original source
again and writes a new receipt/artifact for the submitting attempt; it can repeat
an external submission, so inspect Partner Center before rerunning a submission.
When `both` is selected, each product must have its own submitted artifact in the
chosen successful flight attempt. Never mix attempts implicitly.

Do not merge this pipeline migration until the in-progress legacy flight
`37982351957` at `d71d5c5bd333b8f71979c4818320de05deb0fab4` (Benchmark `1.0.6.0`,
Toolkit `1.0.3.0`) has completed its existing production promotion. Its artifacts
lack the new manifest/receipt contract and intentionally are not accepted by the
new validator. Keep its commit, candidate versions, and old workflows intact until
that release finishes. This migration does not bump those versions or start another
flight. After the migration, changed binaries for an already accepted version still
require a deliberate higher candidate version before any new flight submission.

Local automated checks: `node --test build/tests/release-artifacts.test.mjs` exercises
validators with injected GitHub responses and byte-preserving stage transfers;
`build/test-store-artifact-formats.ps1` compares the actual MSIX/ZIP payloads. CI also
parses all PowerShell and checks workflow syntax with pinned actionlint `v1.7.7`.

Creating a Microsoft Entra tenant does not associate it with the Windows developer account. After creation, return to Partner Center **Account settings > Tenants > Developer**, select **Associate Microsoft Entra ID**, sign in with that tenant's Global Administrator, and confirm that the tenant appears under **Current tenant associations**. Only then does **User management > Microsoft Entra applications** become available. Add the CI app registration there and grant only the `Manager (Windows)` role required by Microsoft Store Developer CLI. Record the tenant ID, application client ID, one-time client-secret value, and the Seller ID from **Legal info > Developer > Publisher IDs**; do not confuse Seller ID with Windows publisher ID.

Create both GitHub Environments with required reviewers. Store these secrets separately in each Environment: `AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`, `AZURE_AD_APPLICATION_SECRET`, and `SELLER_ID`. Store `BENCHMARK_STORE_FLIGHT_ID` and `TOOLKIT_STORE_FLIGHT_ID` as variables on `microsoft-store-flight`. The personal Store test account is unrelated to these CI credentials: add its Microsoft-account email to a known-user group and attach that group to one package flight per product.

Before approving a flight submission, the Academy owner must confirm that `https://timmy.academy/api/bench/v1` implements the deployed desktop contracts: Clerk authorization/token/revocation, authenticated `GET /me/runs/by-client-id/{clientRunId}`, authenticated `POST /me/runs`, anonymous grouped run browsing, and cohort comparison. Local API evidence does not satisfy this gate. Never put Clerk tokens, Partner Center secrets, the personal test email, or API response bodies in workflow configuration or logs.
