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

The [settings help](references/settings-help.md) explains graphics options with
published visual comparisons and an optional manual starting order that preserves
gameplay preferences. Explanations need no Toolkit or benchmark. Comparisons are
visual examples, not FPS predictions; moving examples are identified separately.

The [response model](references/graphics-response-model.md) supplies provisional
aggregate priors for choosing the next test and forecasting conditional FPS/resource
ranges. It does not promise fixed gains or universal memory requirements.

The [settings evidence guide](references/settings-evidence-guide.md) covers
the limits of measured effects and recommendations across the graphics and
performance-related Game options.

[Session stability](references/session-stability.md) covers later-raid memory
growth, crash diagnosis and conditional restart workarounds. Ordinary tuning
does not require a separate warmup or preparation raids.

Interesting model deviations can produce an optional
[feedback report](references/model-feedback.md) for manual GitHub submission,
with reviewed measurements and a ready-to-copy issue. Nothing is sent
automatically, and a full conversation export is not required.
