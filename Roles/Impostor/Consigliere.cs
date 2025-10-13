using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using static EHR.Options;

namespace EHR.Impostor;

public class Consigliere : RoleBase
{
    private const int Id = 2700;
    public static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem RevealMaxCount;
    public static OptionItem ConsigliereAbilityUseGainWithEachKill;

    private bool CanVent;

    private List<byte> RevealTarget = [];
    private bool HasImpVision;
    private bool IsRitualist;
    private float KCD;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Consigliere);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 25f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Consigliere])
            .SetValueFormat(OptionFormat.Seconds);

        RevealMaxCount = new IntegerOptionItem(Id + 11, "RevealMaxCount", new(0, 15, 1), 1, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Consigliere])
            .SetValueFormat(OptionFormat.Times);

        ConsigliereAbilityUseGainWithEachKill = new FloatOptionItem(Id + 12, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.4f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Consigliere])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = [];
        RevealTarget = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        RevealTarget = [];

        IsRitualist = Main.PlayerStates[playerId].MainRole == CustomRoles.Ritualist;
        playerId.SetAbilityUseLimit(IsRitualist ? Ritualist.RitualMaxCount.GetInt() : RevealMaxCount.GetInt());

        if (IsRitualist)
        {
            KCD = Ritualist.KillCooldown.GetFloat();
            CanVent = Ritualist.CanVent.GetBool();
            HasImpVision = Ritualist.HasImpostorVision.GetBool();
        }
        else
        {
            KCD = KillCooldown.GetFloat();
            CanVent = true;
            HasImpVision = true;
        }
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(HasImpVision);
    }

    private static void SendRPC(byte playerId, byte targetId)
    {
        if (!Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetConsigliere, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(targetId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public void ReceiveRPC(byte targetId)
    {
        RevealTarget.Add(targetId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KCD;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        return killer.GetAbilityUseLimit() < 1 || killer.CheckDoubleTrigger(target, () => { SetReveal(killer, target); });
    }

    public bool IsReveal(byte target)
    {
        return RevealTarget.Contains(target);
    }

    public void SetReveal(PlayerControl killer, PlayerControl target)
    {
        if (!IsReveal(target.PlayerId))
        {
            killer.RpcRemoveAbilityUse();
            RevealTarget.Add(target.PlayerId);
            Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()}: Reveal target → {target.GetNameWithRole().RemoveHtmlTags()} || Remaining: {killer.GetAbilityUseLimit()} uses", "Consigliere");
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);

            SendRPC(killer.PlayerId, target.PlayerId);
            killer.SetKillCooldown();
        }
    }

    public override bool KnowRole(PlayerControl seer, PlayerControl target)
    {
        if (base.KnowRole(seer, target)) return true;

        return Main.PlayerStates[seer.PlayerId].Role is Consigliere ed && ed.RevealTarget.Contains(target.PlayerId);
    }
}