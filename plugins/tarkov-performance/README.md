# Tarkov Performance

Tarkov Performance provides four read-only workflows for Escape from Tarkov: graphics configuration analysis, FPS and frametime interpretation, repeatable benchmark guidance, and controlled performance tuning. It never edits game files, automates gameplay, reads process memory, injects code, or creates an overlay.

## Using the plugin

In Claude web or desktop chat, the plugin cannot inspect the user's PC. Open the separately installed **Tarkov Performance Toolkit**, collect the relevant report or benchmark, choose **Copy JSON** or **Copy results**, and paste the sanitized result into the conversation. Nothing is uploaded automatically.

In Claude Code, a local agent can use the signed Toolkit's `tarkov-skills.exe` execution alias when it is installed. The skills also accept user-provided settings and existing PresentMon, CapFrameX, or FrameView exports for transparent manual analysis.

The plugin includes these skills:

- `tarkov-config` analyzes settings and Windows performance context.
- `tarkov-frametime` interprets FPS and frametime statistics.
- `tarkov-performance-benchmark` guides a repeatable benchmark run.
- `tarkov-tuning` compares controlled before-and-after measurements.

The workflows exclude user names, host names, local paths, IP addresses, serial numbers, and machine identifiers from shareable results. See the [privacy policy](PRIVACY.md) and [terms](TERMS.md). Support and source are available at <https://github.com/thetimmytook/tarkov-skills>.
