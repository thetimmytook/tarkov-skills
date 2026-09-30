# Tarkov Performance Toolkit Store Test Cases

Run these against the Microsoft-signed closed Store package, not a locally self-signed MSIX.

| ID | Scenario | Expected result |
|---|---|---|
| TOOLKIT-01 | Install from the closed Store audience and launch from Start. | The WPF GUI opens without a console, script, certificate, or UAC prompt. |
| TOOLKIT-02 | Resize the main window, switch between Overview, Benchmark, and Goal, then open About. | Content remains reachable without clipping; About shows TimmyTook, GitHub, privacy policy, version, the unofficial notice, and AI-assisted analysis. Its links open correctly; content scrolls on a short display. |
| TOOLKIT-03 | Run `tarkov-skills.exe status` from a new terminal. | Exactly one JSON document is written to stdout with dependency, game, raid, and map status. |
| TOOLKIT-04 | Run `tarkov-skills.exe inspect`. | JSON includes settings, CPU, GPU/VRAM, RAM, game-drive media, pagefile size/media, goal, and log context. |
| TOOLKIT-05 | Inspect the JSON and saved GUI report. | No username, hostname, local path, IP, serial number, machine ID, Control.ini, or Sound.ini is present. |
| TOOLKIT-06 | Save a new goal in GUI, then run `goal get`. | The same goal is returned from package LocalState. |
| TOOLKIT-07 | Run `goal set`, then reopen GUI. | GUI shows the updated goal and validates target FPS between 20 and 360. |
| TOOLKIT-08 | Use Collect report, Copy JSON, and Save JSON. | Preview, clipboard, and saved file contain equivalent sanitized JSON; nothing is uploaded. |
| TOOLKIT-09 | Open Benchmark and start collection while Tarkov is closed or outside a raid. | Collection does not start and gives a concise readiness message. |
| TOOLKIT-10 | Complete and save a two-minute run in Benchmark. | The shared benchmark UI advances the timer, plays two completion sounds, asks for required context, saves the run, and updates the latest-result metrics and run count. |
| TOOLKIT-11 | Cancel an active Benchmark collection. | UI immediately shows stopping, partial data is discarded, and no run is appended. |
| TOOLKIT-12 | Extract or leave the raid during Benchmark collection. | Polling detects the raid-end marker within roughly 4 seconds and discards the partial run. |
| TOOLKIT-13 | Close or crash Tarkov during Benchmark collection. | The run is discarded and no partial metrics are saved as valid. |
| TOOLKIT-14 | Keep an external PresentMon capture active, then start Benchmark collection or CLI capture. | Toolkit refuses with `capture_conflict` and does not stop the external process/session. |
| TOOLKIT-15 | Deny ETW access. | CLI returns nonzero with `permission_required`; the Benchmark UI explains the permission problem without silent elevation. |
| TOOLKIT-16 | Update the Store package over an existing version. | Package LocalState goal data remains available and both GUI and alias use the new version. |
| TOOLKIT-17 | Use the skill from a local agent. | Agent runs `status`, `inspect`, or an explicitly approved `capture` through `tarkov-skills.exe`, receives one sanitized JSON document on stdout, and no GUI opens. |
| TOOLKIT-18 | Use the skill from a web client. | User can collect a diagnostic report in Overview or complete a run in Benchmark, then use Copy JSON or Copy results. Copy results places only the latest completed run on the clipboard and uploads nothing. |
| TOOLKIT-19 | Compare Benchmark behavior in Toolkit and the standalone Benchmark product. | Capture readiness, timer, cancellation, context questions, result metrics, run count, and submission flow behave the same because both host `TarkovBenchmark.Feature.dll`. Copy results appears only in Toolkit, and each product keeps its own package-local history. |
| TOOLKIT-20 | Sign in from the Benchmark section, then close and reopen both Store products. | Toolkit and Benchmark share the Store publisher credential, restore the session without a second login, and display no email or token. |
| TOOLKIT-21 | Sign out from either Store product. | Both products become signed out; a network failure leaves explicit pending revocation rather than a false success. |
| TOOLKIT-22 | Cancel the submission dialog before choosing Send. | Nothing is uploaded, copied, or marked submitted. |
| TOOLKIT-23 | Explicitly send one saved run, then choose Check status. | The Academy owner lookup and submission use the production API, the status is validated and sanitized, and a status check does not create another run. |
| TOOLKIT-24 | Open Position and browse public examples without selecting a local comparison. | Public example groups and charts load anonymously; no local run criteria are sent. |
| TOOLKIT-25 | Choose Compare selected run. | The documented comparison criteria are sent only after the button action, the chart renders, and the local run is not published by comparison. |
| TOOLKIT-26 | Open Overview before collecting a report, including at the minimum window size and 150% display scaling. | Get started explains Collect report, Copy JSON and pasting into a chat with the skills. All instructions and controls remain reachable by scrolling. Copy and Save stay disabled until a report exists. |
| TOOLKIT-27 | Select Set up AI skills in Overview and About. | Both open the same repository installation guide. Opening the guide does not collect, copy, upload or install anything. Local-agent and manual web-chat workflows are distinguished. |
| TOOLKIT-28 | Hide Get started with the small ? button or the close (x) button at the card's top right, restart Toolkit, then update the Store package. | Both controls hide the entire card and its spacing, with no collapsed title row. Keyboard focus returns to the small help button in Overview. The choice persists in package-local `TarkovSkills\toolkit-ui.json`, not in the install directory or report JSON. |
| TOOLKIT-29 | Restore Get started with ?; restart again. Navigate by keyboard and switch tabs. | The card remains expanded after restart. The help button has Show/Hide getting started accessibility text and tooltip, works with keyboard input, and appears only in Overview. No report is collected or uploaded by toggling it. |
| TOOLKIT-30 | Use an unreadable or malformed UI preferences file; separately deny writing that file. | Startup and collection still work with the default expanded card. A failed save changes visibility for the current session and shows a sanitized warning; no crash or false persistence promise. |

