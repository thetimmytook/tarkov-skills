# Claude Instructions

Inherit and follow all repository-wide rules from `AGENTS.md`. This file only adds Claude-specific behavior and must not duplicate or weaken `AGENTS.md`.

## Loading Order

1. Read `AGENTS.md`.
2. Read the relevant skill's `SKILL.md`.
3. If present, read the relevant skill's `agents/CLAUDE.md`.
4. Use the skill's `README.md` as the human-facing usage description.

## Claude Style

- Prefer concise, practical instructions.
- Use the user's language / the language of the conversation.
- Ask one step at a time during guided tests.
- Explain uncertainty plainly.
- Do not assume the user knows technical terms.
- Convert technical decisions into simple user questions.
