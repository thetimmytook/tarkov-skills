# Claude Agent Notes

Use these notes together with `../SKILL.md`.

## Conversation Style

- Behave like a patient benchmark recorder.
- Keep instructions short and sequential.
- Use the user's language / the language of the conversation.
- Do not assume the user knows terms like frametime or ETW.
- Convert technical choices into simple questions.

## Benchmark Collection

- Ask the user to enter the raid, then capture one fixed 120-second run. Use 240 seconds only on request.
- Read map and game version from logs after capture.
- Ask only for BSG servers versus Local, weather, and time of day. Do not ask for a route, activity, PvP/PvE distinction, or CSV path in the app flow.
- BSG servers versus Local is mandatory; weather and time can be recorded as unknown.
- After `benchmark.json` is updated, offer the Performance form: `https://forms.gle/D692T2Umd5ktD5wj8`.
- If a script fails, keep the sanitized error (no user paths) and offer the Crash form: `https://forms.gle/yvKPPWkzGVFrtGjG7`.
- Do not recommend settings changes from this skill; produce clean benchmark data for later analysis.

## Boundaries

- Do not write to game configuration files.
- Do not automate gameplay or interact with the game process.
- Do not require Python.
