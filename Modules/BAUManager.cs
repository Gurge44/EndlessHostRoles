using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using System.ComponentModel;

namespace EHR.Modules;

internal class ModdedSupportExampleClass
{
    
    // These are the flags I would think would be the best for EHR
    // They can be changed, all flags listed here: https://github.com/D1GQ/BetterAmongUs/blob/main/src/Modules/Support/BAUModdedSupportFlags.cs
    [Category("bau:flags")]
    public static string[] BAUFlags = ["gameoption.disable.allgameoptions", "lobby.disable.customloadingbar", "gameplay.disable.customcolorblindtext", "client.disable.discordrp", "lobby.disable.cancelstartinggame", "gameplay.disable.betterrolealgorithm"];

    [Category("bau:event.bau_load")]
    public static bool OnBAULoad(BasePlugin bauPlugin)
    {
        return true;
    }
    
    [Category("bau:event.options_load")]
    public static void OnBAUOptionsLoaded(object[] options)
    {
    }
        
    [Category("bau:event.configs_load")]
    public static void OnBAUConfigEntriesLoaded(ConfigEntryBase[] configs)
    {
    }
}