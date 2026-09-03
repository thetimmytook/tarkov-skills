# Microsoft Store Release Test Cases

Use this checklist to validate the Microsoft-signed private Store release before making the product public. Record `Pass`, `Fail`, or `Blocked` for every case and attach a short note for failures.

## Preconditions

- Windows 10 build 19041 or later, or Windows 11.
- Microsoft Store is signed in with a personal Microsoft account from the private audience.
- The Microsoft-signed Store package is installed.
- Escape from Tarkov is available for the real-raid capture cases.
- Record the initial run count shown by the application and, when available, the number of entries in the package-local `benchmark.json` opened through `Open folder`.
- The PoC Google Form uses a required paragraph field with a maximum length of 200,000 characters.

## Store Installation And Launch

| ID | Test | Expected result |
| --- | --- | --- |
| STORE-01 | Acquire the app from its private Store listing. | The authorized account can see and install the app without trusting a local certificate. |
| STORE-02 | Launch the app from the Start menu. | The app opens without UAC, certificate, PowerShell, or console prompts. |
| STORE-03 | Verify the installed package identity, publisher signature, version, and architecture. | Identity is `TimmyTook.TarkovPerformanceBenchmark`, the package is Microsoft-signed, architecture is x64, and the version matches the submission. |
| STORE-04 | Launch the app a second time while it is already open. | No second application window is created. |
| STORE-05 | Run `tarkov-benchmark.exe` from a new terminal. | The Store application opens through its execution alias. |

## Main Window

| ID | Test | Expected result |
| --- | --- | --- |
| UI-01 | Inspect text, buttons, dropdowns, disabled states, and hover states. | All text is readable against its background and disabled controls are visually distinct. |
| UI-02 | Open About. | Product name, installed version, author `TimmyTook`, PresentMon notice, privacy summary, non-affiliation notice, and working GitHub link are shown. |
| UI-03 | Inspect the window, taskbar, Start menu, installed-app entry, and Store listing icons. | Original product artwork is shown consistently; the default executable icon is not used. This is required before public release. |
| UI-04 | Inspect collection controls before and during a capture. | `Start collection` is readable and enabled when PresentMon is available. `Cancel and discard` is readable but disabled before capture, then enabled during capture. |
| UI-05 | Inspect the latest-result panel with existing data. | The heading shows `LATEST RESULT · N RUNS` with correct singular/plural text. Average FPS, 1% Low, 0.1% Low, and P95 frametime are visible. `Open folder` and `Submit` are enabled when runs exist. |
| UI-06 | Compare the standalone latest-result actions with Toolkit. | The standalone Benchmark does not show the Toolkit-only `Copy results` action; Open folder and Submit continue to work. |

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

## Manual Submission

| ID | Test | Expected result |
| --- | --- | --- |
| SUBMIT-01 | Select `Submit` when unsubmitted runs exist. | The app explicitly says that valid JSON for only the unsubmitted runs was copied to the clipboard and instructs the user to paste it into the form with `Ctrl+V`. The dialog offers clear `Open form` and `Cancel` commands; the form opens only after selecting `Open form`, and nothing is posted automatically. |
| SUBMIT-02 | Paste the clipboard contents into the Google Form paragraph field, submit it, then return to the app. | The submission confirmation appears only after returning to the app. Confirming marks only the copied runs as `submitted: true` in the local JSON. |
| SUBMIT-03 | Select `Submit` when every run is already marked submitted. | The app explains that there are no new runs and asks whether up to 20 most recent runs should be copied again. |
| SUBMIT-04 | Keep more than 20 unsubmitted runs, then select `Submit`. | The payload contains only the 20 most recent unsubmitted runs, remains below the PoC form limit of 200,000 characters for normal run sizes, and local history is not deleted. |

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
| LIFE-04 | Add another personal Microsoft account to the private-audience known user group. | The account gains access without a new app submission after group membership propagation. |

## Current Private-Flight Observations

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
