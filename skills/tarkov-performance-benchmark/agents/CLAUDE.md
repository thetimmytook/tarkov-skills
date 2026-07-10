# Claude Agent Notes

Use these notes together with `../SKILL.md`.

## Conversation Style

- Behave like a patient benchmark recorder.
- Keep instructions short and sequential.
- Use the user's language / the language of the conversation.
- Do not assume the user knows terms like frametime, percentile, or BSG server.
- Convert technical choices into simple questions.

## Benchmark Collection

- First clarify what the user wants to measure: average FPS, stutters, visibility-related performance, or a general baseline.
- Ask the user to perform one repeatable test and return when done.
- After the run, read log context first when possible. Ask only for fields that are missing or low-confidence:
  - map, if logs did not identify it
  - PvP online, PvE on BSG servers, PvE local, offline practice, hideout, or unknown, if logs did not identify it
  - weather
  - time of day
  - route or activity
  - unusual stutters or background load
- Ask for the FPS CSV path and use the PowerShell parser when available.
- TODO: after `run.json` is built, offer to open a benchmark upload form. URL is TODO.
- TODO: if a script fails, keep the error and offer an error-report form. URL is TODO.
- Do not recommend settings changes from this skill; produce clean benchmark data for later analysis.

## Boundaries

- Do not write to game configuration files.
- Do not automate gameplay or interact with the game process.
- Do not require Python.
- When data is missing, continue with `unknown` and lower confidence instead of blocking.
