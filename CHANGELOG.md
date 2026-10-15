# HS BackPack changelog

7 Days to Die 3.2–3.3. Install on the dedicated server and on every client.

**1.0.3** — Pager lives in the inventory header gap so it does not cover slots or the lock. Harmony now patches the real grid Init (the old Backpack.Init patch aborted every other patch, which is why the UI stayed at 1/1). Last page leftover cells are locked empty; every bag slot is reachable.

**1.0.2** — Pager moved under the grid so it no longer covers the lock. Page count uses the real bag size (45 default, more with a worn backpack), not the 40 on-screen cells.

**1.0.1** — Alt+G drops the worn backpack as a world pickup. The bag contents stay inside that item; pick it up and equip it again to unpack. Not a loot bag.

**1.0.0** — Backpack grid uses pages of 40 instead of scrolling. Dual DLL ports 3.3 wearable backpacks onto 3.2 with a new equipment slot. A 3.3-only paging DLL is shipped as `HS_BackPack_33.dll.off` so it does not load beside the dual build.
