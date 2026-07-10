# Benchmark Protocol

Use this when guiding a user through a test. Keep questions simple and assume the user may not know technical terms.

## Baseline Flow

1. Ask the user to close overlays or background apps they do not normally use.
2. Ask them to start their FPS capture tool: CapFrameX, PresentMon, or NVIDIA FrameView.
3. Ask them to launch Escape from Tarkov and enter the selected scenario. Use the user's problem map when known; otherwise use Streets for a worst-case stress test.
4. Prefer 90-180 seconds of capture.
5. Ask them to follow the same route for future runs.
6. Ask them to stop capture and provide the CSV path.

## Suggested Scenarios

- User's problem map: best choice when the goal is to reproduce a real complaint.
- Streets: default worst-case stress test, high CPU/RAM pressure.
- Customs: balanced practical test.
- Woods: visibility and long-range rendering test.
- Hideout: useful only for quick sanity checks, not final benchmark data.

## Log Context

Prefer reading map/mode/raid id from EFT logs with `scripts/read-raid-context.ps1`. This follows the same general idea used by TarkovMonitor: read game-created logs in read-only mode and infer events from known log messages.

Logs may not contain every field and may not update during the raid, so keep manual fallback questions. Do not ask the user for map or mode when logs identify them with useful confidence. Mode/server model inference is best-effort; if logs cannot distinguish PvP online, PvE BSG server, PvE local, offline practice, or hideout, mark it `unknown` and ask a plain-language fallback question.

## Manual Context Questions

Ask after the run only for fields that logs did not identify or identified with low confidence:

- Which map was it, if logs did not identify it?
- Was it PvP online, PvE on BSG servers, PvE local, offline practice, hideout, or unknown, if logs did not identify it?
- What was the weather: clear, rain, fog, snow, cloudy, or unknown?
- Was it day, night, dawn/dusk, or unknown?
- Were you standing still, walking a route, fighting, or mixed/unknown?
- Any unusual stutters, loading, alt-tab, recording, or background downloads?

## Upload And Error Reporting

After `run.json` is ready, offer to open a benchmark upload form and submit/copy the run contents. The URL is TODO.

If a PowerShell script fails, create or reference a small error report artifact and offer to open an error-report form. The URL is TODO.

## Confidence

Use `high` when duration is at least 120 seconds and the user knows map, mode, weather, and time of day.
Use `medium` for shorter or partly unknown runs.
Use `low` when the run is too short, context is vague, or the user changed many conditions at once.
