using Hazel;
using System.Collections.Generic;
using TOHE.Roles.Crewmate;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public static class Amnesiac
{
    private static readonly int Id = 35000;
    private static List<byte> playerIdList = [];

    public static OptionItem RememberCooldown;
    public static OptionItem RefugeeKillCD;
    public static OptionItem IncompatibleNeutralMode;
    public static readonly string[] amnesiacIncompatibleNeutralMode =
    [
        "Role.Amnesiac",
        "Role.Pursuer",
        "Role.Follower",
        "Role.Maverick",
    ];

    private static int RememberLimit;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Amnesiac);
        RememberCooldown = FloatOptionItem.Create(Id + 10, "RememberCooldown", new(0f, 180f, 2.5f), 5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac])
            .SetValueFormat(OptionFormat.Seconds);
        RefugeeKillCD = FloatOptionItem.Create(Id + 11, "RefugeeKillCD", new(0f, 180f, 2.5f), 25f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac])
            .SetValueFormat(OptionFormat.Seconds);
        IncompatibleNeutralMode = StringOptionItem.Create(Id + 12, "IncompatibleNeutralMode", amnesiacIncompatibleNeutralMode, 0, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);
    }
    public static void Init()
    {
        playerIdList = [];
        RememberLimit = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        RememberLimit = 1;

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;

    private static void SendRPC()
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetRememberLimit, SendOption.Reliable, -1);
        writer.Write(RememberLimit);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader) => RememberLimit = reader.ReadInt32();
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = RememberLimit >= 1 ? RememberCooldown.GetFloat() : 300f;
    public static bool CanUseKillButton(PlayerControl player) => !player.Data.IsDead && RememberLimit >= 1;
    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (RememberLimit < 1) return;
        if (CanBeRememberedNeutralKiller(target))
        {
            RememberLimit--;
            SendRPC();
            killer.RpcSetCustomRole(target.GetCustomRole());

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedNeutralKiller")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            //killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);

            Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Soulless.ToString(), "Assign " + CustomRoles.Soulless.ToString());
            Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
            return;
        }
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacInvalidTarget")));
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");

        if (CanBeRememberedJackal(target))
        {
            RememberLimit--;
            SendRPC();
            killer.RpcSetCustomRole(CustomRoles.Sidekick);

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedNeutralKiller")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            //killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);

            Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Soulless.ToString(), "Assign " + CustomRoles.Soulless.ToString());
            Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
            return;
        }
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacInvalidTarget")));
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");

        if (CanBeRememberedNeutral(target))
        {
            if (IncompatibleNeutralMode.GetValue() == 0)
            {
                RememberLimit--;
                SendRPC();
                killer.RpcSetCustomRole(CustomRoles.Amnesiac);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedAmnesiac")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

                Add(killer.PlayerId);

                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Soulless.ToString(), "Assign " + CustomRoles.Soulless.ToString());
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
                return;
            }
            if (IncompatibleNeutralMode.GetValue() == 2)
            {
                RememberLimit--;
                SendRPC();
                killer.RpcSetCustomRole(CustomRoles.Pursuer);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedPursuer")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

                Pursuer.Add(killer.PlayerId);

                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Soulless.ToString(), "Assign " + CustomRoles.Soulless.ToString());
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
                return;
            }
            if (IncompatibleNeutralMode.GetValue() == 3)
            {
                RememberLimit--;
                SendRPC();
                killer.RpcSetCustomRole(CustomRoles.Totocalcio);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedFollower")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

                Totocalcio.Add(killer.PlayerId);

                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Soulless.ToString(), "Assign " + CustomRoles.Soulless.ToString());
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
                return;
            }
            if (IncompatibleNeutralMode.GetValue() == 4)
            {
                RememberLimit--;
                SendRPC();
                killer.RpcSetCustomRole(CustomRoles.Maverick);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedMaverick")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

                killer.ResetKillCooldown();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Soulless.ToString(), "Assign " + CustomRoles.Soulless.ToString());
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
                return;
            }
        }
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacInvalidTarget")));
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
        if (CanBeRememberedImpostor(target))
        {
            RememberLimit--;
            SendRPC();
            killer.RpcSetCustomRole(CustomRoles.Refugee);

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedImpostor")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            //killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);

            Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Soulless.ToString(), "Assign " + CustomRoles.Soulless.ToString());
            Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
            return;
        }
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacInvalidTarget")));
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
        if (CanBeRememberedCrewmate(target))
        {
            RememberLimit--;
            SendRPC();
            killer.RpcSetCustomRole(CustomRoles.Sheriff);

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedCrewmate")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

            Sheriff.Add(killer.PlayerId);

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            //killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);

            Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Soulless.ToString(), "Assign " + CustomRoles.Soulless.ToString());
            Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
            return;
        }
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacInvalidTarget")));
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
        if (CanBeRememberedPoisoner(target))
        {
            RememberLimit--;
            SendRPC();
            killer.RpcSetCustomRole(CustomRoles.Poisoner);

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedNeutralKiller")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

            Poisoner.Add(killer.PlayerId);

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            //killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);

            Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Soulless.ToString(), "Assign " + CustomRoles.Soulless.ToString());
            Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
            return;
        }
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacInvalidTarget")));
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
        if (CanBeRememberedJuggernaut(target))
        {
            RememberLimit--;
            SendRPC();
            killer.RpcSetCustomRole(CustomRoles.Juggernaut);

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedNeutralKiller")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

            Juggernaut.Add(killer.PlayerId);

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            //killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);

            Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Soulless.ToString(), "Assign " + CustomRoles.Soulless.ToString());
            Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
            return;
        }
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacInvalidTarget")));
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
        if (CanBeRememberedHexMaster(target))
        {
            RememberLimit--;
            SendRPC();
            killer.RpcSetCustomRole(CustomRoles.HexMaster);

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedNeutralKiller")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

            HexMaster.Add(killer.PlayerId);

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            //killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);

            Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Soulless.ToString(), "Assign " + CustomRoles.Soulless.ToString());
            Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
            return;
        }
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacInvalidTarget")));
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
        if (CanBeRememberedBloodKnight(target))
        {
            RememberLimit--;
            SendRPC();
            killer.RpcSetCustomRole(CustomRoles.BloodKnight);

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedNeutralKiller")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

            BloodKnight.Add(killer.PlayerId);

            killer.ResetKillCooldown();
            killer.SetKillCooldown();
            //killer.RpcGuardAndKill(target);
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);

            Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole().ToString() + " + " + CustomRoles.Soulless.ToString(), "Assign " + CustomRoles.Soulless.ToString());
            Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
            return;
        }
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacInvalidTarget")));
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{RememberLimit}次魅惑机会", "Amnesiac");
    }
    public static string GetRememberLimit() => Utils.ColorString(RememberLimit >= 1 ? Utils.GetRoleColor(CustomRoles.Amnesiac) : Color.gray, $"({RememberLimit})");
    public static bool CanBeRememberedNeutralKiller(this PlayerControl pc) => pc != null && pc.GetCustomRole().IsAmneNK();
    public static bool CanBeRememberedNeutral(this PlayerControl pc) => pc != null && pc.GetCustomRole().IsAmneMaverick();
    public static bool CanBeRememberedImpostor(this PlayerControl pc) => pc != null && (pc.GetCustomRole().IsImpostor() || pc.Is(CustomRoles.Madmate));
    public static bool CanBeRememberedCrewmate(this PlayerControl pc) => pc != null && pc.GetCustomRole().IsCrewmate() && !pc.Is(CustomRoles.Madmate);
    public static bool CanBeRememberedJackal(this PlayerControl pc) => pc != null && pc.Is(CustomRoles.Jackal);
    public static bool CanBeRememberedHexMaster(this PlayerControl pc) => pc != null && pc.Is(CustomRoles.HexMaster);
    public static bool CanBeRememberedPoisoner(this PlayerControl pc) => pc != null && pc.Is(CustomRoles.Poisoner);
    public static bool CanBeRememberedJuggernaut(this PlayerControl pc) => pc != null && pc.Is(CustomRoles.Juggernaut);
    public static bool CanBeRememberedBloodKnight(this PlayerControl pc) => pc != null && pc.Is(CustomRoles.BloodKnight);
    public static bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (player.IsNeutralKiller() && target.IsNeutralKiller() && player.GetCustomRole() == target.GetCustomRole()) return true;
        if (player.Is(CustomRoles.Refugee) && target.Is(CustomRoleTypes.Impostor)) return true;
        if (player.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Refugee)) return true;
        return false;
    }

}
