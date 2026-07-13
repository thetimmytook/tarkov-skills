# Benchmark Protocol

Use this when guiding a player through a test. Keep questions simple and assume the player may not know technical terms.

## Baseline Flow

1. Ask the player to close overlays or background apps they do not normally use.
2. Ask them to launch Escape from Tarkov and enter the selected scenario. Use the player's problem map when known; otherwise use Streets for a worst-case stress test.
3. Start automated PresentMon capture after the player is in the raid.
4. Capture 120 seconds by default. Use 240 seconds only when needed.
5. Let the app stop capture and parse the CSV automatically.

## Suggested Scenarios

- Player's problem map: best choice when the goal is to reproduce a real complaint.
- Streets: default worst-case stress test, high CPU/RAM pressure.
- Customs: balanced practical test.
- Woods: visibility and long-range rendering test.
- Hideout: useful only for quick sanity checks, not final benchmark data.

## Log Context

Read map, raid ID, and game version from EFT logs with `scripts/read-raid-context.ps1`. This follows the same general idea used by TarkovMonitor: read game-created logs in read-only mode and infer events from known log messages.

Logs may not update during the raid. They provide map and game version context but cannot reliably identify PvP versus PvE. Do not ask the player for the map unless parsing has failed; the only mandatory manual environment choice is BSG servers versus Local.

## Manual Context Questions

Ask after the run:

- Did it run on BSG servers or Local? Require one answer.
- What was the weather: clear, rain, fog, snow, cloudy, or unknown?
- Was it day, night, dawn/dusk, or unknown?

## Upload And Error Reporting

After `benchmark.json` is updated, offer the Performance form: `https://forms.gle/D692T2Umd5ktD5wj8`. Google Forms cannot receive the JSON automatically through this short link; the player supplies the data in the form.

Build the paste payload with `scripts/get-benchmark-upload.ps1`: it returns only runs added since the last submission, and `-MarkUploaded` records `uploaded_run_count`/`last_uploaded_at` in `benchmark.json` afterwards, so repeated submissions do not create duplicates in the database.

If a PowerShell script fails, create or reference a small error report artifact and offer the Crash form: `https://forms.gle/yvKPPWkzGVFrtGjG7`.

## Confidence

Confidence tiers are defined in `measurement-rules.md` and computed by `scripts/add-benchmark-run.ps1`.
