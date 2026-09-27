# FrizzQOL Build From Chest

Hammer placement spends wood and stone from your inventory first. Anything still missing comes from the nearest chest you can open. A closer chest owned by someone else is skipped.

Only wood and stone are pulled, and only for the vanilla hammer. Fine wood and other materials stay where they are. One chest is used per placement. If that chest is short, nothing is taken from it.

The pull happens on the hammer click. If that click does not place the piece, the items go back to the same chest. Chests are not searched while the build menu is open.

## Config

`BepInEx/config/com.frizzqol.buildfromchest.cfg`

| Setting | Default | What it does |
| --- | --- | --- |
| ChestRadius | 10 | Meters from you to the chest. 0 or less turns the pull off. |

## Multiplayer

Install this on every player. If you use a dedicated server, install it there too.

## Install

Install with r2modman or the Thunderstore Mod Manager.

To install by hand, copy `FrizzQOL.BuildFromChest.dll` into `BepInEx/plugins`.

## Requirements

- Valheim
- [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)

## License

[MIT](LICENSE). You can use, copy, change, and share this mod. Keep the copyright notice with any copy.

## Building

1. Copy `Environment.props.example` to `Environment.props`.
2. Set your Valheim and BepInEx folders in that file.
3. From this folder, run:

```
dotnet build BuildFromChest.sln -c Release
```

The plugin file is `FrizzQOL.BuildFromChest.dll`, under the project `bin\Release\net48` folder.

`Environment.props` stays on your machine. It is listed in `.gitignore`.

## Publishing

This folder is ready to push as its own public repository. Create an empty GitHub repo named `FrizzQOL.BuildFromChest`. Do not add a README, license, or gitignore on GitHub. Those files are already here. Then run:

```
git remote add origin https://github.com/<you>/FrizzQOL.BuildFromChest.git
git push -u origin main
```

Set `website_url` in `Package/manifest.json` to that repository before the Thunderstore upload.

## Thunderstore package

Zip these files from `Package` together with the Release dll:

- `manifest.json`
- `README.md`
- `CHANGELOG.md`
- `LICENSE`
- `icon.png`
- `FrizzQOL.BuildFromChest.dll`
