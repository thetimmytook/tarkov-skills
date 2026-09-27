# Claude Plugin Publication

This checklist covers preparation of the four Tarkov Performance skills for Anthropic's public Claude directory. A GitHub marketplace, a manually uploaded plugin, and an organization-shared plugin are distribution options, but they are not publication in Anthropic's public directory.

Official references:

- [Submit your plugin](https://claude.com/docs/plugins/submit)
- [Plugin pre-submission checklist](https://claude.com/docs/plugins/pre-submission-checklist)
- [Anthropic Software Directory Policy](https://support.claude.com/en/articles/13145358-anthropic-software-directory-policy)
- [Anthropic Software Directory Terms](https://support.claude.com/en/articles/13145338-anthropic-software-directory-terms)
- [Use plugins in Claude](https://support.claude.com/en/articles/13837440-use-plugins-in-claude)

## Package

- [ ] Raise `.claude-plugin/plugin.json` to the intended stable version in the release PR and merge it to `main`. The product-release workflow creates the matching `skills-v<version>` tag and GitHub Release after validation.
- [ ] Run `build/sync-skills.ps1 -Check`.
- [ ] Run `build/sync-public-plugin.ps1` and commit the generated copies with the release change.
- [ ] Confirm `plugins/tarkov-performance/` contains `.claude-plugin/plugin.json`, `README.md`, `LICENSE`, `PRIVACY.md`, `TERMS.md`, and `skills/<skill-name>/SKILL.md`.
- [ ] Confirm the public plugin folder contains no application binaries, executable scripts, Store packages, captures, or repository-only agent notes.
- [ ] Run `build/test-skills-plugin.ps1`; optionally run `claude plugin validate ./plugins/tarkov-performance` when Claude Code is installed.
- [ ] Run `build/build-claude-plugin.ps1` and inspect the clean archival ZIP.
- [ ] Upload the clean plugin as a custom plugin and run the review cases in a clean Claude web chat before requesting public review.

## Listing Draft

- **Name:** Tarkov Performance
- **Developer:** TimmyTook
- **Description:** Read-only Escape from Tarkov settings analysis, FPS and frametime interpretation, repeatable benchmark guidance, and controlled performance tuning.
- **Website:** `https://github.com/thetimmytook/tarkov-skills`
- **Support:** `https://github.com/thetimmytook/tarkov-skills/issues`
- **Privacy:** `https://github.com/thetimmytook/tarkov-skills/blob/main/PRIVACY.md`
- **Terms:** `https://github.com/thetimmytook/tarkov-skills/blob/main/TERMS.md`
- **Logo:** Use the original Tarkov Performance Toolkit artwork without official game or Battlestate Games assets.

The listing must state that Claude cannot access the user's PC. Web users explicitly collect sanitized data with Tarkov Performance Toolkit and paste or attach it to the conversation. The plugin does not edit game files, automate gameplay, read process memory, or upload benchmark data.

## Required Use Cases

Anthropic requires at least three working examples:

1. Analyze a pasted Tarkov Performance Toolkit report and identify likely FPS or stability bottlenecks.
2. Interpret a pasted benchmark result, including Average FPS, 1% Low, 0.1% Low, and P95 frametime.
3. Compare two repeatable benchmark runs and decide whether one manual setting change produced a meaningful improvement.

## Developer Portal Submission

- [ ] Connect the publisher's GitHub account to the intended Claude organization.
- [ ] Open [Claude Directory developer portal](https://claude.ai/directory/manage), select **Submit new**, then **Plugin bundle**.
- [ ] Enter repository `thetimmytook/tarkov-skills`, plugin path `plugins/tarkov-performance`, and tracked branch `main`.
- [ ] Select **Validate**, fix every blocking finding in the repository, push it, and validate the new commit again.
- [ ] Review listing details sourced from `plugin.json` and the plugin README.
- [ ] Answer the data-handling questions, confirm the contact email, and accept the required acknowledgements.
- [ ] Select **Submit for review**.
- [ ] After submission, configure the GitHub push webhook from the plugin's Settings page.
- [ ] Enable automatic publication of passing updates only when the portal makes that option available for the approved listing.

The first submission, data-handling answers, compliance acknowledgements, and initial publication require the publisher in the developer portal. After approval, the directory can detect changes to `plugins/tarkov-performance/` from `main` through the webhook. Whether a passing version publishes automatically depends on the auto-publish setting Anthropic applies to the listing; held versions still require review.

The automatically created `skills-v<version>` GitHub release also publishes a clean Claude plugin ZIP for manual testing and archival. Claude Directory updates use the committed `plugins/tarkov-performance/` folder, not the release ZIP. Never move or reuse an existing skills tag.

## Review Cases

- [ ] Confirm every skill works from pasted or attached Toolkit output without local computer access.
- [ ] Confirm missing Toolkit data produces transparent collection guidance rather than a claim that Claude can inspect the PC.
- [ ] Confirm requests to edit game files, automate gameplay, read process memory, inject code, or create an overlay are declined with a read-only alternative.
- [ ] Confirm outputs do not expose user names, host names, local paths, IP addresses, serial numbers, or machine identifiers.
- [ ] Confirm the public support contact, documentation, privacy policy, terms, and developer identity are current.
- [ ] Agree to Anthropic's Software Directory Terms and submit through the Claude Directory developer portal.

## After Publication

- [ ] Install the public listing from a non-developer Claude account.
- [ ] Run the three listed use cases and the safety cases in a clean web chat.
- [ ] Verify the listing, logo, support links, privacy policy, and terms.
- [ ] Add the public directory link to README and replace future-tense wording only after the listing is live.
- [ ] Confirm a later version bump on `main` is detected by the configured webhook and follows the selected auto-publish policy.
