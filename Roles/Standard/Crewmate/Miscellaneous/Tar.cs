using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules.Extensions;

namespace EHR.Roles;

public class Tar : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    private static OptionItem AbilityCooldown;
    private static OptionItem AbilityDuration;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachTaskCompleted;
    private static OptionItem AbilityChargesWhenFinishedTasks;

    private CountdownTimer Timer;
    private byte TarId;
    private int ColorId;

    public override void SetupCustomOption()
    {
        StartSetup(658100)
            .AutoSetupOption(ref AbilityCooldown, 15, new IntegerValueRule(0, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 5, new IntegerValueRule(0, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachTaskCompleted, 0.5f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityChargesWhenFinishedTasks, 0.2f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Timer = null;
        TarId = playerId;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
        ColorId = Camouflage.PlayerSkins.GetValueOrDefault(playerId)?.ColorId ?? 0;
    }

    public override void OnPet(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1) return;
        pc.RpcRemoveAbilityUse();

        Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
        Main.PlayerStates[pc.PlayerId].IsBlackOut = true;
        pc.RpcSetColor(6);
        pc.MarkDirtySettings();

        Timer = new CountdownTimer(AbilityDuration.GetFloat(), () =>
        {
            Timer = null;
            if (!pc || !pc.IsAlive()) return;
            Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            Main.PlayerStates[pc.PlayerId].IsBlackOut = false;
            pc.RpcSetColor((byte)ColorId);
            pc.MarkDirtySettings();
        }, cancelOnMeeting: false, onCanceled: () =>
        {
            Timer = null;
            Main.AllPlayerSpeed[TarId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            Main.PlayerStates[TarId].IsBlackOut = false;
        });
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (Timer != null)
        {
            Vector2 pos = target.Pos();
            killer.TP(Main.LIMap ? ShipStatus.Instance.AllVents.MaxBy(x => Vector2.Distance(pos, x.transform.position)).transform.position : RandomSpawn.SpawnMap.GetSpawnMap().Positions.Values.MaxBy(x => Vector2.Distance(pos, x)));
            Timer.Complete();
            Timer = null;
            return false;
        }

        return true;
    }

    public override void OnReportDeadBody()
    {
        if (Timer != null)
        {
            Timer.Dispose();
            Timer = null;
            Main.AllPlayerSpeed[TarId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            Main.PlayerStates[TarId].IsBlackOut = false;
            TarId.GetPlayer()?.RpcSetColor((byte)ColorId);
        }
    }
}