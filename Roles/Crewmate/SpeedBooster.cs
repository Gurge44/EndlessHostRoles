namespace EHR.Roles.Crewmate
{
    internal class SpeedBooster : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

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
            if (player.IsAlive() && ((completedTaskCount + 1) <= Options.SpeedBoosterTimes.GetInt()))
            {
                Main.AllPlayerSpeed[player.PlayerId] += Options.SpeedBoosterUpSpeed.GetFloat();
                player.Notify(Main.AllPlayerSpeed[player.PlayerId] > 3 ? Translator.GetString("SpeedBoosterSpeedLimit") : string.Format(Translator.GetString("SpeedBoosterTaskDone"), Main.AllPlayerSpeed[player.PlayerId].ToString("0.0#####")));
                player.MarkDirtySettings();
            }
        }
    }
}
