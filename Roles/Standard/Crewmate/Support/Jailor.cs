using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles;

public class Jailor : RoleBase
{
    private const int Id = 63420;
    public static List<byte> PlayerIdList = [];

    private static OptionItem JailCooldown;
    private static OptionItem NotifyJailedOnMeeting;
    public static OptionItem UsePet;
    private bool JailorDidVote;

    private byte JailorId;
    public byte JailorTarget;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Jailor);

        JailCooldown = new FloatOptionItem(Id + 10, "JailorJailCooldown", new(0f, 60f, 1f), 15f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jailor])
            .SetValueFormat(OptionFormat.Seconds);

        NotifyJailedOnMeeting = new BooleanOptionItem(Id + 18, "notifyJailedOnMeeting", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jailor]);

        UsePet = CreatePetUseSetting(Id + 11, CustomRoles.Jailor);
    }

    public override void Init()
    {
        PlayerIdList = [];
        JailorTarget = byte.MaxValue;
        JailorDidVote = false;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        JailorTarget = byte.MaxValue;
        JailorDidVote = false;
        JailorId = playerId;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = Utils.GetPlayerById(id).IsAlive() ? JailCooldown.GetFloat() : 0f;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    private void SendRPC(byte jailerId, byte targetId = byte.MaxValue, bool setTarget = true)
    {
        if (!Utils.DoRPC) return;

        MessageWriter writer;

        if (!setTarget)
        {
            writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetJailorExeLimit, SendOption.Reliable);
            writer.Write(jailerId);
            writer.Write(JailorDidVote);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            return;
        }

        writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetJailorTarget, SendOption.Reliable);
        writer.Write(jailerId);
        writer.Write(targetId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader, bool setTarget = true)
    {
        byte jailerId = reader.ReadByte();
        if (Main.PlayerStates[jailerId].Role is not Jailor jl) return;

        if (!setTarget)
        {
            bool didvote = reader.ReadBoolean();
            jl.JailorDidVote = didvote;
        }

        byte targetId = reader.ReadByte();
        jl.JailorTarget = targetId;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null) return false;

        if (JailorTarget != byte.MaxValue)
        {
            killer.Notify(GetString("JailorTargetAlreadySelected"));
            return false;
        }

        JailorTarget = target.PlayerId;
        killer.Notify(GetString("SuccessfullyJailed"));
        killer.ResetKillCooldown();
        killer.SetKillCooldown();
        SendRPC(killer.PlayerId, target.PlayerId);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        return false;
    }

    public override void OnReportDeadBody()
    {
        if (!NotifyJailedOnMeeting.GetBool()) return;
        if (JailorTarget == byte.MaxValue) return;

        PlayerControl tpc = Utils.GetPlayerById(JailorTarget);
        if (tpc == null) return;

        if (tpc.IsAlive()) LateTask.New(() => Utils.SendMessage(GetString("JailedNotifyMsg"), JailorTarget, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle")), importance: MessageImportance.High), 0.3f, "JailorNotifyJailed");
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (hud || seer.PlayerId != JailorId || target.PlayerId != JailorTarget) return string.Empty;
        return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailedSuffix"));
    }
}