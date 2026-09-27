# Benchmark Protocol

Use the signed Store applications for automated collection.

1. Start in an active raid and capture for 120 seconds by default, or 240 seconds when requested.
2. Snapshot `Graphics.ini`, `PostFx.ini`, graphically relevant `Game.ini`, and sanitized Windows hardware at capture start.
3. Read map and game version from EFT logs. Ask for a map only when log inference fails.
4. Ask after capture for `BSG servers` or `Local`. Weather and time of day are optional and may be `unknown`.
5. Do not ask for route, activity, PvP/PvE, or a separate server-model field.
6. Discard partial data after cancellation, raid exit, game exit, capture conflict, or measurement failure.
7. Keep settings and system context with every comparable run.

The standalone benchmark app owns local history and explicit submission. Toolkit reports are copied or saved by the user and are never uploaded automatically.

On application failure, use its sanitized report and Crash form. Do not invent a report from unsanitized exception paths.
