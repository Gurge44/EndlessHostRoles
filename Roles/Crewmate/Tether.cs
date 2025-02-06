using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

using static Options;

public class Tether : RoleBase
{
    private const int Id = 640300;
    public static List<byte> PlayerIdList = [];

    public static OptionItem VentCooldown;
    public static OptionItem UseLimitOpt;
    public static OptionItem TetherAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;
    public static OptionItem CancelVote;

    private byte Target = byte.MaxValue;
    private byte TetherId;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Tether);

        VentCooldown = new FloatOptionItem(Id + 10, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Tether])
            .SetValueFormat(OptionFormat.Seconds);

        UseLimitOpt = new IntegerOptionItem(Id + 11, "AbilityUseLimit", new(0, 20, 1), 1, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Tether])
            .SetValueFormat(OptionFormat.Times);

        TetherAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 12, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.4f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tether])
            .SetValueFormat(OptionFormat.Times);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 14, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tether])
            .SetValueFormat(OptionFormat.Times);

        CancelVote = CreateVoteCancellingUseSetting(Id + 13, CustomRoles.Tether, TabGroup.CrewmateRoles);
    }

    public override void Init()
    {
        PlayerIdList = [];
        Target = byte.MaxValue;
        TetherId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
        Target = byte.MaxValue;
        TetherId = playerId;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    private void SendRPCSyncTarget()
    {
        if (!IsEnable || !Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetTetherTarget, HazelExtensions.SendOption);
        writer.Write(TetherId);
        writer.Write(Target);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPCSyncTarget(MessageReader reader)
    {
        byte id = reader.ReadByte();
        if (Main.PlayerStates[id].Role is not Tether th) return;

        th.Target = reader.ReadByte();
    }

    public override void OnPet(PlayerControl pc)
    {
        Teleport(pc, 0, true);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        Teleport(pc, vent.Id);
    }

    private void Teleport(PlayerControl pc, int ventId, bool isPet = false)
    {
        if (pc == null) return;

        if (Target != byte.MaxValue)
        {
            LateTask.New(() =>
            {
                if (GameStates.IsInTask) pc.TP(Utils.GetPlayerById(Target).Pos());
            }, isPet ? 0.1f : 2f, "Tether TP");
        }
        else if (!isPet) LateTask.New(() => { pc.MyPhysics?.RpcBootFromVent(ventId); }, 0.5f, "Tether No Target Boot From Vent");
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (UsePets.GetBool()) return;

        AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override bool OnVote(PlayerControl pc, PlayerControl target)
    {
        if (pc == null || target == null || pc.PlayerId == target.PlayerId || Main.DontCancelVoteList.Contains(pc.PlayerId)) return false;

        if (pc.GetAbilityUseLimit() >= 1)
        {
            pc.RpcRemoveAbilityUse();
            Target = target.PlayerId;
            SendRPCSyncTarget();
            Main.DontCancelVoteList.Add(pc.PlayerId);
            return true;
        }

        return false;
    }

    public override void OnReportDeadBody()
    {
        Target = byte.MaxValue;
        SendRPCSyncTarget();
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        var sb = new StringBuilder();

        sb.Append(Utils.GetAbilityUseLimitDisplay(playerId, Target != byte.MaxValue));
        sb.Append(Utils.GetTaskCount(playerId, comms));

        return sb.ToString();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        return Target != byte.MaxValue && seer.PlayerId == target.PlayerId && seer.PlayerId == TetherId ? $"<color=#00ffa5>Target:</color> <color=#ffffff>{Utils.GetPlayerById(Target).GetRealName()}</color>" : string.Empty;
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }
}