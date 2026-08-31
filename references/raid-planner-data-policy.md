# Raid Planner Data and Asset Policy

Status: accepted

## Goal

Build a non-commercial interactive raid-planning viewer that helps players understand routes, destinations, extracts, containers, and possible loot without redistributing Escape from Tarkov assets or publishing hidden game data.

## Decision

- Keep the application code independent from game content. Code may use the MIT license, but that license applies only to code owned by this project.
- Do not ship, host, or redistribute EFT meshes, textures, materials, shaders, audio, scenes, bundles, or other extracted assets.
- Do not ship a client-file datamining workflow for hidden loot tables, unreleased content, or exact spawn weights.
- Build the visual map from original schematic geometry or separately licensed community maps. Track the source, author, and license of every non-original map asset.
- If using the community `tarkov-dev-svg-maps` project, comply with CC BY-NC-SA 4.0: attribution, non-commercial use, change disclosure, and ShareAlike for adaptations.
- Use documented public community APIs, currently tarkov.dev, for map coordinates and gameplay metadata where practical.
- Treat loose-loot item lists as possible spawns, not probabilities. Do not display exact percentages unless an authorized public source explicitly provides them.
- Label data with its source, game mode, and last-updated date. Expect live loot behavior to differ because server-side weights, events, and patches may change it.
- Keep all game interaction read-only. Do not inspect process memory, game network traffic, anti-cheat components, or live raid state.
- Do not imply that the project is official, sponsored, or endorsed by Battlestate Games.

## Preferred Architecture

```text
Original or licensed schematic map
                 +
Documented public map/gameplay API
                 +
User-created route annotations
                 |
                 v
Non-commercial interactive raid planner
```

The viewer may use Three.js for schematic 3D presentation. Local game-file reading is not required for the public product and must not become a path for exporting or publishing original game content.

## Licensing Boundary

- MIT permits commercial reuse, so it cannot by itself enforce a non-commercial project policy.
- Code and content licenses are separate. A MIT codebase may display CC BY-NC-SA map content if attribution and distribution obligations are handled separately.
- A community license grants only the rights held by that community author; it does not grant Battlestate Games trademarks or other third-party rights.

## Public References

- tarkov.dev describes its API as available for building EFT-related tools: <https://tarkov.dev/about>
- tarkov.dev source code: <https://github.com/the-hideout/tarkov-dev>
- Community SVG map license (CC BY-NC-SA 4.0): <https://github.com/the-hideout/tarkov-dev-svg-maps/blob/main/LICENSE.md>
- Battlestate Games statement concerning datamining: <https://x.com/tarkov/status/1672289412405686273>

## Revisit When

Revisit this decision before adding local game-file parsing, publishing any derived map geometry, enabling monetization, changing content licenses, or claiming exact loot probabilities.
