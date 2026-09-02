# Microsoft Store Submission

Status: first private submission in certification

## Package

- Product: `Tarkov Performance Benchmark`
- Store ID: `9PJMPQ06JL21`
- Identity: `TimmyTook.TarkovPerformanceBenchmark`
- Package: unsigned x64 MSIX
- First package version: `1.0.0.0`
- Device family: Windows 10/11 Desktop only
- Minimum version: Windows build `19041`
- Do not let Microsoft add future device families automatically.

Microsoft Store signs the package after certification. Do not use local self-signed certificate installation as a release check.

## Pricing And Audience

- Markets: all available markets
- Base price: free (`0`)
- First submission audience: Private audience
- Testers: known user group containing personal Microsoft accounts
- Discoverability: restricted to the private audience
- Publishing hold: publish as soon as certification passes

After the private release passes testing, create a new submission and change the audience to Public. A product submitted to a public audience cannot later be changed back to private.

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

```text
Privacy Policy for Tarkov Performance Benchmark

Effective date: August 31, 2026

Tarkov Performance Benchmark processes performance and diagnostic information locally on the user's device. This may include FPS and frame-time metrics, Windows hardware and driver information, Escape from Tarkov graphics settings, game version, map, raid environment, weather, and time-of-day context.

The application does not collect names, email addresses, account identifiers, IP addresses, device serial numbers, machine identifiers, or precise location data. It does not read game process memory, automate input, or modify game files.

Benchmark results are stored locally in the application's private Microsoft Store data folder. No benchmark or diagnostic data is uploaded automatically. Users can open the storage folder from the application and delete the benchmark JSON file, or remove all package-local data by uninstalling the application.

The application uses the bundled open-source PresentMon utility to perform user-initiated FPS and frame-time capture. PresentMon runs locally on the device.

The application does not sell, share, or transmit personal information to the developer or third parties.

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

## Private Release Check

Execute and record the manual cases in [`store-release-test-cases.md`](store-release-test-cases.md) before moving the product to a public audience.

After certification and private publishing:

1. Install from Microsoft Store using an account in the private audience.
2. Verify Start menu launch and single-instance behavior.
3. Verify the `tarkov-benchmark.exe` execution alias.
4. Complete a real two-minute PresentMon capture.
5. Verify cancellation discards the incomplete run.
6. Verify metrics and context are saved without user-specific paths.
7. Verify benchmark history is stored in package `LocalState`, survives a Store update, and is exposed to skills only through machine-readable execution-alias output.
8. Verify no data is uploaded without explicit consent.
