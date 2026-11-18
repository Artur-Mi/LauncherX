# LauncherX

Space Engineers 1 mod: vertical missile launcher with a visuals script. Part of the MisterWeapon modpack; **requires** the brain [../missil-misterplace](../missil-misterplace/CLAUDE.md). Workspace overview: [../CLAUDE.md](../CLAUDE.md).

## Docs
- [README.md](README.md): quick start
- [docs/contents.md](docs/contents.md): what the mod defines, the script, relationship to the brain, gaps
- [../missil-misterplace/docs/deployment.md](../missil-misterplace/docs/deployment.md): dev/final deploy rules (shared by all mods)

## Rules
- Edit only inside `Mod/`. Never edit the copies in `<SE data>/Mods` (Windows: `%APPDATA%\SpaceEngineers\Mods`) directly.
- Scripts must compile under the game's Mod API: C# 7.3 and whitelisted APIs only. Use the `se-dev-mod` and
  `se-dev-game-code` skills to check APIs.
- Deploy with `.\deploy.ps1 dev`. Run `.\deploy.ps1 final` only when the user asks for a release.
- Neither target has a Workshop ID yet. `Mod/modinfo.sbmi` is generated and git-ignored; after a first publish,
  record the new ID in `deploy.json`.
- Large `.mwm`/`.dds` files go through Git LFS (see `.gitattributes`).
- Keep the docs current whenever behaviour, layout or the workflow changes.
