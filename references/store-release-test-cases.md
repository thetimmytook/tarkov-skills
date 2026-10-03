# Microsoft Store Release Test Cases

Use this checklist to validate Microsoft-signed Store releases and updates. Record `Pass`, `Fail`, or `Blocked` for every applicable case and attach a short note for failures.

## Preconditions

- Windows 10 build 19041 or later, or Windows 11.
- Microsoft Store is signed in with a personal Microsoft account. For a closed package flight, the account belongs to its known-user group.
- The Microsoft-signed Store package is installed.
- Escape from Tarkov is available for the real-raid capture cases.
- Record the initial run count shown by the application and, when available, the number of entries in the package-local `benchmark.json` opened through `Open folder`.
- The Academy production owner has confirmed the deployed desktop-auth, owner lookup, submission, public browsing, and cohort-comparison contracts before the flight submission is approved.

## Store Installation And Launch

| ID | Test | Expected result |
| --- | --- | --- |
| STORE-01 | Acquire the app from its public Store listing or assigned closed package flight. | The authorized account can see and install the app without trusting a local certificate. |
| STORE-02 | Launch the app from the Start menu. | The app opens without UAC, certificate, PowerShell, or console prompts. |
| STORE-03 | Verify the installed package identity, publisher signature, version, and architecture. | Identity is `TimmyTook.TarkovPerformanceBenchmark`, the package is Microsoft-signed, architecture is x64, and the version matches the submission. |
| STORE-04 | Launch the app a second time while it is already open. | No second application window is created. |
| STORE-05 | Run `tarkov-benchmark.exe` from a new terminal. | The Store application opens through its execution alias. |
| STORE-06 | Close Benchmark, then run `tarkov-benchmark.exe collect --source skill` in a terminal. Enter a raid, explicitly press Start collection, complete the capture and save its context. Reopen the app normally afterwards. | Capture waits for the user's Start action. After saving, the window closes automatically and stdout contains one JSON summary with `status: completed`, run ID, map, Average FPS, 1% Low, 0.1% Low, P95 frametime, `saved_locally: true`, and `uploaded: false`. The completed run survives reopening. |

## Main Window

| ID | Test | Expected result |
| --- | --- | --- |
| UI-01 | Inspect text, buttons, dropdowns, disabled states, and hover states. | All text is readable against its background and disabled controls are visually distinct. |
| UI-02 | Open About, including on a short display. | Product name, installed version, author `TimmyTook`, PresentMon notice, privacy summary, non-affiliation notice, and working GitHub link are shown. AI-assisted analysis points to Toolkit for the full skills workflow; Set up AI skills opens the repository installation guide. Content scrolls when needed. |
| UI-03 | Inspect the window, taskbar, Start menu, installed-app entry, and Store listing icons. | Original product artwork is shown consistently; the default executable icon is not used. This is required before public release. |
| UI-04 | Inspect collection controls before and during a capture. | `Start collection` is readable and enabled when PresentMon is available. `Cancel and discard` is readable but disabled before capture, then enabled during capture. |
| UI-05 | Inspect the latest-result panel with existing data. | The heading shows `LATEST RESULT · N RUNS` with correct singular/plural text. Average FPS, 1% Low, 0.1% Low, and P95 frametime are visible. `Open folder` and `Submit` are enabled when runs exist. |
| UI-06 | Compare the standalone latest-result actions with Toolkit. | The standalone Benchmark does not show the Toolkit-only `Copy results` action; Open folder and Submit continue to work. |
| UI-07 | Inspect the standalone footer at minimum window size and 150% scaling. | Author and disclaimer/About actions stay in separate columns without overlap. Side padding is 24 DIP, bottom padding is at least 16 DIP, and About is vertically centered rather than stretched. Footer stays outside the scrolling content and clear of the resize grip. |

## Raid Detection And Capture

