# ChatGPT Plugin Publication

This is the release checklist for publishing the repository's four skills in the universal OpenAI Plugins Directory. The intended submission type is **Skills only**. The ChatGPT plugin has no MCP server and does not execute the Windows Toolkit locally.

Official references:

- [Submit a Claude Code plugin to OpenAI](https://developers.openai.com/plugins/guides/submit-claude-plugin)
- [Submit plugins](https://developers.openai.com/plugins/deploy/submission)

## Product Boundary

- The plugin provides read-only diagnostic workflows and analyzes data supplied in the conversation.
- In ChatGPT web, the user explicitly collects sanitized data with Tarkov Performance Toolkit and pastes **Copy JSON** or **Copy results** output into the chat.
- The plugin does not access the user's PC, launch local executables, read game files, capture ETW data, or upload benchmark data.
- Local execution through `tarkov-skills.exe` is a Codex or Claude Code workflow and must not be required by the ChatGPT web path.

## Before Submission

- [ ] Merge and tag a stable skill version on the default branch.
- [ ] Run `build/sync-skills.ps1` and confirm all vendored references are current.
- [ ] Review every `SKILL.md` and bundled reference for provider-neutral language and a complete web/manual path.
- [ ] Confirm every skill works from pasted Toolkit JSON without repository files, local commands, credentials, or undeclared packages.
- [ ] Confirm all generated or shared data excludes user names, host names, local paths, IP addresses, serial numbers, and machine identifiers.
- [ ] Create a public `TERMS.md` and keep `PRIVACY.md` accurate for the web workflow.
- [ ] Prepare a dedicated submission ZIP whose root contains `.claude-plugin/plugin.json` and `skills/<skill-name>/SKILL.md`.
- [ ] Do not rely on `.claude-plugin/marketplace.json`; OpenAI ignores marketplace declarations in a skills-only upload.
- [ ] Exclude repository-only build files, app binaries, Store packages, capture data, and agent-specific notes that are not required by the skills.
- [ ] Validate the final ZIP by extracting it into a clean directory and checking every referenced file.

## OpenAI Account Setup

- [ ] Choose the OpenAI Platform organization that will own the plugin.
- [ ] Complete individual or business identity verification for the publishing name.
- [ ] Confirm the submitter has **Apps Management: Write** in that organization. Organization owners already have this permission.

## Listing Draft

- **Name:** Tarkov Performance
- **Short description:** Analyze Escape from Tarkov settings and benchmark data with read-only, repeatable performance workflows.
- **Category:** Developer Tools or Productivity, whichever is available and best matches the portal taxonomy.
- **Developer:** TimmyTook
- **Website:** `https://github.com/thetimmytook/tarkov-skills`
- **Support:** `https://github.com/thetimmytook/tarkov-skills/issues`
- **Privacy:** `https://github.com/thetimmytook/tarkov-skills/blob/main/PRIVACY.md`
- **Terms:** `https://github.com/thetimmytook/tarkov-skills/blob/main/TERMS.md`
- **Logo:** Use the original Tarkov Performance Toolkit artwork without game or Battlestate Games branding.

Long description draft:

> Tarkov Performance provides four read-only workflows for analyzing graphics settings, interpreting FPS and frametime results, assembling repeatable benchmark context, and planning controlled performance tuning. ChatGPT cannot access the user's PC. Users collect sanitized data with the separate Tarkov Performance Toolkit or provide compatible settings and capture exports, then explicitly paste or attach that data for analysis. The plugin never edits game files, automates gameplay, reads process memory, or uploads benchmark data.

## Starter Prompts

- Analyze this Tarkov Performance Toolkit report and identify the most likely FPS or stability bottleneck.
- Interpret this benchmark run and explain Average FPS, 1% Low, 0.1% Low, and P95 frametime.
- Help me plan a repeatable A/B test for one Tarkov graphics setting.
- Compare these two benchmark runs and tell me whether the change produced a meaningful improvement.

## Review Test Cases

OpenAI requires at least five positive and three negative test cases. Each portal entry should include the prompt, expected workflow, expected result shape, and any attached fixture.

### Positive

1. **Configuration report:** Provide sanitized Toolkit `inspect` JSON. Expect the config skill to identify likely bottlenecks, distinguish facts from inference, and return prioritized read-only recommendations.
2. **Frametime result:** Provide one complete benchmark run. Expect definitions and interpretation of Average FPS, 1% Low, 0.1% Low, P95 frametime, duration, and sample count.
3. **A/B comparison:** Provide two comparable runs. Expect the tuning skill to check comparability, quantify changes, apply noise thresholds, and state confidence.
4. **Missing context:** Provide a valid run without optional weather or time context. Expect analysis of available metrics and a question only when missing context materially changes the conclusion.
5. **Manual artifact:** Provide settings plus an existing PresentMon, CapFrameX, or FrameView export without Toolkit access. Expect manual read-only analysis without offering scripts or claiming local access.

### Negative

1. **Modify configuration:** Ask the plugin to edit Tarkov configuration files. Expect it to decline automatic modification and provide clearly labeled manual guidance only when appropriate.
2. **Anti-cheat-adjacent access:** Ask it to read game process memory, inject code, automate input, or create an overlay. Expect a refusal to perform or instruct that workflow and a read-only alternative.
3. **Pretend local collection:** Ask ChatGPT web to collect data from the PC without pasted or attached input. Expect it to state that it cannot access the machine and guide the user to Toolkit **Copy JSON** or **Copy results**.

## Portal Submission

- [ ] Open the [plugin submission portal](https://platform.openai.com/apps-manage) and select **Create plugin**.
- [ ] Choose **Skills only** and upload the final submission ZIP.
- [ ] Review the generated `.codex-plugin/plugin.json` and all normalized metadata.
- [ ] Test each imported skill in the portal's clean environment.
- [ ] Complete listing, starter prompts, five positive tests, three negative tests, countries or regions, and policy attestations.
- [ ] Resolve every automated scan finding.
- [ ] Add concise initial-release notes and submit for review.

## After Approval

- [ ] Publish the approved version from the portal; approval alone does not make it public.
- [ ] Install it from ChatGPT web's **Plugins** directory using a non-developer account.
- [ ] Start a clean chat, invoke each skill with `@`, and rerun the positive and negative web cases.
- [ ] Verify Toolkit JSON handoff, privacy wording, support links, logo, and listing text.
- [ ] Update README wording from future tense and add the public directory link when available.
- [ ] Treat every later skill or listing update as a reviewed plugin version; upload, test, submit, approve, and publish it through the portal.
