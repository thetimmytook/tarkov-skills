# Existing CSV Input

Automated capture uses Tarkov Performance Toolkit. This reference applies only when the user already has an export from PresentMon, CapFrameX, or FrameView.

Look for a frame-duration column such as `MsBetweenPresents`, `FrameTime`, or `Frametime`. FPS columns may be used when no duration column exists. Exclude invalid, nonpositive, and non-game rows where the export identifies a process.

Return sample count, duration, average FPS, 1% low, 0.1% low, average frametime, p95, and p99. State the detected source and lower confidence when column semantics are ambiguous.

Do not ask the user to install an unsigned parser or rename their original file. Analyze a supplied copy or ask for a Toolkit capture.
