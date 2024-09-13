using System;
using AmongUs.Data;
using Discord;
using HarmonyLib;

namespace EHR.Patches
{
    // Originally from "Town of Us Rewritten", by Det
    [HarmonyPatch(typeof(ActivityManager), nameof(ActivityManager.UpdateActivity))]
    public class DiscordRPC
    {
        private static string Lobbycode = "";
        private static string Region = "";

        public static void Prefix([HarmonyArgument(0)] Activity activity)
        {
            if (activity == null) return;

            var details = $"EHR v{Main.PluginDisplayVersion}";
            activity.Details = details;

            try
            {
                if (activity.State != "In Menus")
                {
                    if (!DataManager.Settings.Gameplay.StreamerMode)
                    {
                        if (GameStates.IsLobby)
                        {
                            Lobbycode = GameStartManager.Instance.GameRoomNameCode.text;
                            Region = Utils.GetRegionName();
                        }

                        if (Lobbycode != "" && Region != "")
                        {
                            details = $"EHR - {Lobbycode} ({Region})";
                        }
                    }
                    else
                    {
                        details = $"EHR v{Main.PluginDisplayVersion}";
                    }

                    activity.Details = details;
                }
            }

            catch (ArgumentException ex)
            {
                Logger.Error("Error in updating discord rpc", "DiscordPatch");
                Logger.Exception(ex, "DiscordPatch");
                details = $"EHR v{Main.PluginDisplayVersion}";
                activity.Details = details;
            }
        }
    }
}