| ID | Test | Expected result |
| --- | --- | --- |
| CAP-01 | Press `Start collection` while Tarkov is not running. | Capture does not start. The app asks the user to run Tarkov and enter a raid without treating this as a crash. |
| CAP-02 | Press `Start collection` in the Tarkov menu or Hideout. | Capture does not start. The app displays the short raid-required message and does not open the crash-report flow. |
| CAP-03 | Enter a real raid and press `Start collection`. | The raid is detected, bundled PresentMon starts without UAC, progress begins, and cancellation becomes available. |
| CAP-04 | Let the two-minute capture finish. | Progress reaches two minutes, capture stops automatically, a double completion sound plays, and the benchmark-details dialog opens. |
| CAP-05 | Inspect the benchmark-details dialog. | `BSG servers` or `Local` is required. `Save benchmark` remains disabled until all required values are available. Weather and time of day remain optional. |
| CAP-06 | Save a completed capture. | The dialog closes, the latest metrics appear in the main window, and exactly one new run is appended to the JSON file. |
| CAP-07 | Start another capture and press `Cancel and discard`. | Capture stops, partial data is discarded, no details dialog opens, and the JSON run count does not change. |
| CAP-08 | Close the application during capture. | PresentMon is stopped and the incomplete run is not appended. |
| CAP-09 | Restart the application after a successful capture. | The latest saved metrics are restored and `Open folder` remains enabled. |
| CAP-10 | Start capture, then close Tarkov or reproduce a game crash before the two-minute capture completes. | The partial measurement is discarded, no success sound plays, no benchmark-details dialog opens, the JSON run count is unchanged, the main window reports that Tarkov closed, and the app-owned ETW session is removed. |
| CAP-11 | Start capture in a raid, extract after 20-30 seconds, and remain on the first post-raid screen. | The app detects the post-raid profile marker within approximately four seconds, stops capture, discards partial data, plays no success sound, opens no details dialog, leaves the JSON run count unchanged, and removes the app-owned ETW session. |

## Benchmark Data Contract

| ID | Test | Expected result |
| --- | --- | --- |
| DATA-00 | Remove the benchmark application's package-local `LocalState\TarkovSkills` directory, then launch the app. | The app starts normally, latest-result metrics are empty, `Open folder` is disabled, and the missing data directory is not treated as an error. |
| DATA-01 | Complete and save the first benchmark while its package-local data directory does not exist. | The app creates `LocalState\TarkovSkills` and a valid `benchmark.json` containing exactly one completed run. If the file already exists, the run is appended without overwriting earlier runs. |
| DATA-02 | Inspect the new run metrics. | Duration, Average FPS, 1% Low, 0.1% Low, P95 frametime, and sample data are present and plausible. |
| DATA-03 | Inspect run context. | Map is inferred from logs when available; BSG server versus Local matches the user's selection; optional weather and time values are preserved when supplied. |
| DATA-04 | Inspect system and settings data. | Relevant system information and current Graphics, PostFX, and Game settings are stored inside the run. Control and Sound settings are absent. |
| DATA-05 | Search the artifact for private or temporary data. | No user name, host name, IP address, serial number, machine GUID, user-specific path, settings directory, PresentMon CSV path, or raw CSV is present. |
| DATA-06 | Observe the app and network behavior without consenting to upload. | The run remains local and no benchmark data is uploaded automatically. |

## Account, Submission, And Comparison

| ID | Test | Expected result |
| --- | --- | --- |
| AUTH-01 | Open `Submit` while signed out and complete the Clerk system-browser flow. | Consent is explicit, the loopback callback returns to the app, no token or email is displayed, and the run is not sent merely by signing in. |
| AUTH-02 | Close and reopen both Store products after signing in through Benchmark. | Both products restore the shared signed-in session without opening a new browser tab. |
| AUTH-03 | Sign out in either Store product. | Both products become signed out; incomplete remote revocation is reported as pending rather than confirmed. |
| SUBMIT-01 | Select a completed run, open `Submit`, review the destination, and cancel. | No benchmark is uploaded and local history is unchanged. |
| SUBMIT-02 | Explicitly send one completed run. | The app first checks the authenticated owner endpoint, posts only after a confirmed not-found result, and shows only a validated `pending_review`, `published`, `rejected`, or `deleted` server status. |
| SUBMIT-03 | Choose `Check status` for the submitted run. | The app performs an owner lookup without another POST, shows the current confirmed server status, and stores only the sanitized status checkpoint. |
| SUBMIT-04 | Retry after an uncertain network response. | The app performs owner lookup first and does not create a duplicate run automatically. |
| COMPARE-01 | Open Position without selecting explicit comparison. | Public example groups may load anonymously, but no local run or hardware criteria are sent. |
| COMPARE-02 | Select `Compare selected run`. | Only the documented CPU/GPU models, RAM, map, mode, resolution, and game version criteria are sent; the returned chart and sample counts render without publishing the local run. |
| COMPARE-03 | Restart after a successful submission and comparison. | Local run history remains intact, the saved submission status is labelled as last confirmed, and comparison requires another explicit button action. |

## Failure Handling

