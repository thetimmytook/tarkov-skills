# FPS CSV Formats

Use this if `parse-fps-csv.ps1` cannot detect the capture format.

## Preferred Data

Prefer per-frame frametime in milliseconds. If available, compute:

- average FPS as `frame_count / total_seconds`
- 1% low FPS from the slowest 1% frametimes
- 0.1% low FPS from the slowest 0.1% frametimes
- average, p95, and p99 frametime

## Header Hints

PresentMon often includes fields such as:

- `MsBetweenPresents`
- `msBetweenPresents`
- `Application`
- `ProcessName`

CapFrameX exports may include:

- `Frametime`
- `Frame Time`
- `FPS`

FrameView exports may include:

- `FPS`
- `FrameTime`
- `Frame Time`

If only FPS samples are available, compute average and lows from FPS samples but mark `method` as `fps_samples`, because this is less reliable than frametime.
