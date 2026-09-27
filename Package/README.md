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

## Source

https://github.com/frizzlebeard/FrizzQOL.BuildFromChest

## Install

Install with r2modman or the Thunderstore Mod Manager.

To install by hand, copy `FrizzQOL.BuildFromChest.dll` into `BepInEx/plugins`.

## Requirements

- Valheim
- [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)

## License

[MIT License](https://opensource.org/licenses/MIT). You can use, copy, change, and share this mod. The LICENSE file shipped with the package has the full text.
