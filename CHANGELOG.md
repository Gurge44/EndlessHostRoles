# Thank you so much for finding and using EHR!

> [!CAUTION]
> The latest Among Us update introduced a lot (and I mean a LOT) of issues with the base game itself, which also affects all mods. Expect some bugs! We did our best to fix the ones we could, but there are still some that we haven't figured out. Sorry for the inconvenience....

> [!NOTE]
> While at the time of releasing this, the mod should more-less work on InnerSloth's regions, **I still recommend playing on modded regions** (MEU/MAS/MNA/Niko-EU/Niko-AS/Niko-NA).

## Bug Fixes
- **Fixed most of the memory leaks introduced by the latest Among Us update**
- CTA data is now synced with non-host modded clients
- Fixed Generator not seeing its charge
- Fixed forged roles having incorrect colors
- Exorcist ability no longer kills ghosts
- + Mostly internal code-wise fixes

## Changes
- **The HTML log file now contains all data from the other log file (LogOutput.log)**
- More compatibility with [BAU](https://github.com/D1GQ/BetterAmongUs) - by Limeau
- Ghost roles use manual cooldown tracking instead of the protect button cooldown (fixes inconsistencies)
- Renaming commands now check the DenyName list
- Ventilation and Decontamination rooms are now available in room-based game modes
- Ventriloquist works again on InnerSloth's regions
- Improved how the lobby settings UI works overall (no more close & reopens required)
- Quick Chat messages are now handled by the mod just like normal text messages
- Improved the detection of 'start' spam
- Impostors don't see each other's progress texts when there is a Double Agent
- Pestilence is now immune to after-meeting deaths
- Chat character limit lowered to 1000 characters due to packet size limits
- New loading spinner icon

## Additions
- New setting under `Apply DenyName List`: `Ban instead of kick on trigger`

### New game mode: Doom Tag - by HayashiUme
- In the Doom Tag gamemode, every player is assigned a target, shown by an arrow.
- You can only kill your assigned target.
- Killing the wrong player will result in a punishment, such as death or reduced vision.
- When you eliminate your target, you inherit their target and continue the hunt.
- As more players are eliminated, the circle grows smaller and the pressure increases.
- When only a few players remain, Carnival Mode activates. All remaining players receive a speed boost and a reduced kill cooldown, turning the final showdown into a fast-paced battle for survival.
- Be the last player standing to win!

> [!WARNING]
> If you're joining a lobby hosted on vanilla regions (NA/EU/AS), please make sure you have a vanilla region selected in the Create Game menu, otherwise authentication will fail. ("Sabotage! The Among Us servers could not authenticate you!")
I'm saying this now, it's guaranteed that we'll get someone who skipped reading this complaining about this exact issue.

---