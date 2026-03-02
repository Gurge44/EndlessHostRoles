using AmongUs.GameOptions;
using EHR.Modules.Extensions;

namespace EHR.Roles;

internal class Express : RoleBase
{
    private CountdownTimer Timer;

    public static bool On;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(653500, TabGroup.CrewmateRoles, CustomRoles.Express);

        Options.ExpressSpeed = new FloatOptionItem(653502, "ExpressSpeed", new(0.25f, 5f, 0.25f), 1.5f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Express])
            .SetValueFormat(OptionFormat.Multiplier);

        Options.ExpressSpeedDur = new IntegerOptionItem(653503, "ExpressSpeedDur", new(0, 90, 1), 5, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Express])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Add(byte playerId)
    {
        On = true;
        Timer = null;
    }

    public override void Init()
    {
        On = false;
    }

    public override void OnTaskComplete(PlayerControl player, int completedTaskCount, int totalTaskCount)
    {
        if (Timer == null)
        {
            Main.AllPlayerSpeed[player.PlayerId] = Options.ExpressSpeed.GetFloat();
            player.MarkDirtySettings();
        }
        else
            Timer.Dispose();
        
        Timer = new CountdownTimer(Options.ExpressSpeedDur.GetInt(), () =>
        {
            Timer = null;
            Main.AllPlayerSpeed[player.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            player.MarkDirtySettings();
        }, onCanceled: () =>
        {
            Timer = null;
            if (Main.RealOptionsData == null) return;
            Main.AllPlayerSpeed[player.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
        });
    }
}