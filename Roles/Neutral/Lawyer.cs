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
    public static List<byte> playerIdList = [];

    private static OptionItem CanTargetImpostor;
    private static OptionItem CanTargetNeutralKiller;
    private static OptionItem CanTargetCrewmate;
    private static OptionItem CanTargetJester;
    public static OptionItem ChangeRolesAfterTargetKilled;
    public static OptionItem KnowTargetRole;
    public static OptionItem TargetKnowsLawyer;

    public static Dictionary<byte, byte> Target = [];

    public static readonly CustomRoles[] CRoleChangeRoles =
    [
        CustomRoles.CrewmateEHR,
        CustomRoles.Jester,
        CustomRoles.Opportunist,
        CustomRoles.Convict,
        CustomRoles.CyberStar,
        CustomRoles.Bodyguard,
        CustomRoles.Dictator,
        CustomRoles.Mayor,
        CustomRoles.Doctor,
        CustomRoles.Amnesiac
    ];

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Lawyer);
        CanTargetImpostor = new BooleanOptionItem(Id + 10, "LawyerCanTargetImpostor", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        CanTargetNeutralKiller = new BooleanOptionItem(Id + 11, "LawyerCanTargetNeutralKiller", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        CanTargetCrewmate = new BooleanOptionItem(Id + 12, "LawyerCanTargetCrewmate", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        CanTargetJester = new BooleanOptionItem(Id + 13, "LawyerCanTargetJester", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        KnowTargetRole = new BooleanOptionItem(Id + 14, "KnowTargetRole", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        TargetKnowsLawyer = new BooleanOptionItem(Id + 15, "TargetKnowsLawyer", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
        ChangeRolesAfterTargetKilled = new StringOptionItem(Id + 16, "LawyerChangeRolesAfterTargetKilled", CRoleChangeRoles.Select(x => x.ToColoredString()).ToArray(), 2, TabGroup.NeutralRoles, noTranslation: true).SetParent(CustomRoleSpawnChances[CustomRoles.Lawyer]);
    }

    public override void Init()
    {
        playerIdList = [];
        Target = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        try
        {
            if (AmongUsClient.Instance.AmHost)
            {
                List<PlayerControl> targetList = [];
                targetList.AddRange(from target in Main.AllPlayerControls where playerId != target.PlayerId where CanTargetImpostor.GetBool() || !target.Is(CustomRoleTypes.Impostor) where CanTargetNeutralKiller.GetBool() || !target.IsNeutralKiller() where CanTargetCrewmate.GetBool() || !target.Is(CustomRoleTypes.Crewmate) where CanTargetJester.GetBool() || !target.Is(CustomRoles.Jester) where !target.Is(CustomRoleTypes.Neutral) || target.IsNeutralKiller() || target.Is(CustomRoles.Jester) where target.GetCustomRole() is not (CustomRoles.GM or CustomRoles.SuperStar) where Main.LoversPlayers.TrueForAll(x => x.PlayerId != playerId) select target);

                if (targetList.Count == 0)
                {
                    ChangeRole(Utils.GetPlayerById(playerId));
                    return;
                }

                var SelectedTarget = targetList.RandomElement();
                Target.Add(playerId, SelectedTarget.PlayerId);
                SendRPC(playerId, SelectedTarget.PlayerId, "SetTarget");
                Logger.Info($"{Utils.GetPlayerById(playerId)?.GetNameWithRole().RemoveHtmlTags()}:{SelectedTarget.GetNameWithRole().RemoveHtmlTags()}", "Lawyer");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex.ToString(), "Lawyer.Add");
        }
    }

    public static void SendRPC(byte lawyerId, byte targetId = 0x73, string Progress = "")
    {
        if (!Utils.DoRPC) return;
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
        byte Lawyer = 0x73;
        Target.Do(x =>
        {
            if (x.Value == target.PlayerId)
                Lawyer = x.Key;
        });
        PlayerControl lawyer = Utils.GetPlayerById(Lawyer);
        lawyer.RpcSetCustomRole(CRoleChangeRoles[ChangeRolesAfterTargetKilled.GetValue()]);
        Target.Remove(Lawyer);
        SendRPC(Lawyer);
        Utils.NotifyRoles(SpecifySeer: lawyer, SpecifyTarget: target);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: lawyer);
    }

    public override bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (!KnowTargetRole.GetBool()) return false;
        return player.Is(CustomRoles.Lawyer) && Target.TryGetValue(player.PlayerId, out var tar) && tar == target.PlayerId;
    }

    public static string LawyerMark(PlayerControl seer, PlayerControl target)
    {
        if (!seer.Is(CustomRoles.Lawyer))
        {
            if (!TargetKnowsLawyer.GetBool()) return string.Empty;
            return (Target.TryGetValue(target.PlayerId, out var x) && seer.PlayerId == x) ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lawyer), "§") : string.Empty;
        }

        var GetValue = Target.TryGetValue(seer.PlayerId, out var targetId);
        return GetValue && targetId == target.PlayerId ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lawyer), "§") : string.Empty;
    }

    public static void ChangeRole(PlayerControl lawyer)
    {
        lawyer.RpcSetCustomRole(CRoleChangeRoles[ChangeRolesAfterTargetKilled.GetValue()]);
        Target.Remove(lawyer.PlayerId);
        SendRPC(lawyer.PlayerId);
        var text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lawyer), Translator.GetString(""));
        text = string.Format(text, Utils.ColorString(Utils.GetRoleColor(CRoleChangeRoles[ChangeRolesAfterTargetKilled.GetValue()]), Translator.GetString(CRoleChangeRoles[ChangeRolesAfterTargetKilled.GetValue()].ToString())));
        lawyer.Notify(text);
    }

    public static bool CheckExileTarget(NetworkedPlayerInfo exiled /*, bool DecidedWinner, bool Check = false*/)
    {
        return Target.Where(x => x.Value == exiled.PlayerId).Select(kvp => Utils.GetPlayerById(kvp.Key)).Any(lawyer => lawyer != null && !lawyer.Data.Disconnected);
    }
}