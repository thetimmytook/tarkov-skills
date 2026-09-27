---
name: tarkov-frametime
description: Collect or interpret read-only Escape from Tarkov FPS and frametime metrics. Use for average FPS, 1% low, 0.1% low, p95/p99 frametime, duration, and sample count; prefer the signed Tarkov Performance Toolkit and leave map/settings interpretation to benchmark or tuning skills.
---

# Tarkov Frametime

Collect performance metrics without changing game files, reading game memory, injecting code, or automating input.

## Automated Capture

For a local agent:

1. Run `tarkov-skills.exe status`.
2. Confirm that Tarkov is running and `raid_active` is true.
3. Tell the user that capture uses bundled PresentMon and ask before starting the timed measurement.
4. Run `tarkov-skills.exe capture --duration 120`, or `240` when requested.
5. Consume the returned JSON. A nonzero exit and JSON status such as `permission_required`, `capture_conflict`, or `failed` is a failed capture; do not keep partial results.

For a web client, ask the user to open **Tarkov Performance Toolkit**, select **Benchmark**, enter a raid, press **Start collection**, save the completed run, then press **Copy results** and paste the JSON into the conversation.

If Toolkit is unavailable, direct the user to `https://apps.microsoft.com/detail/9N3L7DZH0K64`. There is no unsigned-script capture fallback. You may interpret an existing PresentMon, CapFrameX, or FrameView CSV the user already has, but do not claim an automated capture occurred.

Return average FPS, 1% low, 0.1% low, p95/p99 frametime, duration, sample count, and method. Do not collect map, weather, server execution, or settings conclusions in this skill.
