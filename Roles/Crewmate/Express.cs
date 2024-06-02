using System.Collections.Generic;

namespace EHR.Roles.Crewmate
{
    internal class Express : RoleBase
    {
        public static Dictionary<byte, long> SpeedUp = [];
        public static Dictionary<byte, float> SpeedNormal = [];

        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(5585, TabGroup.CrewmateRoles, CustomRoles.Express);
            Options.ExpressSpeed = new FloatOptionItem(5587, "ExpressSpeed", new(0.25f, 5f, 0.25f), 1.5f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Express])
                .SetValueFormat(OptionFormat.Multiplier);
            Options.ExpressSpeedDur = new IntegerOptionItem(5588, "ExpressSpeedDur", new(0, 90, 1), 5, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Express])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void OnTaskComplete(PlayerControl player, int completedTaskCount, int totalTaskCount)
        {
            if (!SpeedUp.ContainsKey(player.PlayerId)) SpeedNormal[player.PlayerId] = Main.AllPlayerSpeed[player.PlayerId];
            Main.AllPlayerSpeed[player.PlayerId] = Options.ExpressSpeed.GetFloat();
            SpeedUp[player.PlayerId] = Utils.TimeStamp;
            player.MarkDirtySettings();
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (!GameStates.IsInTask) return;

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