| ID | Test | Expected result |
| --- | --- | --- |
| ERR-01 | Cause or reproduce a normal precondition failure, such as starting outside a raid. | A concise actionable message is shown; no crash report is created for expected user-state failures. |
| ERR-02 | Reproduce an ETW access-denied failure on a standard-user setup when possible. | The app explains the permissions problem and does not save a broken measurement. |
| ERR-03 | Reproduce a genuine capture or save exception in a development build. | A sanitized report is written under the application's current `TarkovSkills\reports` data directory, copied to the clipboard, and the Crash form is offered. The report contains useful error details but no user-specific paths. |
| ERR-04 | Start a capture while the app-owned `TimmyTook.TarkovPerformanceBenchmark` ETW session remains from any earlier app version. | The stale app-owned session is stopped automatically before capture. Capture succeeds and the session is removed after completion. |
| ERR-05 | Cancel a capture, then query active ETW sessions and start another capture. | Cancellation removes the app-owned ETW session, no run is saved, and the next capture starts normally. |
| ERR-06 | Leave an orphaned legacy `PresentMon` ETW session with no running `PresentMon.exe`, then start capture. | The orphaned legacy session is removed automatically and capture starts normally. |
| ERR-07 | Keep an external `PresentMon.exe` capture running, then select `Start collection`. | The benchmark refuses to start, shows a concise `Collection unavailable` popup, keeps the reason in the main status area, does not save a run, and does not stop or modify the external PresentMon process or session. |

## Store Lifecycle

| ID | Test | Expected result |
| --- | --- | --- |
| LIFE-01 | Install a newer Store package over a version that already stores history in package `LocalState`. | Microsoft Store updates the application and preserves benchmark history. |
| LIFE-02 | Launch the execution alias after an update. | The alias starts the updated Store application. |
| LIFE-03 | Uninstall and reinstall the Store application. | Installation remains clean and starts with no package-local benchmark history. The UI does not display stale runs from an unpackaged development build. |
| LIFE-04 | For a closed package flight, add another personal Microsoft account to its known-user group. | The account gains access without a new app submission after group membership propagation. |

## Recorded Release Observations

Recorded on September 1, 2026:

- The Store build loaded Average FPS from an existing benchmark file.
- A completed run was appended to JSON.
- No dedicated run counter was visible in Store version 1.0.0. The next build adds the count to the latest-result heading.
- `Start collection` was available.
- Cancellation worked, but the `Cancel and discard` label was not visible and must be checked against `UI-04`.

Recorded on September 2, 2026:

- Version 1.0.2 intentionally starts a new package `LocalState` history and does not migrate prototype runs redirected into `LocalCache`; no public users received those prototype versions. Persistence testing begins with data created by 1.0.2.
- No `Submit` or upload button was present in Store version 1.0.0. The next build adds an explicit clipboard-and-form submission flow without automatic upload.
- With the local data directory renamed, the app opened with empty metrics and `Open folder` disabled as expected.
- The attempted first capture then failed because PresentMon reported that another PresentMon session was already running. This is separate from the missing-directory scenario and must be reproduced against `ERR-04`.
- Stopping the orphaned legacy `PresentMon` ETW session restored successful capture without elevation. Future builds use a version-independent app-owned session name and clean it before and after every capture.
- In the local post-1.0 development build, closing Tarkov during capture produced a `discarded` result, left the JSON run count unchanged, saved and uploaded nothing, and removed the app-owned ETW session.

Recorded on September 3, 2026:

- The standalone Benchmark regression checklist passed against the shared `TarkovBenchmark.Feature` implementation.
- A complete two-minute capture, completion notification, context dialog, save, metrics, run-count increment, Open folder, and Submit flow all passed.
- Cancellation discarded the partial capture without incrementing the run count.
- The standalone product kept the Toolkit-only Copy results action hidden.

Recorded on October 3, 2026, against installed Microsoft-signed Benchmark `1.0.5.0`
and Toolkit `1.0.2.0`:

- Manual submission checks returned `Pending review`; reopening the app restored the
  confirmed status. This observation does not independently prove the HTTP request count.
- Both products showed the shared signed-in session. Signing out in Toolkit also signed
  out Benchmark. Their package-local benchmark histories remained separate, as designed.
- Standalone `collect --source skill` completed a real capture and returned the expected
  machine-readable summary with `saved_locally: true` and `uploaded: false`. The user
  confirmed automatic window closure and that the saved run remained after reopening.
- Closing Tarkov during an incomplete GUI capture showed `Measurement discarded` and
  did not increase the saved run count.
- The user confirmed `Cancel and discard` behaved as expected; a subsequent capture
  completed and saved successfully. These observations do not independently inspect ETW
  cleanup, completion-sound behavior, or every failure-handling case in this checklist.
- These are user-confirmed installed-package observations, not isolated WPF test results
  or certification of every supported Windows version. Raw captures, account details and
  generated test reports are not versioned.
