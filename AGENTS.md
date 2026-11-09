# Repository instructions

These instructions apply to the entire repository, for human contributors and coding agents.

- Read [CONTRIBUTING.md](CONTRIBUTING.md) before making changes. It is the shared source for architecture, coding style, privacy, validation, and PR conventions.
- Inspect the working tree first. Preserve existing work and keep changes focused on the requested task; do not reformat unrelated code.
- Keep parsing and domain state in `BossMonitor.Core`, and presentation effects in the WPF app. Do not change tracked facts to implement a visual effect.
- Validate the affected behavior using the commands and change-specific guidance in `CONTRIBUTING.md`. Report what actually ran and any remaining verification gaps.
- Do not change real game files or user settings to exercise repository tests. Keep original logs private and generated artifacts out of commits.
- Update versions only as part of a requested release. Keep all version-bearing files synchronized as described in `CONTRIBUTING.md`.
- Keep these product-specific rules here. `CLAUDE.md` points to this file rather than maintaining a second copy.

## Monitoring UI conventions

- Put seasonal or event-only enemy types in the **Events** section below the boss cards in the left column. Infected (`infectedScreamer`) is the first example. Do not add event mobs to the right AI PMC / Scav / Rogue / Raider column.
- Register new event cards through `MainWindow.RenderEventMobs` and reuse `RenderEventMobCard`. Keep map/event eligibility in `RenderEventMobs`, and each enemy type's tracking state separate in `RaidState`. Include new state in death matching, raid-end handling, and the render signature as needed.
- Halloween styling is opt-in for the `infectedScreamer` card only. Keep other event cards (including infected Tagilla), the Events heading, and the shared app theme unchanged when extending this decoration.
- `infectedTagilla` belongs to Events, separately from ordinary `bossTagilla`. Show its card when a spawn decision or activation is logged. A probability alone does not confirm activation: keep Planned / NotPlanned / Confirmed distinct, and include actual activations in death matching and raid-end handling.
- Localize user-visible text for Korean, English and Simplified Chinese through `I18n.T` and the English dictionary in `Localization.cs`.
- Keep the player's kill count prominent in the upper-right raid header. Raid IDs are internal correlation data; do not show them in the monitoring UI.
- Show BEAR and USEC together in one AI PMC card. Before activation, show Planned if either faction has a plan, NotPlanned if both have explicit negative decisions, and Unknown otherwise. Once activated, show combined life counts. Omit the redundant "Initial spawn" row.
- Omit routine "Monitoring additional spawns" and "Additional spawn monitoring ended" rows from all cards. Keep the PMC "Additional spawn planned" indication when a plan is recorded and the raid is active; continue tracking spawns normally.
- Emphasize estimated surviving Impossible PMC counts with their difficulty and count only, explicitly included in the total estimated survivors. Do not add a "High risk" label. After PMC activation, show a zero Impossible count as a small, muted row, including after raid end. Use neutral end-of-raid wording when Impossible deaths remain unconfirmed.
- Run `./test.ps1` after changing tracking or rendering. Use `./test.ps1 -IncludePrivateLogs` when the ignored archive in `work/test-logs` is available. Keep original logs private; commit only anonymized fixtures under `tests/fixtures`.
