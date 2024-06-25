using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

public class Medic : RoleBase
{
    private const int Id = 7100;
    public static List<byte> playerIdList = [];
    public static List<byte> ProtectList = [];
    public static List<byte> TempMarkProtectedList = [];
    public static int SkillLimit;

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

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
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
        CD = new FloatOptionItem(Id + 13, "AbilityCooldown", new(0f, 180f, 2.5f), 7.5f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Medic])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        playerIdList = [];
        ProtectList = [];
        TempMarkProtectedList = [];
        SkillLimit = AmountOfShields.GetInt();
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(SkillLimit);

        if (!AmongUsClient.Instance.AmHost || (Options.UsePets.GetBool() && UsePet.GetBool())) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    private static void SendRPCForProtectList()
    {
        if (!Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetMedicalerProtectList, SendOption.Reliable);
        writer.Write(ProtectList.Count);
        ProtectList.Do(x => writer.Write(x));
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPCForProtectList(MessageReader reader)
    {
        int count = reader.ReadInt32();
        ProtectList = [];
        for (int i = 0; i < count; i++) ProtectList.Add(reader.ReadByte());
    }

    public override bool CanUseKillButton(PlayerControl pc)
        => !Main.PlayerStates[pc.PlayerId].IsDead
           && pc.GetAbilityUseLimit() >= 1;

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CanUseKillButton(Utils.GetPlayerById(id)) ? CD.GetFloat() : 300f;
    public static bool InProtect(byte id) => ProtectList.Contains(id) && Main.PlayerStates.TryGetValue(id, out var ps) && !ps.IsDead;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => false;
    public override void ApplyGameOptions(IGameOptions opt, byte playerId) => opt.SetVision(false);

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null) return false;
        if (!CanUseKillButton(killer)) return false;
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

        return false;
    }

    public static bool OnAnyoneCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null) return false;
        if (!ProtectList.Contains(target.PlayerId)) return false;

        killer.SetKillCooldown(ResetCooldown.GetFloat());

        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

        switch (KnowShieldBroken.GetInt())
        {
            case 0:
                target.RpcGuardAndKill(target);
                Main.AllPlayerControls.Where(x => playerIdList.Contains(x.PlayerId) && !x.Data.IsDead).Do(x => x.Notify(Translator.GetString("MedicKillerTryBrokenShieldTargetForMedic")));
                Main.AllPlayerControls.Where(x => ProtectList.Contains(x.PlayerId)).Do(x => x.Notify(Translator.GetString("MedicKillerTryBrokenShieldTargetForTarget")));
                break;
            case 1:
                Main.AllPlayerControls.Where(x => playerIdList.Contains(x.PlayerId) && !x.Data.IsDead).Do(x => x.Notify(Translator.GetString("MedicKillerTryBrokenShieldTargetForMedic")));
                break;
            case 2:
                target.RpcGuardAndKill(target);
                Main.AllPlayerControls.Where(x => ProtectList.Contains(x.PlayerId)).Do(x => x.Notify(Translator.GetString("MedicKillerTryBrokenShieldTargetForTarget")));
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

        return true;
    }

    public static void OnCheckMark()
    {
        bool notify = false;
        notify |= CheckMedicDeath();
        notify |= CheckShieldBreak();

        if (notify)
        {
            foreach (byte id in playerIdList)
            {
                foreach (byte id2 in ProtectList)
                {
                    Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(id), SpecifyTarget: Utils.GetPlayerById(id2));
                }
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

        Utils.NotifyRoles(SpecifySeer: target);

        ProtectList.Clear();
        SendRPCForProtectList();
        Logger.Info($"{target.GetNameWithRole().RemoveHtmlTags()} : Medic is dead", "Medic");

        if ((Visible)ShieldDeactivationIsVisible.GetInt() == Visible.Immediately)
        {
            TempMarkProtectedList = [];

            foreach (byte pc in ProtectList)
            {
                Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(pc), SpecifyTarget: target);
            }
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

            if (self && (seerProtected || seerHasMark) && targetSeesProtection)
                return shieldMark;

            if (seerIsMedic && (targetProtected || targetHasMark) && medicSeesProtection)
                return shieldMark;

            if (seer.Data.IsDead && targetProtected && !seerIsMedic)
                return shieldMark;
        }

        return string.Empty;
    }

    enum Visible
    {
        Immediately,
        AfterMeeting,
        OFF
    }
}