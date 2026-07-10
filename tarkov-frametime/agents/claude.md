# Claude Agent Notes

Use these notes together with `../SKILL.md`.

- Keep instructions short and sequential.
- Use the user's language / the language of the conversation.
- Ask the user to start the scenario before capture, but do not ask what map or mode it is.
- Treat PresentMon capture as blocking; if the environment supports delegated workers, use one for capture and keep the main conversation free.
- If capture is blocked, switch calmly to manual CSV export mode.
- Return the FPS summary plainly and avoid interpreting settings/context.
