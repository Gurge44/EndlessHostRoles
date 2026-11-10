using AmongUs.GameOptions;
using EHR.Modules.Extensions;

namespace EHR.Roles;

public class Tundra : CovenBase
{
    public static bool On;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    private byte TundraId;

    private static OptionItem AbilityCooldown;
    private static OptionItem KillCooldown;
    private static OptionItem FrigidCountdown;
    private static OptionItem FreezeRange;

    public override void SetupCustomOption()
    {
        StartSetup(659900)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref FrigidCountdown, 10, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref FreezeRange, 2f, new FloatValueRule(0.1f, 10f, 0.1f), OptionFormat.Multiplier);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        TundraId = playerId;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return true;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = HasNecronomicon ? KillCooldown.GetFloat() : AbilityCooldown.GetFloat();
    }

    public override void OnReceiveNecronomicon()
    {
        SetKillCooldown(TundraId);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (HasNecronomicon) return true;
        Frigid(killer, target);
        killer.SetKillCooldown(AbilityCooldown.GetFloat());
        return false;
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        foreach (PlayerControl victim in FastVector2.GetPlayersInRange(killer.Pos(), FreezeRange.GetFloat(), x => x.PlayerId != killer.PlayerId && x.PlayerId != target.PlayerId))
            Frigid(killer, victim);
    }

    private static void Frigid(PlayerControl killer, PlayerControl target)
    {
        ReportDeadBodyPatch.CanReport[target.PlayerId] = false;
        Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
        target.MarkDirtySettings();
        _ = new CountdownTimer(FrigidCountdown.GetInt(), () =>
        {
            Main.AllPlayerSpeed[target.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            target.MarkDirtySettings();
            target.Suicide(PlayerState.DeathReason.Frozen, killer);
            ReportDeadBodyPatch.CanReport[target.PlayerId] = true;
        }, onCanceled: () =>
        {
            if (GameStates.IsEnded || GameStates.IsLobby || !GameStates.InGame) return;
            Main.AllPlayerSpeed[target.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            ReportDeadBodyPatch.CanReport[target.PlayerId] = true;
        });
    }
}