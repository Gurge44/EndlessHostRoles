# Thank you so much for finding and using EHR!

> [!INFO]
> Updated to Among Us v2026.3.31 (v17.3)

> [!WARNING]
> **Use commands with a `/cmd ` prefix, otherwise your message gets leaked.** So if you were to type `/bt 4 snitch`, you would now type `/cmd bt 4 snitch`. The system will NOT spam the chat after command usage even if it would be required.

## Bug Fixes
- Fixed Custom Hide And Seek errors when the host uses /setrole with an unavailable role
- Fixed Mingle ending during killing process
- Fixed disasters spawning while some players still see the ejection screen in Natural Disasters
- Fixed sinkholes not being despawned properly in Natural Disasters when the disaster limit specified in settings is reached
- Fixed players getting stuck inside collapsed buildings in Natural Disasters
- Fixed Sprayer, Catcher CNOs not disappearing after meetings
- Fixed Skeld/Dleks Electrical bounds overlapping Storage's colliders
- Fixed after-death actions not being triggered after guesses if the game instantly ends due to it
- Fixed shapeshift-related issues (by TommyXL)
- Fixed april fools stuff (by TommyXL)
- Fixed host's messages not being logged (for chat spam & chat during game notifies)
- GM and Specators cannot be summoned by Summoner
- Fixed Hacker consuming ability uses when looking at physical admin map
- Fixed memory leak
- Medic's tasks don't reset if it uses the pet button as the kill button (by Hyper)
- Time Master & Stasis now reset player speeds to their original values instead of the global value
- Bloodmoon now always gives Loss Of Blood as the death reason

## Changes
- **More compatibility with Starlight** (should be stable at this point)
- Player points are counted individually in King Of The Zones (by Zypherus)
- Optimized Natural Disasters: Suffix cache & saved about 3/4 of all RPCs sent
- Reduced Snowdown vanish cooldown from 2s to 1s
- Reduced network pressure in Solo PVP
- Missing 'Notification' template no longer sends an error message
- Snowdown auto enables pet use, just like Bed Wars
- Many more small improvements

## Additions
- New setting for Capture The Flag: `Spawn Protection Time` (by Zypherus)
- New setting for Natural Disasters: `Disaster Spawn Mode` (`All On Players`/`All On Random Places`/`On Players & Random Places`)
- Death messages for others in Natural Disasters
- New client option: `Classic Mode` (by TommyXL)

---