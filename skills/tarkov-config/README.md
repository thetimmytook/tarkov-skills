# Tarkov Config

Read-only agent instructions for interpreting Escape from Tarkov graphics settings, Windows hardware, storage, pagefile context, and the player's saved FPS/quality goal.

For a first-time setup, the skill can also offer an optional manual **Game**-tab profile
for visible status information, hints, and interaction preferences. This is separate
from FPS optimization; existing preferences and a declined profile are respected.

## Use

Automated local collection comes from the signed **Tarkov Performance Toolkit** Microsoft Store application:

```text
tarkov-skills.exe inspect
tarkov-skills.exe goal get
tarkov-skills.exe goal set --goal stable-fps --target-fps 60 --quality "balanced visibility/performance"
```

In a web client, open Toolkit, press **Collect report**, then **Copy JSON** and paste or attach the report.

Without Toolkit, the agent may explain and perform individual read-only file/system checks, or analyze files supplied by the user. This skill ships no scripts and never changes game files.
