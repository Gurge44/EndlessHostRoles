using System.Collections.Generic;
using static EHR.Translator;

namespace EHR.Impostor;

public class Gangster : RoleBase
{
    private const int Id = 2900;
    private static List<byte> PlayerIdList = [];

    private static OptionItem RecruitLimitOpt;
    public static OptionItem KillCooldown;
    public static OptionItem SheriffCanBeMadmate;
    public static OptionItem MayorCanBeMadmate;
    public static OptionItem NGuesserCanBeMadmate;
    public static OptionItem JudgeCanBeMadmate;
    public static OptionItem MarshallCanBeMadmate;
    public static OptionItem FarseerCanBeMadmate;
    public static OptionItem PresidentCanBeMadmate;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Gangster);

        KillCooldown = new FloatOptionItem(Id + 10, "GangsterRecruitCooldown", new(0f, 60f, 2.5f), 7.5f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster])
            .SetValueFormat(OptionFormat.Seconds);

        RecruitLimitOpt = new IntegerOptionItem(Id + 12, "GangsterRecruitLimit", new(1, 5, 1), 1, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster])
            .SetValueFormat(OptionFormat.Times);

        SheriffCanBeMadmate = new BooleanOptionItem(Id + 14, "GanSheriffCanBeMadmate", false, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        MayorCanBeMadmate = new BooleanOptionItem(Id + 15, "GanMayorCanBeMadmate", false, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        NGuesserCanBeMadmate = new BooleanOptionItem(Id + 16, "GanNGuesserCanBeMadmate", false, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        JudgeCanBeMadmate = new BooleanOptionItem(Id + 17, "GanJudgeCanBeMadmate", false, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        MarshallCanBeMadmate = new BooleanOptionItem(Id + 18, "GanMarshallCanBeMadmate", false, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        FarseerCanBeMadmate = new BooleanOptionItem(Id + 19, "GanFarseerCanBeMadmate", false, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
        PresidentCanBeMadmate = new BooleanOptionItem(Id + 20, "GanPresidentCanBeMadmate", false, TabGroup.ImpostorRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Gangster]);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(RecruitLimitOpt.GetInt());
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = CanRecruit(id) ? KillCooldown.GetFloat() : Options.DefaultKillCooldown;
    }

    public static bool CanRecruit(byte id)
    {
        return id.GetAbilityUseLimit() > 0;
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton.OverrideText(CanRecruit(id) ? GetString("GangsterButtonText") : GetString("KillButtonText"));
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() < 1) return true;

        if (CanBeMadmate(target))
        {
            if (!killer.GetCustomSubRoles().FindFirst(x => x.IsConverted(), out CustomRoles convertedAddon)) convertedAddon = CustomRoles.Madmate;

            killer.RpcRemoveAbilityUse();
            target.RpcSetCustomRole(convertedAddon);

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(convertedAddon), GetString("GangsterSuccessfullyRecruited")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(convertedAddon), GetString("BeRecruitedByGangster")));
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

            killer.ResetKillCooldown();
            killer.SyncSettings();
            killer.SetKillCooldown();
            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);

            Logger.Info($"SetRole: {target?.Data?.PlayerName} = {target.GetCustomRole()} + {convertedAddon}", $"Assign {convertedAddon}");
            if (killer.GetAbilityUseLimit() <= 0) HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");

            return false;
        }

        if (killer.GetAbilityUseLimit() < 0) HudManager.Instance.KillButton.OverrideText($"{GetString("KillButtonText")}");

        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Gangster), GetString("GangsterRecruitmentFailure")));
        return true;
    }

    private static bool CanBeMadmate(PlayerControl pc)
    {
        return pc != null && pc.IsCrewmate() && !pc.Is(CustomRoles.Madmate)
               && !(
                   (pc.Is(CustomRoles.Sheriff) && !SheriffCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.Mayor) && !MayorCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.NiceGuesser) && !NGuesserCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.Judge) && !JudgeCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.Marshall) && !MarshallCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.Farseer) && !FarseerCanBeMadmate.GetBool()) ||
                   (pc.Is(CustomRoles.President) && !PresidentCanBeMadmate.GetBool()) ||
                   pc.Is(CustomRoles.NiceSwapper) ||
                   pc.Is(CustomRoles.Speedrunner) ||
                   pc.Is(CustomRoles.Snitch) ||
                   pc.Is(CustomRoles.Needy) ||
                   pc.Is(CustomRoles.Lazy) ||
                   pc.Is(CustomRoles.Loyal) ||
                   pc.Is(CustomRoles.CyberStar) ||
                   pc.Is(CustomRoles.Egoist)
               );
    }
}