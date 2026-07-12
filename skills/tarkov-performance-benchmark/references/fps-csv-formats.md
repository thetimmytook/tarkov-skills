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

## Parser Behavior

`parse-fps-csv.ps1` handles these cases automatically:

- comment/metadata lines before the header (starting with `#` or `//`) are skipped;
- the delimiter is auto-detected between comma, semicolon, and tab (European locale exports often use `;`);
- decimal commas are converted to dots before parsing;
- non-numeric and non-positive values are dropped from the sample set.

If detection still fails, inspect the raw headers and either rename the column to a recognized name or extend the header patterns in this skill's `scripts/parse-fps-csv.ps1`.
