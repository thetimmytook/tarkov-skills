# Tarkov Performance Toolkit Store Test Cases

Run these against the Microsoft-signed closed Store package, not a locally self-signed MSIX.

| ID | Scenario | Expected result |
|---|---|---|
| TOOLKIT-01 | Install from the closed Store audience and launch from Start. | The WPF GUI opens without a console, script, certificate, or UAC prompt. |
| TOOLKIT-02 | Resize the main window, switch between Overview, Benchmark, and Goal, then open About. | Content remains reachable without clipping; About shows TimmyTook, GitHub, privacy policy, version, and the unofficial notice, and both links open correctly. |
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
