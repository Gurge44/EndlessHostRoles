# Thank you so much for finding and using EHR!

> [!NOTE]
> This release aims to fix most of the reasons for which you got kicked on InnerSloth's servers. However, no solution can be perfect, so there's still always a risk of getting randomly disconnected. Regardless of the measures I've coded to obey InnerSloth's new anti-cheat, **I still recommend playing on modded regions**.

That said, this release brings you HUGE fixes and improvements, both gameplay-wise and performance-wise!<br>

Thanks to TommyXL for his long dedicated work on making EHR run smoother on all kinds of devices! And thanks to everyone who helped testing this new release, we still wouldn't be able to put this out if you guys didn't exist.<br>
Of course, there can (and will) be bugs still that no one found, as we're humans, and humans make mistakes.<br>
Alright, let's get into it:

## Bug Fixes
- **Fixed anti-blackscreen measures not reverting after meetings** (aka. "Roles not loading", not being able to send chat messages as host, etc.)
- **Fixed Starlight players being able to NoClip in game**
- **Fixed everyone seeing role-specific CNOs for a split second after spawning them**
- **Fixed non-host modded clients not having their role set on game start**
- Fixed preset manager dialog disappearing instantly
- Synced Viper body dissolve time for all clients
- Fixed Bloodlust seeing impostors
- Fixed Room Rush showing int.MinValue as survival time for non-host modded clients on the end screen
- Mingle with Natural Disasters integrated now properly ends the game after players dying to disasters
- Fixed custom teams appearing when they're disabled
- Fixed Lazy only working when someone gets ejected
- Fixed Magistrate not changing player names in chat
- Fixed modded client Accumulator being able to do tasks while killing
- Fixed Investigator
- Fixed some Sheriff settings being unused
- Fixed Duellist (or their target) not teleporting back after the duel
- Fixed Hex Master
- Fixed Phantom being invisible for 0s instead of using the setting
- Fixed players' skins not reverting if Magistrate leaves during a court meeting
- Fixed Crusader dying not clearing the crusade effect on their targets
- Fixed King Of The Zones point counting sometimes not working
- Fixed Capitalist
- Fixed Merchant being shielded when they're not supposed to be, and vice versa
- Fixed all sabotages being disabled, not just the selected ones, if `Disable Sabotages` was enabled (doors sabotage too)
- Fixed lovers chat not clearing on meeting call if lovers died during the round
- Fixed some neutral benigns being uncategorized
- Fixed Venerer showing the player's name while camouflaged
- Fixed Stalker

## Changes & Improvements
- Improved compatibility with LevelImposter
- Simplified some CNO sprites quite a lot (to reduce packet sizes and thus lower network pressure)
- Players dying to disasters in Hot Potato (via integrating Natural Disasters into it) now have the correct survived time on the end screen
- Natural Disasters: Tornadoes and Tsunamis now always prefer moving in directions of players (hehe)
- Room Rush: Improved the time limits (wanna know how? I went and tested every single combination of rooms to go from and to on every single map, and recorded the time it took to get there)
- Room Rush: `Global Time Multiplier` => `Global Time Addition`
- Room Rush: Decontamination rooms, and Ventilation on AirShip can be chosen as rooms now
- Natural Disasters: CNOs are now pooled and reused/recycled, so players on weaker devices should not lag as much anymore
- Wiper can now use Vanish
- Tree is unguessable
- Coven members who have the Necronomicon now have a ♤ symbol next to their name
- Poll results only display the choices that got votes
- F1 info view text now auto-scales
- Client Control GUI no longer opens/closes via keybinds while the chat is open
- Victory conditions that trigger nearly at the same time are now both kept (they win along each other)
- Nobody can kill or use their ability at the start of the game for the duration specified by `Starting Kill Cooldown`

## Additions
- New role: Chainbinder (Impostor) (by Dechis)
- New role: Exorcist (Impostor)
- New role: Frightener (Impostor)
- New role: Obstructer (Impostor)
- New role: Operative (Crewmate)
- New role: Survivor (Crewmate) (by Newholiday)
- New role: Tar (Crewmate)
- New role: Blockade (Neutral)
- New role: Jackpot (Neutral) (by Dechis)
- New role: Shadow (Coven)
- New add-on: Absorber (by Newholiday)
- New add-on: Constricted (by Newholiday)
- New add-on: Dizzy
- New add-on: Entombed
- New add-on: Reroll (by Dechis)
- New add-on: Talkative
- New add-on: Urgent
- New ghost role: Meeting Angel
- New settings for `/anagram` by Zypherus: Language, Word Length, Difficulty
- New command by Zypherus: `/ppoll`/`/presetpoll` - Start a poll to vote for the next preset - Can be used by The Host And Moderators In Lobby
- New command by Zypherus: `/changepreset`/`/presetchange`/`/chp` - Switch the active preset to the specified preset number - Can be used by The Host And Moderators In Lobby

<br><br>
....and this changelog would be much, much longer if I listed every minor change too.

> [!WARNING]
> If you're joining a lobby hosted on vanilla regions (NA/EU/AS), please make sure you have a vanilla region selected in the Create Game menu, otherwise authentication will fail. ("Sabotage! The Among Us servers could not authenticate you!")
I'm saying this now, it's guaranteed that we'll get someone who skipped reading this complaining about this exact issue.

---