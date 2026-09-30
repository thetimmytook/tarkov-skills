# Microsoft Store Submission

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
| Tarkov Performance Benchmark | `benchmark-vX.Y.Z` | `benchmark-v1.0.5` | `1.0.5.0` |
| Tarkov Performance Toolkit | `toolkit-vX.Y.Z` | `toolkit-v1.0.2` | `1.0.2.0` |

Do not reuse an x64 package version for changed binaries after that version has been accepted into a flight; raise the candidate version before another flight build. Public promotion is the intentional exception: it reuses the exact flight-tested package bytes and version rather than creating another build. After a public release, update `publishedPackageVersion` and choose the next higher candidate in the next release PR.

The tag workflow refuses a tag that does not match its version file or does not point to the current `main` commit. It tests only the tagged product, verifies PresentMon, builds and inspects an unsigned MSIX, creates the matching portable ZIP and GitHub Release, and publishes SHA-256 checksums. A tag never submits a package to Microsoft Store.

`Microsoft Store flight` is a manual workflow with a `benchmark`, `toolkit`, or `both` choice. It requires an explicit production-API readiness confirmation, runs the applicable Release tests, verifies PresentMon, builds with the versioned Production `desktop-auth.json` and `academy-api.json`, creates the unsigned MSIX with MakeAppx, and inspects the package before any Store credential is available. Its submission jobs use the protected `microsoft-store-flight` Environment and send each validated package only to its configured private package flight. Store mutations are serialized per product. The workflow reads status once after submission and does not poll certification.

`Microsoft Store production promotion` is a separate manual workflow. It requires the successful flight workflow run ID and an explicit public-release confirmation. Both runs must be dispatched from `main`; the workflow refuses a source run from another workflow, a failed run, or a different commit. It downloads the immutable flight artifact, repeats package inspection, and stages the same bytes for an approval-protected `microsoft-store-production` job. Production promotion does not rebuild the MSIX and does not use a flight ID.

Creating a Microsoft Entra tenant does not associate it with the Windows developer account. After creation, return to Partner Center **Account settings > Tenants > Developer**, select **Associate Microsoft Entra ID**, sign in with that tenant's Global Administrator, and confirm that the tenant appears under **Current tenant associations**. Only then does **User management > Microsoft Entra applications** become available. Add the CI app registration there and grant only the `Manager (Windows)` role required by Microsoft Store Developer CLI. Record the tenant ID, application client ID, one-time client-secret value, and the Seller ID from **Legal info > Developer > Publisher IDs**; do not confuse Seller ID with Windows publisher ID.

Create both GitHub Environments with required reviewers. Store these secrets separately in each Environment: `AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`, `AZURE_AD_APPLICATION_SECRET`, and `SELLER_ID`. Store `BENCHMARK_STORE_FLIGHT_ID` and `TOOLKIT_STORE_FLIGHT_ID` as variables on `microsoft-store-flight`. The personal Store test account is unrelated to these CI credentials: add its Microsoft-account email to a known-user group and attach that group to one package flight per product.

Before approving a flight submission, the Academy owner must confirm that `https://timmy.academy/api/bench/v1` implements the deployed desktop contracts: Clerk authorization/token/revocation, authenticated `GET /me/runs/by-client-id/{clientRunId}`, authenticated `POST /me/runs`, anonymous grouped run browsing, and cohort comparison. Local API evidence does not satisfy this gate. Never put Clerk tokens, Partner Center secrets, the personal test email, or API response bodies in workflow configuration or logs.