## Skill Integration Scenarios

### Local Agent: Codex Or Claude

1. Install the signed Tarkov Performance Toolkit from Microsoft Store and install the skill version being tested. Restart or open a new agent session so it does not use a previously loaded skill copy.
2. Verify that `tarkov-skills.exe` resolves to the Store execution alias. Do not substitute a repository build for the signed-package release test.
3. In a local Codex or Claude session, ask the `tarkov-config` skill to inspect the current Tarkov configuration.
4. Confirm that the agent runs `tarkov-skills.exe status` and `tarkov-skills.exe inspect` without opening Toolkit GUI.
5. Confirm that the returned JSON contains `system`, `settings`, `raid`, and `goal`, and contains no username, hostname, local path, Control.ini, or Sound.ini.
6. Enter a raid and ask `tarkov-frametime` or `tarkov-performance-benchmark` to collect a measurement.
7. Confirm that the agent explains the timed PresentMon capture and waits for explicit consent before running `tarkov-skills.exe capture --duration 120`.
8. Confirm that no GUI opens, one JSON document is returned on stdout, and it contains Average FPS, 1% Low, 0.1% Low, P95/P99 frametime, system/settings context, and raid context.
9. Cancel the agent command or leave/crash the raid during a separate capture. Confirm that the command returns a nonzero exit code with machine-readable `cancelled` or `discarded` status and no partial result is treated as valid.

### Web Client

1. Open a web client where the skill cannot execute local commands.
2. Ask the `tarkov-config` skill to inspect the current Tarkov configuration.
3. Confirm that it directs the user to Toolkit **Overview**, then **Collect report** and **Copy JSON**, rather than claiming it can read the machine directly.
4. Paste the clipboard contents into the web conversation. Confirm that the skill accepts the report and that it contains no username, hostname, or local path.
5. Ask `tarkov-frametime` or `tarkov-performance-benchmark` for a measurement, enter a raid, and complete a run under Toolkit **Benchmark**.
6. Save the run, press **Copy results**, and paste the clipboard contents into the conversation.
7. Confirm that the JSON contains exactly one run: the latest completed run. Confirm that earlier history is absent and nothing was uploaded automatically.
8. Confirm that **Copy results** appears in Toolkit but not in the standalone Tarkov Performance Benchmark application.
