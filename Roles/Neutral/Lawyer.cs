using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using static EHR.Options;

namespace EHR.Neutral;

public class Lawyer : RoleBase
{
    private const int Id = 9900;
    private static List<byte> PlayerIdList = [];

    private static OptionItem CanTargetImpostor;
    private static OptionItem CanTargetNeutralKiller;
    private static OptionItem CanTargetCrewmate;
    private static OptionItem CanTargetCoven;
    private static OptionItem CanTargetJester;
    private static OptionItem ChangeRolesAfterTargetKilled;
    private static OptionItem KnowTargetRole;
    private static OptionItem TargetKnowsLawyer;

    public static Dictionary<byte, byte> Target = [];

    private static readonly CustomRoles[] CRoleChangeRoles =
    [
        CustomRoles.CrewmateEHR,
        CustomRoles.Jester,
        CustomRoles.Opportunist,
        CustomRoles.Convict,
        CustomRoles.SuperStar,
        CustomRoles.Bodyguard,
        CustomRoles.Dictator,
        CustomRoles.Mayor,
        CustomRoles.Doctor,
        CustomRoles.Amnesiac
    ];

    private byte LawyerId;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Lawyer);
        CanTargetImpostor = new BooleanOptionItem(Id + 10, "LawyerCanTargetImpostor", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        CanTargetNeutralKiller = new BooleanOptionItem(Id + 11, "LawyerCanTargetNeutralKiller", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        CanTargetCrewmate = new BooleanOptionItem(Id + 12, "LawyerCanTargetCrewmate", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        CanTargetCoven = new BooleanOptionItem(Id + 13, "LawyerCanTargetCoven", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        CanTargetJester = new BooleanOptionItem(Id + 14, "LawyerCanTargetJester", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        KnowTargetRole = new BooleanOptionItem(Id + 15, "KnowTargetRole", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        TargetKnowsLawyer = new BooleanOptionItem(Id + 16, "TargetKnowsLawyer", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        ChangeRolesAfterTargetKilled = new StringOptionItem(Id + 17, "LawyerChangeRolesAfterTargetKilled", CRoleChangeRoles.Select(x => x.ToColoredString()).ToArray(), 2, TabGroup.NeutralRoles, noTranslation: true).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        Target = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        LawyerId = playerId;

        LateTask.New(() =>
        {
            try
            {
                if (AmongUsClient.Instance.AmHost)
                {
                    List<PlayerControl> targetList = [];
                    targetList.AddRange(from target in Main.AllPlayerControls where playerId != target.PlayerId where CanTargetImpostor.GetBool() || !target.Is(CustomRoleTypes.Impostor) where CanTargetNeutralKiller.GetBool() || !target.IsNeutralKiller() where CanTargetCrewmate.GetBool() || !target.Is(CustomRoleTypes.Crewmate) where CanTargetCoven.GetBool() || !target.Is(CustomRoleTypes.Coven) where CanTargetJester.GetBool() || !target.Is(CustomRoles.Jester) where !target.Is(CustomRoleTypes.Neutral) || target.IsNeutralKiller() || target.Is(CustomRoles.Jester) where target.GetCustomRole() is not (CustomRoles.GM or CustomRoles.SuperStar or CustomRoles.Curser) where Main.LoversPlayers.TrueForAll(x => x.PlayerId != playerId) select target);

                    if (targetList.Count == 0)
                    {
                        ChangeRole(Utils.GetPlayerById(playerId));
                        return;
                    }

                    PlayerControl selectedTarget = targetList.RandomElement();
                    Target[playerId] = selectedTarget.PlayerId;
                    SendRPC(playerId, selectedTarget.PlayerId, "SetTarget");
                    Logger.Info($"{Utils.GetPlayerById(playerId)?.GetNameWithRole().RemoveHtmlTags()}:{selectedTarget.GetNameWithRole().RemoveHtmlTags()}", "Lawyer");

                    if (!TargetKnowsLawyer.GetBool()) return;
                    LateTask.New(() => selectedTarget.Notify(string.Format(Translator.GetString("YourLawyerIsNotify"), LawyerId.ColoredPlayerName())), 18f, log: false);
                }
            }
            catch (Exception ex) { Utils.ThrowException(ex); }
        }, 0.5f, log: false);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
        Target.Remove(playerId);
    }

    private static void SendRPC(byte lawyerId, byte targetId = 0x73, string Progress = "")
    {
        MessageWriter writer;

        switch (Progress)
        {
            case "SetTarget":
                writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetLawyerTarget, SendOption.Reliable);
                writer.Write(lawyerId);
                writer.Write(targetId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                break;
            case "":
                if (!AmongUsClient.Instance.AmHost) return;
                writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RemoveLawyerTarget, SendOption.Reliable);
                writer.Write(lawyerId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                break;
        }
    }

    public static void ReceiveRPC(MessageReader reader, bool SetTarget)
    {
        if (SetTarget)
        {
            byte LawyerId = reader.ReadByte();
            byte TargetId = reader.ReadByte();
            Target[LawyerId] = TargetId;
        }
        else
            Target.Remove(reader.ReadByte());
    }

    public static void ChangeRoleByTarget(PlayerControl target)
    {
        byte lawyerId = Target.GetKeyByValue(target.PlayerId);
        PlayerControl lawyer = Utils.GetPlayerById(lawyerId);
        CustomRoles newRole = CRoleChangeRoles[ChangeRolesAfterTargetKilled.GetValue()];
        lawyer.RpcSetCustomRole(newRole);
        lawyer.RpcChangeRoleBasis(newRole);
        Target.Remove(lawyerId);
        SendRPC(lawyerId);
        lawyer.Notify(Translator.GetString("LawyerChangeRole"));
        Utils.NotifyRoles(SpecifySeer: lawyer, SpecifyTarget: target);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: lawyer);
    }

    public override bool KnowRole(PlayerControl seer, PlayerControl target)
    {
        if (base.KnowRole(seer, target)) return true;

        if (TargetKnowsLawyer.GetBool() && Target.ContainsValue(seer.PlayerId) && Target.GetKeyByValue(seer.PlayerId) == target.PlayerId)
            return true;

        if (!seer.Is(CustomRoles.Lawyer) || !KnowTargetRole.GetBool()) return false;
        return Target.TryGetValue(seer.PlayerId, out byte tar) && tar == target.PlayerId;
    }

    public static string LawyerMark(PlayerControl seer, PlayerControl target)
    {
        if (!seer.Is(CustomRoles.Lawyer))
        {
            if (!TargetKnowsLawyer.GetBool() && seer.IsAlive()) return string.Empty;

            return Target.TryGetValue(target.PlayerId, out byte x) && (seer.PlayerId == x || !seer.IsAlive()) ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lawyer), "§") : string.Empty;
        }

        bool GetValue = Target.TryGetValue(seer.PlayerId, out byte targetId);
        return GetValue && targetId == target.PlayerId ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lawyer), "§") : string.Empty;
    }

    private static void ChangeRole(PlayerControl lawyer)
    {
        CustomRoles newRole = CRoleChangeRoles[ChangeRolesAfterTargetKilled.GetValue()];
        lawyer.RpcSetCustomRole(newRole);
        lawyer.RpcChangeRoleBasis(newRole);
        Target.Remove(lawyer.PlayerId);
        SendRPC(lawyer.PlayerId);
        lawyer.Notify(Translator.GetString("LawyerChangeRole"));
    }

    public override void OnReportDeadBody()
    {
        if (MeetingStates.FirstMeeting && TargetKnowsLawyer.GetBool() && Target.TryGetValue(LawyerId, out byte target))
            LateTask.New(() => Utils.SendMessage("\n", target, string.Format(Translator.GetString("YourLawyerIsNotify"), LawyerId.ColoredPlayerName())), 10f, log: false);
    }
}