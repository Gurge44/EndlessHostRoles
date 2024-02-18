using System.Collections.Generic;

namespace TOHE.Roles.Crewmate
{
    internal class Express
    {
        public static Dictionary<byte, long> SpeedUp = [];
        public static Dictionary<byte, float> SpeedNormal = [];

        public static void OnTaskComplete(PlayerControl player)
        {
            if (!SpeedUp.ContainsKey(player.PlayerId)) SpeedNormal[player.PlayerId] = Main.AllPlayerSpeed[player.PlayerId];
            Main.AllPlayerSpeed[player.PlayerId] = Options.ExpressSpeed.GetFloat();
            SpeedUp[player.PlayerId] = Utils.TimeStamp;
            player.MarkDirtySettings();
        }

        public static void OnFixedUpdate(PlayerControl player)
        {
            var playerId = player.PlayerId;
            var now = Utils.TimeStamp;
            if (SpeedUp.TryGetValue(playerId, out var etime) && etime + Options.ExpressSpeedDur.GetInt() < now)
            {
                SpeedUp.Remove(playerId);
                Main.AllPlayerSpeed[playerId] = SpeedNormal[playerId];
                player.MarkDirtySettings();
            }
        }
    }
}