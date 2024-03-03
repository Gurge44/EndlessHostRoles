using System.Collections.Generic;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

public class Gangster : RoleBase
{
    private const int Id = 2900;
    private static List<byte> playerIdList = [];

    private static OptionItem RecruitLimitOpt;
    public static OptionItem KillCooldown;
    public static OptionItem SheriffCanBeMadmate;
    public static OptionItem MayorCanBeMadmate;
    public static OptionItem NGuesserCanBeMadmate;
    public static OptionItem JudgeCanBeMadmate;
    public static OptionItem MarshallCanBeMadmate;
    public static OptionItem FarseerCanBeMadmate;
    //public static OptionItem RetributionistCanBeMadmate;

    public static void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Gangster);
        KillCooldown = FloatOptionItem.Create(Id + 10, "GangsterRecruitCooldown", new(0f, 60f, 2.5f), 7.5f, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster])
            .SetValueFormat(OptionFormat.Seconds);
        RecruitLimitOpt = IntegerOptionItem.Create(Id + 12, "GangsterRecruitLimit", new(1, 5, 1), 1, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster])
            .SetValueFormat(OptionFormat.Times);

        SheriffCanBeMadmate = BooleanOptionItem.Create(Id + 14, "GanSheriffCanBeMadmate", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        MayorCanBeMadmate = BooleanOptionItem.Create(Id + 15, "GanMayorCanBeMadmate", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        NGuesserCanBeMadmate = BooleanOptionItem.Create(Id + 16, "GanNGuesserCanBeMadmate", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        JudgeCanBeMadmate = BooleanOptionItem.Create(Id + 17, "GanJudgeCanBeMadmate", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        MarshallCanBeMadmate = BooleanOptionItem.Create(Id + 18, "GanMarshallCanBeMadmate", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        FarseerCanBeMadmate = BooleanOptionItem.Create(Id + 19, "GanFarseerCanBeMadmate", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        //RetributionistCanBeMadmate = BooleanOptionItem.Create(Id + 20, "GanRetributionistCanBeMadmate", false, TabGroup.ImpostorRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);

    }

    public override void Init()
    {
        playerIdList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(RecruitLimitOpt.GetInt());
    }

    public override bool IsEnable => playerIdList.Count > 0;
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CanRecruit(id) ? KillCooldown.GetFloat() : Options.DefaultKillCooldown;
    public static bool CanRecruit(byte id) => id.GetAbilityUseLimit() > 0;

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton.OverrideText(CanRecruit(id) ? GetString("GangsterButtonText") : GetString("KillButtonText"));
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() < 1) return true;
        if (CanBeMadmate(target))
        {
            if (!killer.Is(CustomRoles.Recruit) && !killer.Is(CustomRoles.Charmed) && !killer.Is(CustomRoles.Contagious))
            {
                killer.RpcRemoveAbilityUse();
                target.RpcSetCustomRole(CustomRoles.Madmate);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Gangster), GetString("GangsterSuccessfullyRecruited")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Gangster), GetString("BeRecruitedByGangster")));
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

                killer.ResetKillCooldown();
                killer.SyncSettings();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole() + " + " + CustomRoles.Madmate, "Assign " + CustomRoles.Madmate);
                if (killer.GetAbilityUseLimit() < 0)
                    HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{killer.GetAbilityUseLimit()}次招募机会", "Gangster");
                return false;
            }
            if (killer.Is(CustomRoles.Recruit))
            {
                killer.RpcRemoveAbilityUse();
                target.RpcSetCustomRole(CustomRoles.Recruit);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Recruit), GetString("GangsterSuccessfullyRecruited")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Recruit), GetString("BeRecruitedByGangster")));
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

                killer.ResetKillCooldown();
                killer.SyncSettings();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole() + " + " + CustomRoles.Recruit, "Assign " + CustomRoles.Recruit);
                if (killer.GetAbilityUseLimit() < 0)
                    HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{killer.GetAbilityUseLimit()}次招募机会", "Gangster");
                return false;
            }
            if (killer.Is(CustomRoles.Charmed))
            {
                killer.RpcRemoveAbilityUse();
                target.RpcSetCustomRole(CustomRoles.Charmed);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Charmed), GetString("GangsterSuccessfullyRecruited")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Charmed), GetString("BeRecruitedByGangster")));
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

                killer.ResetKillCooldown();
                killer.SyncSettings();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole() + " + " + CustomRoles.Charmed, "Assign " + CustomRoles.Charmed);
                if (killer.GetAbilityUseLimit() < 0)
                    HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{killer.GetAbilityUseLimit()}次招募机会", "Gangster");
                return false;
            }
            if (killer.Is(CustomRoles.Contagious))
            {
                killer.RpcRemoveAbilityUse();
                target.RpcSetCustomRole(CustomRoles.Contagious);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Contagious), GetString("GangsterSuccessfullyRecruited")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Contagious), GetString("BeRecruitedByGangster")));
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

                killer.ResetKillCooldown();
                killer.SyncSettings();
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole() + " + " + CustomRoles.Contagious, "Assign " + CustomRoles.Contagious);
                if (killer.GetAbilityUseLimit() < 0)
                    HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{killer.GetAbilityUseLimit()}次招募机会", "Gangster");
                return false;
            }
        }

        if (killer.GetAbilityUseLimit() < 0)
            HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");
        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Gangster), GetString("GangsterRecruitmentFailure")));
        Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()} : 剩余{killer.GetAbilityUseLimit()}次招募机会", "Gangster");
        return true;
    }

    public static bool CanBeMadmate(PlayerControl pc)
    {
        return pc != null && pc.GetCustomRole().IsCrewmate() && !pc.Is(CustomRoles.Madmate)
               && !(
                   (pc.Is(CustomRoles.Sheriff) && !SheriffCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.Mayor) && !MayorCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.NiceGuesser) && !NGuesserCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.Judge) && !JudgeCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.Marshall) && !MarshallCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.Farseer) && !FarseerCanBeMadmate.GetBool()) ||
                   pc.Is(CustomRoles.NiceSwapper) ||
                   pc.Is(CustomRoles.Snitch) ||
                   pc.Is(CustomRoles.Needy) ||
                   pc.Is(CustomRoles.Lazy) ||
                   pc.Is(CustomRoles.Loyal) ||
                   pc.Is(CustomRoles.CyberStar) ||
                   pc.Is(CustomRoles.Demolitionist) ||
                   pc.Is(CustomRoles.NiceEraser) ||
                   pc.Is(CustomRoles.Egoist)
               );
    }
}