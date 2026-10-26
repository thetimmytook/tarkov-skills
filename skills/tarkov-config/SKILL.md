---
name: tarkov-config
description: Analyze Escape from Tarkov graphics configuration and Windows performance context, explain settings with visual comparisons, or guide optional first-time Game-tab setup, without changing game files. Use for FPS expectations, memory-related crash or stutter risks, visibility tradeoffs, or a saved performance goal; prefer the signed Toolkit for collection and support transparent manual review when it is unavailable.
---

# Tarkov Config

Analyze settings and system readiness in read-only mode. Never edit EFT files or promise a specific FPS.

## Choose The Next Action

Follow the user's active FPS, stutter or image-quality goal. Choose the immediate
action before recommending a setting change:

| Request and evidence | Immediate action |
| --- | --- |
| Explain an option or give a general manual route | Read `references/settings-help.md` and answer; no capture is required. |
| Review a configuration without measured tuning | Explain the supplied settings and evidence limits. |
| Tune this PC, with no usable in-raid baseline | Obtain a baseline with the current settings unchanged, after readiness. Choose a change after the result. |
| Tune with a recent comparable baseline | Reuse it; select one useful change for the measured constraint and active goal. |
| Goal met and quality/stability accepted | Keep the profile and stop. |

Hardware specifications, saved settings and menu RAM availability do not establish
the in-raid limit or the budget for better graphics. Without a baseline, possible
settings are future candidates, not a change-first checklist. If the user chooses
unmeasured manual advice instead, explain its uncertainty and continue helping.

For a quality goal, spend measured headroom on a relevant visual improvement.
Disabling an effect removes that feature; describe the tradeoff and the user's
preference rather than calling its removal an automatic quality upgrade.

## Settings Help

For explanations, visual tradeoffs or a requested manual graphics starting route,
read `references/settings-help.md` before supplying menu labels or comparison
links. A help-only request needs no Toolkit, running
game or capture. Show relevant published comparisons and distinguish stills from
moving examples; honor a still-images-only request. Preserve preferences and use
`references/settings-evidence-guide.md` for evidence limits. Continue to collection
below when the user wants analysis of their system or a measured change.

## Optional First-Time Setup

For first-time setup or a request for beginner Game settings, use
`references/game-starting-profile.md`. Offer its manual Game-tab profile separately from
FPS optimization; preserve the user's preferences and skip settings already matching.
Do not proactively repeat it during ordinary FPS troubleshooting or after the user
accepted/declined it in the conversation; revisit it only if requested. A setup-only
request needs no Toolkit report or capture.

## Choose The Input Path

1. For a local agent, run `tarkov-skills.exe status`. If available, run `tarkov-skills.exe inspect` and consume its JSON.
2. In a web client, ask the user to open **Tarkov Performance Toolkit**, press **Collect report**, then **Copy JSON** and paste or attach the result.
3. Without Toolkit, explain each source before reading it. A local agent may directly read `Graphics.ini`, `PostFx.ini`, and `Game.ini` and use standard read-only Windows system queries. A web user may attach those files and a Windows System Information report. Do not download, generate, or execute script files.

The Toolkit is the only automated dependency. If it is missing, direct the user to `https://apps.microsoft.com/detail/9N3L7DZH0K64`; do not offer unsigned PowerShell as a fallback.

## Goal

Read the active goal from `inspect` or `tarkov-skills.exe goal get`. When the user changes the target or quality tradeoff, confirm the values and save them with:

```text
tarkov-skills.exe goal set --goal <name> --target-fps <20-360> --quality <text> [--notes <text>]
```

In web/manual mode, keep the goal in the conversation when the local command cannot be run.

## Analysis

- For performance analysis, use only `Graphics.ini`, `PostFx.ini`, and graphically relevant `Game.ini` data. The optional Game-tab profile is separate manual guidance; ignore Controls/Sound settings in both paths.
- Consider CPU, GPU/VRAM, RAM, game-drive media, pagefile size/media, resolution, and driver version.
- For crashes, deterioration after several raids or little first-raid memory headroom, use `references/session-stability.md`. Consider later-session risk and offer lower-memory settings or a provisional symptom-based restart schedule; do not certify stability from a clean run alone.
- Suggest only changes that are not already applied.
- For a full settings review, use `references/settings-help.md` for plain descriptions/comparisons and `references/settings-evidence-guide.md` for evidence limits; distinguish measured effects from mechanisms and preferences. Unknown costs are not zero, and a review does not require testing every option.
- For a ranked recommendation or FPS/resource forecast, use `references/graphics-response-model.md` and its aggregate priors. Priority points choose the next useful test; they do not convert into FPS. Give conditional ranges from a comparable measured baseline, preserve unknowns, and keep user captures/calibration outside the skill bundle.
- For questions about existing test findings, read `references/graphics-response-model.md` and the relevant setting in `references/settings-evidence-guide.md` before answering. Check those sources before asking the user to supply research already included in the skill.
- When evidence reveals an interesting model deviation or coverage gap, read `references/model-feedback.md`. Present a filled, redacted issue title/body and `https://github.com/thetimmytook/tarkov-skills/issues/new` in the same response; use `unknown` for missing facts. Leave submission to the user and respect a decline. A first transfer miss alone is not atypical.
- For Automatic RAM Cleaner behavior, use `references/automatic-ram-cleaner.md`; retail has a confirmed native trim path but unknown checkbox/event linkage. Keep older local/SPT named events and system-memory observations separate.
- Verify build/map/AI/server context, resolution, caps, settings and session comparability before using a baseline. A shortfall of at least 15% against a relevant expectation triggers diagnostics; a transferred provisional prior alone does not establish that expectation or a hardware fault.
- Treat driver currency as a manual check against the GPU vendor's official page.

Use the readiness display and response conventions in `references/configuration-rules.md`. Apply thresholds from `references/measurement-rules.md`.
