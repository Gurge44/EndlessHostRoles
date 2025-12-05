using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using static EHR.Options;

namespace EHR.Neutral;

public class Executioner : RoleBase
{
    private const int Id = 10700;
    private static List<byte> PlayerIdList = [];

    private static OptionItem CanTargetImpostor;
    private static OptionItem CanTargetNeutralKiller;
    private static OptionItem CanTargetNeutralBenign;
    private static OptionItem CanTargetNeutralEvil;
    private static OptionItem CanTargetCoven;
    public static OptionItem KnowTargetRole;
    public static OptionItem CanGuessTarget;
    public static OptionItem ChangeRolesAfterTargetKilled;

    public static Dictionary<byte, byte> Target = [];

    public static readonly CustomRoles[] CRoleChangeRoles =
    [
        CustomRoles.Amnesiac,
        CustomRoles.Maverick,
        CustomRoles.CrewmateEHR,
        CustomRoles.Jester,
        CustomRoles.Opportunist,
        CustomRoles.Convict,
        CustomRoles.SuperStar,
        CustomRoles.Bodyguard,
        CustomRoles.Dictator,
        CustomRoles.Doctor
    ];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Executioner);
        CanTargetImpostor = new BooleanOptionItem(Id + 10, "ExecutionerCanTargetImpostor", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Executioner]);
        CanTargetNeutralKiller = new BooleanOptionItem(Id + 12, "ExecutionerCanTargetNeutralKiller", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Executioner]);
        CanTargetNeutralBenign = new BooleanOptionItem(Id + 14, "CanTargetNeutralBenign", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Executioner]);
        CanTargetNeutralEvil = new BooleanOptionItem(Id + 15, "CanTargetNeutralEvil", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Executioner]);
        CanTargetCoven = new BooleanOptionItem(Id + 16, "CanTargetCoven", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Executioner]);
        KnowTargetRole = new BooleanOptionItem(Id + 13, "KnowTargetRole", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Executioner]);
        CanGuessTarget = new BooleanOptionItem(Id + 17, "CanGuessTarget", false, TabGroup.NeutralRoles).SetParent(KnowTargetRole);
        ChangeRolesAfterTargetKilled = new StringOptionItem(Id + 11, "ExecutionerChangeRolesAfterTargetKilled", CRoleChangeRoles.Select(x => x.ToColoredString()).ToArray(), 1, TabGroup.NeutralRoles, noTranslation: true).SetParent(CustomRoleSpawnChances[CustomRoles.Executioner]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        Target = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);

        if (AmongUsClient.Instance.AmHost)
        {
            LateTask.New(() =>
            {
                try
                {
                    List<PlayerControl> targetList = [];
                    targetList.AddRange(from target in Main.AllPlayerControls where playerId != target.PlayerId where CanTargetImpostor.GetBool() || !target.Is(CustomRoleTypes.Impostor) where CanTargetNeutralKiller.GetBool() || !target.IsNeutralKiller() where CanTargetNeutralBenign.GetBool() || !target.IsNeutralBenign() where CanTargetNeutralEvil.GetBool() || (!target.IsNeutralEvil() && !target.IsNeutralPariah()) where CanTargetCoven.GetBool() || !target.Is(CustomRoleTypes.Coven) where target.GetCustomRole() is not (CustomRoles.GM or CustomRoles.SuperStar) where Main.LoversPlayers.TrueForAll(x => x.PlayerId != playerId) select target);
                    targetList.AddRange(Main.AllPlayerControls.Where(x => x.IsCrewmate() && x.GetCustomRole() is not (CustomRoles.Swapper or CustomRoles.Mayor or CustomRoles.NiceGuesser or CustomRoles.Dictator)));
                    targetList.RemoveAll(x => x.GetCustomRole() is CustomRoles.Swapper or CustomRoles.Mayor or CustomRoles.NiceGuesser or CustomRoles.Dictator or CustomRoles.Executioner or CustomRoles.Terrorist or CustomRoles.GM);
                    if (!CanTargetNeutralBenign.GetBool() && !CanTargetNeutralEvil.GetBool() && !CanTargetNeutralKiller.GetBool()) targetList.RemoveAll(x => x.GetCustomRole().IsNeutral() || x.Is(CustomRoles.Bloodlust));

                    if (targetList.Count == 0)
                    {
                        LateTask.New(() => ChangeRole(Utils.GetPlayerById(playerId)), 8f, log: false);
                        return;
                    }

                    PlayerControl selectedTarget = targetList.RandomElement();
                    Target[playerId] = selectedTarget.PlayerId;
                    SendRPC(playerId, selectedTarget.PlayerId, "SetTarget");
                    Logger.Info($"{Utils.GetPlayerById(playerId)?.GetNameWithRole().RemoveHtmlTags()}'s target: {selectedTarget.GetNameWithRole().RemoveHtmlTags()}", "Executioner");
                }
                catch (Exception ex) { Logger.Error(ex.ToString(), "Executioner.Add"); }
            }, 0.5f, log: false);
        }
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
        Target.Remove(playerId);
    }

    public static void SendRPC(byte executionerId, byte targetId = 0x73, string Progress = "")
    {
        if (!AmongUsClient.Instance.AmHost) return;

        MessageWriter writer;

        switch (Progress)
        {
            case "SetTarget":
                writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetExecutionerTarget, SendOption.Reliable);
                writer.Write(executionerId);
                writer.Write(targetId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                break;
            case "":
                if (!AmongUsClient.Instance.AmHost) return;

                writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RemoveExecutionerTarget, SendOption.Reliable);
                writer.Write(executionerId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                break;
        }
    }

    public static void ReceiveRPC(MessageReader reader, bool SetTarget)
    {
        if (SetTarget)
        {
            byte ExecutionerId = reader.ReadByte();
            byte TargetId = reader.ReadByte();
            Target[ExecutionerId] = TargetId;
        }
        else
            Target.Remove(reader.ReadByte());
    }

    public static void ChangeRoleByTarget(PlayerControl target)
    {
        byte Executioner = Target.GetKeyByValue(target.PlayerId);
        PlayerControl ExePC = Utils.GetPlayerById(Executioner);
        CustomRoles newRole = CRoleChangeRoles[ChangeRolesAfterTargetKilled.GetValue()];
        ExePC.RpcSetCustomRole(newRole);
        ExePC.RpcChangeRoleBasis(newRole);
        Target.Remove(Executioner);
        SendRPC(Executioner);
        ExePC.Notify(Translator.GetString("ExecutionerRoleChanged"));
        Utils.NotifyRoles(SpecifySeer: ExePC, SpecifyTarget: target);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: ExePC);
    }

    private static void ChangeRole(PlayerControl executioner)
    {
        CustomRoles newRole = CRoleChangeRoles[ChangeRolesAfterTargetKilled.GetValue()];
        executioner.RpcSetCustomRole(newRole);
        executioner.RpcChangeRoleBasis(newRole);
        Target.Remove(executioner.PlayerId);
        SendRPC(executioner.PlayerId);
        executioner.Notify(Translator.GetString("ExecutionerRoleChanged"));
    }

    public override bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (base.KnowRole(player, target)) return true;

        if (!KnowTargetRole.GetBool()) return false;

        return player.Is(CustomRoles.Executioner) && Target.TryGetValue(player.PlayerId, out byte tar) && tar == target.PlayerId;
    }

    public static string TargetMark(PlayerControl seer, PlayerControl target)
    {
        bool GetValue = Target.TryGetValue(seer.PlayerId, out byte targetId) || (!seer.IsAlive() && Target.ContainsValue(target.PlayerId));
        return GetValue && (!seer.IsAlive() || targetId == target.PlayerId) ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Executioner), "♦") : string.Empty;
    }

    public static bool CheckExileTarget(NetworkedPlayerInfo exiled, bool Check = false)
    {
        foreach (KeyValuePair<byte, byte> kvp in Target.Where(x => x.Value == exiled.PlayerId))
        {
            PlayerControl executioner = Utils.GetPlayerById(kvp.Key);
            if (executioner == null || !executioner.IsAlive() || executioner.Data.Disconnected) continue;

            if (!Check) ExeWin(kvp.Key);

            return true;
        }

        return false;
    }

    private static void ExeWin(byte playerId)
    {
        CustomWinnerHolder.SetWinnerOrAdditonalWinner(CustomWinner.Executioner);
        CustomWinnerHolder.WinnerIds.Add(playerId);
    }
}