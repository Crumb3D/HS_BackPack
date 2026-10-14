# HS BackPack

Paged player backpack (8×5 = 40 slots per page) instead of the 3.3 scroll list. Works on 7DTD **3.2** and **3.3**. Install on the dedicated server and on every client.

## Two DLLs

| File | What it is |
| --- | --- |
| `HS_BackPack.dll` | Dual build. Loads here. Paging on 3.2 and 3.3. On **3.2 only** it adds the 3.3 backpack items, crafting skill, magazine, and a real backpack equipment slot. |
| `HS_BackPack_33.dll.off` | 3.3-only paging. Renamed so it does **not** load next to the dual DLL. For a 3.3-only install, copy this to `HS_BackPack.dll` and do not ship the dual DLL. |

3.3 already has wearable backpacks. The dual DLL leaves those alone and only replaces the scrolling grid with pages.

## 3.2 backpacks

Small / medium / large backpacks from 3.3, using the vanilla sack hold mesh and the loot-bag icons (`cntBackpack01`, `cntBackpack03`, `cntDuffle01`). Equip them on the character window **Backpack** tab. Bag size grows with quality; extras past the new size drop as a loot bag.

Craft at the workbench after reading Pack Rat Magazine (`craftingBackpack`).

## Pages

Header pager, 40 visible slots, no scroll. Extra bag slots are on later pages.

## Drop backpack

**Alt+G** dumps the worn backpack on the ground as a pickup, with everything that was in it packed inside that item. You can run. Walk back, pick the backpack up, and put it on again to get the items back. It is not a loot crate.

`HSBackPackSettings.json` next to the mod (a copy in the user data folder `HSBackPack` overrides it):

```json
{ "DropKey": "G", "DropAlt": true, "DropLifetimeHours": 24 }
```

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
