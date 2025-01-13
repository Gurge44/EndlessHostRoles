namespace EHR.Crewmate;

internal class SpeedBooster : RoleBase
{
    public static bool On;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(649198, TabGroup.CrewmateRoles, CustomRoles.SpeedBooster);
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
        if (player.IsAlive() && completedTaskCount + 1 <= totalTaskCount)
        {
            PlayerControl target = Main.AllAlivePlayerControls.RandomElement();
            Main.AllPlayerSpeed[target.PlayerId] += 0.5f;

            target.Notify(Main.AllPlayerSpeed[target.PlayerId] > 3
                ? Translator.GetString("SpeedBoosterSpeedLimit")
                : string.Format(Translator.GetString("SpeedBoosterTaskDone"), Main.AllPlayerSpeed[player.PlayerId].ToString("0.0#####")));

            target.MarkDirtySettings();
        }
    }
}