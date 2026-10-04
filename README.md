# FrizzQOL Build From Chest

https://github.com/frizzlebeard/FrizzQOL.BuildFromChest

> The hammer spends the materials in your inventory first, then pulls the rest from chests you can open. 📦

---

## What it does

Place a piece with the vanilla hammer. Materials in your inventory are used first. Anything still missing comes from nearby chests.

- A closer chest you cannot open is skipped.
- Every material on the piece can be pulled, including fine wood, metal, and trophies.
- Chests in range are used nearest first. If they still cannot cover the cost, nothing is taken.
- The pull happens on the hammer click. If that click does not place the piece, the items go back to the same chests.
- Chests are left alone while the build menu is open.

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

## Support

☕ If you enjoy my work, please buy me a coffee.

Cash App: `$FrizzleFry4`

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
