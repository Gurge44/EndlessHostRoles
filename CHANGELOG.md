# Thank you so much for finding and using EHR!

### Mod-side fixes

- Fixed another reason for kicks in Deathrace on vanilla regions
- Fixed not being able to create lobbies with more than 15 players even on modded regions
- Fixed Evader always being invisible after meetings
- Fixed Hypocrite being able to kill

### Major fixes

> [!CAUTION]
> - BepInEx version updated.
> - Il2CppInterop updated.
> - **A full reinstallation is necessary.** Sorry for the inconvenience. This is the only way to fix the current issues with the game. The reason for the crashes was not in the mod (aka. not in EHR.dll), but in Il2CppInterop. Pietro made a custom build of it that fixes those issues, which requires a newer version of BepInEx to work. So please understand that this is necessary.

> [!IMPORTANT]
> - We have also completed the _mono_ version of EHR. It requires a completely separate installation to work. If you download the Mono zip, it has an `INSTRUCTIONS.txt` which explains what to do to set it up.
> - Why was this needed, you might ask? As you've already seen, Il2CppInterop causes many, many problems. With the Mono version, the mod talks directly with the game, bypassing/skipping Il2CppInterop entirely. That means far fewer crashes.
> - Since it requires a separate installation, if you're using other mods along with EHR, they will not work. In that case, stick to the Il2Cpp version that you were using all along. Those mods would also need to update to support Mono to work alongside your Mono EHR installation.
> - Crossplay between Mono and Il2Cpp installations should work without any problems. I recommend using the Mono version if you're only using EHR (no other plugins).

---