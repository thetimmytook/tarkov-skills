# First-Time Game Settings

An optional starting profile for information visibility and interaction preferences in
Escape from Tarkov's first settings tab, **Game**. This is not an FPS optimization,
a graphics preset, or a requirement for collecting a benchmark.

## When To Offer It

- Offer it when the user says this is their first setup or asks for beginner Game settings.
  If a setup request is ambiguous, ask whether they want this optional starting profile.
  Do not infer first-time setup from missing history, a default FPS goal, or missing files.
- For an established setup or an ordinary FPS/stutter investigation, skip this step unless
  requested. Reuse an accepted or declined choice already present in the conversation;
  do not proactively repeat the profile during subsequent tuning captures. The user
  may explicitly ask to revisit it.
- Use confirmed values from the existing report, pasted settings, or screenshots first.
  Suggest only differences that are known. For unknown values, give the target UI labels
  for the user to check; do not claim the current setting is wrong or invent numeric enums.
- The profile can be explained without Toolkit, a running game, sign-in, or a capture.
  The user applies any chosen settings manually in **Settings -> Game**. Never edit files,
  automate clicks/key presses, or collect Controls/Sound settings for this step.
- Preserve personal preferences. If a label or value is unavailable in the user's game
  version, skip it rather than guessing a replacement. Ask for a screenshot only if needed
  to help with that particular option.

## Information Visibility And Hints

Explain that this group keeps useful status information and operation hints visible.
It is a beginner-oriented preference, not a promise of better FPS.

| Game option | Suggested value |
| --- | --- |
| Quick slots | Always shown |
| Stamina and stance | Always shown |
| Health condition | Always shown |
| Health color scheme | Polychrome |
| Highlight available operations | Everything |
| Priority window | Auto |

## Interaction And Inventory Preferences

Offer these as choices, not mandatory controls. The player may keep a different
interaction style or inventory workflow.

| Game option | Suggested value |
| --- | --- |
| Double click item quick use | In raid only |
| Vaulting over medium obstacles | Hotkey |
| Continuous medkit healing | Enabled |
| Wishlist item notifications | Enabled |
| Auto add to Wishlist | Favorite recipes + zone upgrades |
| Task item warning | Enabled |
| Task item search assistance | Enabled |

## Handoff To Performance Tuning

Keep this optional setup separate from the FPS/quality goal and graphics change batches.
Do not write its acceptance to Goal memory or change the target FPS to record it. If the
user wants both first-time setup and tuning, finish or skip this profile before the first
baseline capture, then proceed with the normal consented measure/change/measure loop.
Skipping the profile must not block diagnosis or capture. Do not attribute an FPS change
to this profile without a separate controlled measurement.
