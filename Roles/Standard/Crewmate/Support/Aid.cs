using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;

namespace EHR.Roles;

public class Aid : RoleBase
{
    private const int Id = 640200;
    public static Dictionary<byte, CountdownTimer> ShieldedPlayers = [];

    public static OptionItem AidDur;
    public static OptionItem AidCD;
    public static OptionItem TargetKnowsShield;
    public static OptionItem UseLimitOpt;
    public static OptionItem UsePet;
    private static bool On;
    private byte AidId;

    public byte TargetId;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Aid);

        AidCD = new FloatOptionItem(Id + 10, "AidCD", new(0f, 60f, 1f), 15f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
            .SetValueFormat(OptionFormat.Seconds);

        AidDur = new FloatOptionItem(Id + 11, "AidDur", new(0f, 60f, 1f), 10f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
            .SetValueFormat(OptionFormat.Seconds);

        TargetKnowsShield = new BooleanOptionItem(Id + 14, "AidTargetKnowsAboutShield", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid]);

        UseLimitOpt = new IntegerOptionItem(Id + 12, "AbilityUseLimit", new(1, 20, 1), 5, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
            .SetValueFormat(OptionFormat.Times);

        UsePet = Options.CreatePetUseSetting(Id + 13, CustomRoles.Aid);
    }

    public override void Init()
    {
        On = false;
        ShieldedPlayers = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        playerId.SetAbilityUseLimit(UseLimitOpt.GetFloat());
        TargetId = byte.MaxValue;
        AidId = playerId;
    }

    public override void SetKillCooldown(byte playerId)
    {
        Main.AllPlayerKillCooldown[playerId] = AidCD.GetInt();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.GetAbilityUseLimit() >= 1;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return pc.GetAbilityUseLimit() >= 1 && TargetId != byte.MaxValue;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        TargetId = target.PlayerId;
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        return false;
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (pc.GetAbilityUseLimit() >= 1 && TargetId != byte.MaxValue)
        {
            pc.RpcRemoveAbilityUse();
            PlayerControl target = Utils.GetPlayerById(TargetId);
            ShieldedPlayers[TargetId] = new CountdownTimer(AidDur.GetInt(), () =>
            {
                ShieldedPlayers.Remove(TargetId);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
            }, onTick: () =>
            {
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc, SendOption: SendOption.None);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target, SendOption: SendOption.None);
            }, onCanceled: () => ShieldedPlayers.Remove(TargetId));
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, TargetId);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: target);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
            TargetId = byte.MaxValue;
        }

        pc.MyPhysics?.RpcExitVent(vent.Id);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        byte id = reader.ReadByte();
        ShieldedPlayers[id] = new CountdownTimer(AidDur.GetInt(), () => ShieldedPlayers.Remove(id), onCanceled: () => ShieldedPlayers.Remove(id));
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud)) return string.Empty;

        if (TargetKnowsShield.GetBool() && ShieldedPlayers.TryGetValue(seer.PlayerId, out CountdownTimer timer))
        {
            int timeLeft = (int)Math.Ceiling(timer.Remaining.TotalSeconds);
            return string.Format(Translator.GetString("AidCounterSelf"), timeLeft);
        }

        return seer.PlayerId == AidId ? string.Join("\n", ShieldedPlayers.Select(x => string.Format(Translator.GetString("AidCounterTarget"), x.Key.ColoredPlayerName(), (int)Math.Ceiling(x.Value.Remaining.TotalSeconds)))) : string.Empty;
    }
}