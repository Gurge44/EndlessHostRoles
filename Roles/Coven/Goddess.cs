using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Coven;

public class Goddess : Coven
{
    public static bool On;

    private static OptionItem AbilityCooldown;
    private static OptionItem AbilityDuration;

    private long AbilityEndTS;

    private byte GoddessId;
    private long LastNotifyTS;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650060)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 10, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        AbilityEndTS = 0;
        LastNotifyTS = 0;
        GoddessId = playerId;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return HasNecronomicon;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        AbilityEndTS = Utils.TimeStamp + AbilityDuration.GetInt();
        Utils.SendRPC(CustomRPC.SyncRoleData, GoddessId, AbilityEndTS);
        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
        AURoleOptions.PhantomDuration = 1f;
    }

    public override void OnReportDeadBody()
    {
        AbilityEndTS = 0;
        Utils.SendRPC(CustomRPC.SyncRoleData, GoddessId, AbilityEndTS);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (AbilityEndTS > 0)
        {
            long now = Utils.TimeStamp;
            bool notify = now != LastNotifyTS;

            if (now >= AbilityEndTS)
            {
                AbilityEndTS = 0;
                Utils.SendRPC(CustomRPC.SyncRoleData, GoddessId, AbilityEndTS);
                pc.RpcResetAbilityCooldown();
                notify = true;
            }

            if (notify)
            {
                LastNotifyTS = now;
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }
        }
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (AbilityEndTS == 0 || killer.Is(CustomRoles.Pestilence)) return true;

        killer.SetRealKiller(target);
        target.Kill(killer);
        return false;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        AbilityEndTS = long.Parse(reader.ReadString());
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != GoddessId || seer.PlayerId != target.PlayerId || (seer.IsModClient() && !hud) || meeting || AbilityEndTS == 0) return string.Empty;
        return string.Format(Translator.GetString("Goddess.Suffix"), AbilityEndTS - Utils.TimeStamp, Main.CovenColor);
    }
}