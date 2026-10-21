# Duck Defender codes

Open **Settings → Codes** from the main menu, enter the four digits, then choose
**Apply**. Keep the leading zero in `0381`. Collection changes apply to your next run.

| Code | Effect |
| --- | --- |
| `1111` | **New:** adds one of every item to your account inventory (all 12 current items). Repeat to add another set. |
| `2002` | **New:** unlocks every collection card at level 2 and adds **1,000,000 gold** (1000k). Higher card levels, ascensions and banked copies are preserved. Repeat to add another million gold. |
| `8672` | Unlocks every collection card. Newly unlocked cards start at level 1; existing upgrades are preserved. |
| `3619` | Sets every collection card to its maximum level (currently 6) and ascends every eligible card. Clears banked duplicate copies. |
| `9845` | Enables unlimited gold and all four pack essences. Purchases stop spending these resources; this does not replace the displayed numeric balances. |
| `0381` | Unlocks every collection card and enables unlimited upgrade copies. Gold costs still apply unless unlimited resources are also enabled. |
| `5942` | Combines `3619` and `9845`: maximum levels, all eligible ascensions, unlimited gold and essence. Does not enable unlimited copies. |
| `2048` | Toggles the two-player keyboard/controller co-op test mode for Host. Enter it again to restore controller-only co-op. This setting is session-only. |

Collection codes affect the 47 collectible cards. The four Basic cards are run-only
upgrades and do not use persistent collection levels.

`1111` adds Bomb, Health Potion, Shield, Battle Banner, Pocket Casino, Clone,
Experience Potion, Flamethrower, Sawblade, Shock, Divine Intervention and Nuke.
Equip items through your loadout before starting a run; the code does not replace
your equipped items. Grants are saved immediately, and inventory/gold stop at
2,147,483,647 rather than overflowing.

The resource/copy flags remain enabled across restarts. The existing **Reset
Progress** action clears them together with all account progression; it is not a
separate code toggle. The code console is available in the Editor and Development
builds, and in release builds when `AllowInReleaseBuilds` is enabled on its component.
