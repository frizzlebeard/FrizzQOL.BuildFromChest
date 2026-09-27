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

## Source

https://github.com/frizzlebeard/FrizzQOL.BuildFromChest


## Thunderstore package

A valid upload is a zip whose root contains `icon.png`, `README.md`, and `manifest.json`. `CHANGELOG.md` is optional and is included here. Copy `FrizzQOL.BuildFromChest.dll` from the Release build into `Package`, then zip the files themselves. Do not zip the `Package` folder. If the files sit inside a folder in the zip, Thunderstore rejects the package.

- `manifest.json`
- `README.md`
- `CHANGELOG.md`
- `icon.png`
- `LICENSE`
- `FrizzQOL.BuildFromChest.dll`

The dll belongs at the zip root. The mod manager installs those files under `BepInEx/plugins/<Team>-<PackageName>/`.

Before you upload, check the package readme in the [markdown preview](https://thunderstore.io/tools/markdown-preview/) and the manifest in the [manifest validator](https://thunderstore.io/tools/manifest-v1-validator/). The package rules are in [Creating a Package](https://wiki.thunderstore.io/mods/creating-a-package).
