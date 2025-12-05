using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

public class Medic : RoleBase
{
    private const int Id = 7100;
    public static List<byte> PlayerIdList = [];
    public static List<byte> ProtectList = [];
    public static List<byte> TempMarkProtectedList = [];
    public static int SkillLimit;

    private static int LocalPlayerTryKillShieldedTimes;

    public static OptionItem WhoCanSeeProtect;
    private static OptionItem KnowShieldBroken;
    private static OptionItem ShieldDeactivatesWhenMedicDies;
    private static OptionItem ShieldDeactivationIsVisible;
    private static OptionItem ShieldBreaksOnKillAttempt;
    private static OptionItem ShieldBreakIsVisible;
    private static OptionItem ResetCooldown;
    public static OptionItem GuesserIgnoreShield;
    public static OptionItem JudgingIgnoreShield;
    private static OptionItem AmountOfShields;
    public static OptionItem UsePet;
    public static OptionItem CD;

    public static readonly string[] MedicWhoCanSeeProtectName =
    [
        "SeeMedicAndTarget",
        "SeeMedic",
        "SeeTarget",
        "SeeNoone"
    ];

    public static readonly string[] ShieldDeactivationIsVisibleOption =
    [
        "DeactivationImmediately",
        "DeactivationAfterMeeting",
        "DeactivationIsVisibleOFF"
    ];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Medic);

        WhoCanSeeProtect = new StringOptionItem(Id + 2, "MedicWhoCanSeeProtect", MedicWhoCanSeeProtectName, 0, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic]);

        KnowShieldBroken = new StringOptionItem(Id + 3, "MedicKnowShieldBroken", MedicWhoCanSeeProtectName, 1, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic]);

        ShieldDeactivatesWhenMedicDies = new BooleanOptionItem(Id + 4, "MedicShieldDeactivatesWhenMedicDies", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic]);

        ShieldDeactivationIsVisible = new StringOptionItem(Id + 5, "MedicShielDeactivationIsVisible", ShieldDeactivationIsVisibleOption, 0, TabGroup.CrewmateRoles)
            .SetParent(ShieldDeactivatesWhenMedicDies);

        ShieldBreaksOnKillAttempt = new BooleanOptionItem(Id + 6, "MedicShieldBreaksOnKillAttempt", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic]);

        ShieldBreakIsVisible = new StringOptionItem(Id + 7, "MedicShieldBreakIsVisible", ShieldDeactivationIsVisibleOption, 0, TabGroup.CrewmateRoles)
            .SetParent(ShieldBreaksOnKillAttempt);

        ResetCooldown = new FloatOptionItem(Id + 8, "MedicResetCooldown", new(0f, 120f, 1f), 15f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic])
            .SetValueFormat(OptionFormat.Seconds);

        GuesserIgnoreShield = new BooleanOptionItem(Id + 9, "MedicShieldedCanBeGuessed", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic]);

        JudgingIgnoreShield = new BooleanOptionItem(Id + 10, "MedicShieldedCanBeJudged", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic]);

        AmountOfShields = new IntegerOptionItem(Id + 11, "MedicAmountOfShields", new(1, 14, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic]);

        UsePet = Options.CreatePetUseSetting(Id + 12, CustomRoles.Medic);

        CD = new FloatOptionItem(Id + 13, "AbilityCooldown", new(0f, 180f, 0.5f), 7.5f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
        ProtectList = [];
        TempMarkProtectedList = [];
        SkillLimit = AmountOfShields.GetInt();

        LocalPlayerTryKillShieldedTimes = 0;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(SkillLimit);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    private static void SendRPCForProtectList()
    {
        if (!Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetMedicalerProtectList, SendOption.Reliable);
        writer.Write(1);
        writer.Write(ProtectList.Count);
        ProtectList.ForEach(x => writer.Write(x));
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPCForProtectList(MessageReader reader)
    {
        if (reader.ReadInt32() != 1)
        {
            byte id = reader.ReadByte();
            Main.PlayerStates[id].InitTask(id.GetPlayer());
            return;
        }
        
        int count = reader.ReadInt32();
        ProtectList = [];
        for (var i = 0; i < count; i++) ProtectList.Add(reader.ReadByte());
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return !Main.PlayerStates[pc.PlayerId].IsDead
               && pc.GetAbilityUseLimit() >= 1;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = CD.GetFloat();
    }

    public static bool InProtect(byte id)
    {
        return ProtectList.Contains(id) && Main.PlayerStates.TryGetValue(id, out PlayerState ps) && !ps.IsDead;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return playerId.GetAbilityUseLimit() > 0 ? base.GetProgressText(playerId, comms) : Utils.GetTaskCount(playerId, comms);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null) return false;
        if (ProtectList.Contains(target.PlayerId)) return false;

        killer.RpcRemoveAbilityUse();

        ProtectList.Add(target.PlayerId);
        TempMarkProtectedList.Add(target.PlayerId);
        SendRPCForProtectList();

        killer.SetKillCooldown();

        switch (WhoCanSeeProtect.GetInt())
        {
            case 0:
                killer.RPCPlayCustomSound("Shield");
                target.RPCPlayCustomSound("Shield");
                break;
            case 1:
                killer.RPCPlayCustomSound("Shield");
                break;
            case 2:
                target.RPCPlayCustomSound("Shield");
                break;
        }

        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

        if (target.AmOwner && target.Is(CustomRoles.Snitch))
        {
            if (WhoCanSeeProtect.GetInt() is 1 or 3) Achievements.Type.ImUnstoppable.CompleteAfterGameEnd();
            else Achievements.Type.ImUnstoppable.Complete();
        }

        if (killer.GetAbilityUseLimit() < 1f)
        {
            killer.RpcChangeRoleBasis(CustomRoles.CrewmateEHR);
            killer.RpcResetTasks();

            if (killer.IsNonHostModdedClient())
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetMedicalerProtectList, SendOption.Reliable);
                writer.Write(2);
                writer.Write(killer.PlayerId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
            }
        }

        return false;
    }

    public static bool OnAnyoneCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null) return false;
        if (!ProtectList.Contains(target.PlayerId)) return false;

        killer.SetKillCooldown(ResetCooldown.GetFloat());
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

        var medics = Main.AllPlayerControls.Where(x => PlayerIdList.Contains(x.PlayerId) && x.IsAlive()).ToArray();

        switch (KnowShieldBroken.GetInt())
        {
            case 0:
                target.RpcGuardAndKill();
                medics.Do(x => x.KillFlash());
                medics.NotifyPlayers(Translator.GetString("MedicKillerTryBrokenShieldTargetForMedic"));
                Main.AllPlayerControls.Where(x => ProtectList.Contains(x.PlayerId)).NotifyPlayers(Translator.GetString("MedicKillerTryBrokenShieldTargetForTarget"));
                break;
            case 1:
                medics.Do(x => x.KillFlash());
                medics.NotifyPlayers(Translator.GetString("MedicKillerTryBrokenShieldTargetForMedic"));
                break;
            case 2:
                target.RpcGuardAndKill();
                Main.AllPlayerControls.Where(x => ProtectList.Contains(x.PlayerId)).NotifyPlayers(Translator.GetString("MedicKillerTryBrokenShieldTargetForTarget"));
                break;
        }

        if (ShieldBreaksOnKillAttempt.GetBool())
        {
            ProtectList.Remove(target.PlayerId);
            SendRPCForProtectList();

            if ((Visible)ShieldBreakIsVisible.GetInt() == Visible.Immediately)
            {
                TempMarkProtectedList.Remove(target.PlayerId);
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            }
        }

        if (killer.AmOwner)
        {
            LocalPlayerTryKillShieldedTimes++;
            if (LocalPlayerTryKillShieldedTimes >= 2) Achievements.Type.DiePleaseDie.CompleteAfterGameEnd();
        }

        return true;
    }

    public static void OnCheckMark()
    {
        var notify = false;
        notify |= CheckMedicDeath();
        notify |= CheckShieldBreak();

        if (notify)
        {
            foreach (byte id in PlayerIdList)
            {
                foreach (byte id2 in ProtectList)
                    Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(id), SpecifyTarget: Utils.GetPlayerById(id2));
            }
        }
    }

    private static bool CheckMedicDeath()
    {
        if (!ShieldDeactivatesWhenMedicDies.GetBool()) return false;

        if ((Visible)ShieldDeactivationIsVisible.GetInt() == Visible.AfterMeeting)
        {
            TempMarkProtectedList = [];
            return true;
        }

        return false;
    }

    private static bool CheckShieldBreak()
    {
        if (!ShieldBreaksOnKillAttempt.GetBool()) return false;

        if ((Visible)ShieldBreakIsVisible.GetInt() == Visible.AfterMeeting)
        {
            TempMarkProtectedList = [];
            return true;
        }

        return false;
    }

    public static void IsDead(PlayerControl target)
    {
        if (!target.Is(CustomRoles.Medic)) return;

        if (!ShieldDeactivatesWhenMedicDies.GetBool()) return;

        ProtectList.Clear();
        SendRPCForProtectList();
        Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} : Medic is dead", "Medic");

        if ((Visible)ShieldDeactivationIsVisible.GetInt() == Visible.Immediately)
        {
            TempMarkProtectedList = [];

            foreach (byte id in ProtectList) Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(id), SpecifyTarget: target);
        }
    }

    public static string GetMark(PlayerControl seer, PlayerControl target)
    {
        if (ProtectList.Count > 0)
        {
            var shieldMark = $"<color={Utils.GetRoleColorCode(CustomRoles.Medic)}> ●</color>";

            bool self = seer.PlayerId == target.PlayerId;
            bool seerIsMedic = seer.Is(CustomRoles.Medic);
            bool targetProtected = InProtect(target.PlayerId);
            bool seerProtected = InProtect(seer.PlayerId);
            bool targetSeesProtection = WhoCanSeeProtect.GetInt() is 0 or 2;
            bool medicSeesProtection = WhoCanSeeProtect.GetInt() is 0 or 1;
            bool seerHasMark = TempMarkProtectedList.Contains(seer.PlayerId);
            bool targetHasMark = TempMarkProtectedList.Contains(target.PlayerId);

            if (self && (seerProtected || seerHasMark) && targetSeesProtection || seerIsMedic && (targetProtected || targetHasMark) && medicSeesProtection || !seer.IsAlive() && targetProtected && !seerIsMedic)
                return shieldMark;
        }

        return string.Empty;
    }

    private enum Visible
    {
        Immediately,
        AfterMeeting,
        Off
    }
}