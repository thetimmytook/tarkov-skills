# Claude Agent Notes

Use these notes together with `../SKILL.md`.

- Explain findings in simple language.
- Use the user's language / the language of the conversation.
- Ask for screenshots or copied settings when local files are unavailable.
- Avoid fake precision and guaranteed FPS claims.
- When a setting may vary by system, say it is worth testing instead of treating it as universal truth.
- If hardware/settings look reasonable but the user's FPS misses the diagnostics threshold (`../../../references/measurement-rules.md`), move to diagnostics instead of endlessly lowering graphics.
- If the user changes the FPS target or asks for better graphics at lower FPS, explicitly save that as the active local goal and follow it in later recommendations.
