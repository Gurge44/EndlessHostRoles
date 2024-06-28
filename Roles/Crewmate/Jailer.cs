using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate;

public class Jailor : RoleBase
{
    private const int Id = 63420;
    public static List<byte> playerIdList = [];

    public static OptionItem JailCooldown;
    public static OptionItem notifyJailedOnMeeting;
    public static OptionItem UsePet;
    public bool JailorDidVote;

    public byte JailorTarget;

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Jailor);
        JailCooldown = new FloatOptionItem(Id + 10, "JailorJailCooldown", new(0f, 60f, 1f), 15f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jailor])
            .SetValueFormat(OptionFormat.Seconds);
        notifyJailedOnMeeting = new BooleanOptionItem(Id + 18, "notifyJailedOnMeeting", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Jailor]);
        UsePet = CreatePetUseSetting(Id + 11, CustomRoles.Jailor);
    }

    public override void Init()
    {
        playerIdList = [];
        JailorTarget = byte.MaxValue;
        JailorDidVote = false;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        JailorTarget = byte.MaxValue;
        JailorDidVote = false;

        if (!AmongUsClient.Instance.AmHost || (UsePets.GetBool() && UsePet.GetBool())) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId) => opt.SetVision(false);
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Utils.GetPlayerById(id).IsAlive() ? JailCooldown.GetFloat() : 0f;
    public override bool CanUseKillButton(PlayerControl pc) => pc.IsAlive();

    void SendRPC(byte jailerId, byte targetId = byte.MaxValue, bool setTarget = true)
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
        return false;
    }

    public override void OnReportDeadBody()
    {
        if (!notifyJailedOnMeeting.GetBool()) return;
        if (JailorTarget == byte.MaxValue) return;
        var tpc = Utils.GetPlayerById(JailorTarget);
        if (tpc == null) return;

        if (tpc.IsAlive())
        {
            LateTask.New(() => { Utils.SendMessage(GetString("JailedNotifyMsg"), JailorTarget, title: Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jailor), GetString("JailorTitle"))); }, 0.3f, "JailorNotifyJailed");
        }
    }
}