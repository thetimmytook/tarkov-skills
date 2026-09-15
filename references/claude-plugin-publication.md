# Claude Plugin Publication

This checklist covers preparation of the four Tarkov Performance skills for Anthropic's public Claude directory. A GitHub marketplace, a manually uploaded plugin, and an organization-shared plugin are distribution options, but they are not publication in Anthropic's public directory.

Official references:

- [Anthropic Software Directory Policy](https://support.claude.com/en/articles/13145358-anthropic-software-directory-policy)
- [Anthropic Software Directory Terms](https://support.claude.com/en/articles/13145338-anthropic-software-directory-terms)
- [Use plugins in Claude](https://support.claude.com/en/articles/13837440-use-plugins-in-claude)

## Package

- [ ] Merge and tag a stable skill version on the default branch.
- [ ] Run `build/sync-skills.ps1 -Check`.
- [ ] Build the provider-neutral plugin with `build/build-skills-plugin.ps1`.
- [ ] Confirm the ZIP root contains `.claude-plugin/plugin.json` and `skills/<skill-name>/SKILL.md`.
- [ ] Confirm the ZIP contains no application binaries, executable scripts, Store packages, captures, or repository-only agent notes.
- [ ] Upload the exact final ZIP as a custom plugin and run the review cases in a clean Claude web chat before requesting public review.

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

## Review

- [ ] Confirm every skill works from pasted or attached Toolkit output without local computer access.
- [ ] Confirm missing Toolkit data produces transparent collection guidance rather than a claim that Claude can inspect the PC.
- [ ] Confirm requests to edit game files, automate gameplay, read process memory, inject code, or create an overlay are declined with a read-only alternative.
- [ ] Confirm outputs do not expose user names, host names, local paths, IP addresses, serial numbers, or machine identifiers.
- [ ] Confirm the public support contact, documentation, privacy policy, terms, and developer identity are current.
- [ ] Agree to Anthropic's Software Directory Terms and submit through the public-review route supplied by Anthropic.

Anthropic documents public directory review requirements but does not currently document a general self-service submission portal for skills-only plugins. Do not claim public availability until Anthropic has accepted and listed the plugin. Record the accepted submission route here when Anthropic provides it.

## After Publication

- [ ] Install the public listing from a non-developer Claude account.
- [ ] Run the three listed use cases and the safety cases in a clean web chat.
- [ ] Verify the listing, logo, support links, privacy policy, and terms.
- [ ] Add the public directory link to README and replace future-tense wording only after the listing is live.
