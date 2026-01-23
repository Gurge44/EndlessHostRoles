# Thank you so much for finding and using EHR!

> [!IMPORTANT]
> InnerSloth added a way to use chat commands without needing to spam the chat after it to hide who typed it. In the next few weeks, please adapt to using commands with a `/cmd ` prefix. So if you were to type `/bt 4 snitch`, you would now type `/cmd bt 4 snitch`. This is the last version of EHR that allows the old way of typing commands.

> [!NOTE]
> - Custom Net Objects work again! All game modes and roles can be used again on vanilla servers!
> - HTML tags are supported on vanilla regions again! Just make sure they don't contain many numbers. Color tags are automatically replaced to ones without digits. Some visual differences may occur.
> - See more info on [our Discord server](https://discord.gg/ehr).

## Bug Fixes
- Fixed Comms Camouflage activating in non-standard game modes
- Fixed Room Rush sometimes not working
- Fixed chatting during game having weird notifications
- Deadlined and Alchemist info is now properly synced with non-host modded clients
- Fixed Imitator being able to imitate even after changing roles by other means
- Fixed Retributionist kill button availability not syncing with non-host modded clients
- Fixed Beacon sometimes not updating vision
- Fixed Chameleon being invisible infinitely
- Fixed Room Rusher showing incorrect rooms for non-host modded clients
- Fixed Starspawn ignoring ability cooldown and uses

## Changes
- More compatibility with LevelImpostor
- Augur no longer dies due to misguessing, instead it just can't guess in that meeting again
- Doppelganger's ID is visually swapped with their victim on the meeting UI. Does not affect `/id` for critical scenarios.
- Kill log timestamps are now relative to when the game started (timestamps may be removed entirely in the future, because it's so many digits)
- Increased target selection range through pet button
- Optimized Mastermind code

## Additions
- New role: Accumulator (Neutral Killing)
- New role: Quarry (Neutral Killing)
- New settings for Portal Maker:
- - `Ability Cooldown`
- - `Can Pet To Remove Both Portals After Placing Them`
- New setting (Mod Settings > `Camouflage During Comms`):
- - `Don't Camouflage Round 1`
- New setting (Mod Settings):
- - `Take Role Spawn Chances Into Account When Picking Draft Choices`

---