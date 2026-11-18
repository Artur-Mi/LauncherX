# LauncherX

Vertical missile launcher tower (1x1x4) for the MisterWeapon modpack (Space Engineers 1). Has a script of its own for
visuals. **Requires the brain mod** ([missil-misterplace](../missil-misterplace/README.md)), which gives its missile the
SLBM behaviour; enable both in the world (dev with dev, final with final). See [docs/contents.md](docs/contents.md).

| Target | Local folder in `%APPDATA%\SpaceEngineers\Mods` | Workshop ID |
|---|---|---|
| dev   | `LauncherX-dev` | none yet (created by the first dev Publish) |
| final | `LauncherX`     | none yet (not published) |

## Quick start
```powershell
.\deploy.ps1 dev            # junction Mods\LauncherX-dev -> Mod\ (live edits)
.\deploy.ps1 final -DryRun  # preview a release
.\deploy.ps1 final          # needs a clean git tree; copies a snapshot, tags release/<date>
```
Then publish from the game's Mods screen. Never load dev and final in the same world.
Deploy details, and what to do about IDs of unpublished targets:
[../missil-misterplace/docs/deployment.md](../missil-misterplace/docs/deployment.md).

## Layout
- `Mod/`: everything that ships (Data, Models, Textures)
- `deploy.json` / `deploy.ps1`: deploy targets and script
- `docs/`: documentation

## Docs
- [Contents](docs/contents.md)
