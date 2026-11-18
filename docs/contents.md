# Contents

| Item | Subtype | Notes |
|---|---|---|
| Block (`SmallMissileLauncher`) | `LauncherX` | "Launcher X", 1x1x4 vertical tower; model `Models\LauncherX\LauncherX.mwm` |
| Weapon definition | `LauncherX_Weapon` | Used by the block |
| Ammo, magazine, blueprint | `LauncherXMissile` | "Míssil Launcher X": speed 3 to 300, acceleration 30, explosion radius 11, damage 10000 |
| Water config | `WaterConfig.xml` | Empty |

All definitions are in `Mod/Data/LauncherX.sbc`. Models are in `Mod/Models/LauncherX/` (body, display missile, two door
parts), textures in `Mod/Textures/`.

## Script: `Mod/Data/Scripts/LauncherX/LauncherX.cs`

Visuals only: it does not aim or fire; the vanilla launcher logic does that.

- `LauncherXLogic` (game logic on the `LauncherX` block): sliding doors on top that open while the block is on and
  close when it is off or damaged; a display missile in the silo that rises after each shot.
- `LauncherXSession` (session component): tracks missiles launched from the block (flame and smoke), straightens the
  missile once it clears the tower, and spawns a large multi-stage explosion effect on impact.

## Relationship to the brain

The script does not call the brain, but the brain knows `LauncherXMissile`: it is **aquatic (SLBM) ammo**, so it gets
the SLBM underwater behaviour and guidance, and the brain's Iron Dome blocks can intercept it once it surfaces
([../../missil-misterplace/docs/architecture.md](../../missil-misterplace/docs/architecture.md)). The brain changes only
behaviour: the ammo's stats (damage, speed, acceleration, fire rate) stay defined here and were not changed. Without the
brain loaded, the missile flies as an ordinary unguided missile. Phase 4 of
[../../docs/modpack-plan.md](../../docs/modpack-plan.md) covers moving more shared logic into the brain.

Not copied from SLBM: the block is not restricted to underwater use (`WaterConfig.xml` is empty).

## Not set up yet
- No `metadata.mod` (the other mods have one).
- No Workshop item, dev or final.
- No block category entry (`BlockCategories.sbc`); check where the block shows in the G menu.
