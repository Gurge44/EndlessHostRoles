using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate;

internal class Analyst : RoleBase
{
    private const int Id = 643100;
    private static byte PlayerId = byte.MaxValue;

    private static OptionItem UseLimitOpt;
    private static OptionItem CD;
    private static OptionItem Duration;
    private static OptionItem SeeKillCount;
    private static OptionItem SeeVentCount;
    private static OptionItem SeeRoleBasis;
    public static OptionItem UsePet;

    private static readonly Dictionary<string, string> ReplacementDict = new() { { "Analyze", Utils.ColorString(Utils.GetRoleColor(CustomRoles.Analyst), "Analyze") } };

    public static Dictionary<byte, int> VentCount = [];
    public (byte ID, long TIME) CurrentTarget = (byte.MaxValue, Utils.TimeStamp);

    public override bool IsEnable => PlayerId != byte.MaxValue;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Analyst);

        UseLimitOpt = new IntegerOptionItem(Id + 10, "AbilityUseLimit", new(0, 30, 1), 3, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Analyst])
            .SetValueFormat(OptionFormat.Times);

        CD = new FloatOptionItem(Id + 11, "AnalyzeCD", new(0f, 60f, 0.5f), 15f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Analyst])
            .SetValueFormat(OptionFormat.Seconds);

        CD.ReplacementDictionary = ReplacementDict;

        Duration = new IntegerOptionItem(Id + 12, "AnalyzeDur", new(1, 30, 1), 5, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Analyst])
            .SetValueFormat(OptionFormat.Seconds);

        Duration.ReplacementDictionary = ReplacementDict;

        SeeKillCount = new BooleanOptionItem(Id + 13, "AnalyzerSeeKillCount", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Analyst]);

        SeeVentCount = new BooleanOptionItem(Id + 14, "AnalyzerSeeVentCount", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Analyst]);

        SeeRoleBasis = new BooleanOptionItem(Id + 15, "AnalyzerSeeRoleBasis", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Analyst]);

        UsePet = CreatePetUseSetting(Id + 16, CustomRoles.Analyst);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return CurrentTarget.ID == byte.MaxValue && pc.GetAbilityUseLimit() > 0;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(false);
    }

    private static string GetRoleBasis(CustomRoles role)
    {
        return SeeRoleBasis.GetBool()
            ? role.GetVNRole(true) switch
            {
                CustomRoles.Impostor or CustomRoles.ImpostorEHR => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Impostor), GetString("Impostor")),
                CustomRoles.Shapeshifter or CustomRoles.ShapeshifterEHR => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Speedrunner), GetString("Shapeshifter")),
                CustomRoles.Phantom or CustomRoles.PhantomEHR => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Witness), GetString("Specter")),
                CustomRoles.Viper or CustomRoles.ViperEHR => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Poisoner), GetString("Viper")),
                CustomRoles.Crewmate or CustomRoles.CrewmateEHR => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Crewmate), GetString("Crewmate")),
                CustomRoles.Engineer or CustomRoles.EngineerEHR => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Autocrat), GetString("Engineer")),
                CustomRoles.Scientist or CustomRoles.ScientistEHR => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Doctor), GetString("Scientist")),
                CustomRoles.Noisemaker or CustomRoles.NoisemakerEHR => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Bubble), GetString("Noisemaker")),
                CustomRoles.Tracker or CustomRoles.TrackerEHR => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Scout), GetString("Tracker")),
                CustomRoles.Detective or CustomRoles.DetectiveEHR => Utils.ColorString(Utils.GetRoleColor(CustomRoles.Forensic), GetString("Detective")),
                _ => string.Empty
            }
            : string.Empty;
    }

    private static int GetKillCount(byte id)
    {
        return SeeKillCount.GetBool() ? Main.PlayerStates.Count(x => x.Value.GetRealKiller() == id) : 0;
    }

    private static int GetVentCount(byte id)
    {
        return SeeVentCount.GetBool() ? VentCount.GetValueOrDefault(id, 0) : 0;
    }

    private static string GetAnalyzeResult(PlayerControl pc)
    {
        return string.Format(GetString("AnalyzerResult"), pc.GetRealName().RemoveHtmlTags(), GetKillCount(pc.PlayerId), GetVentCount(pc.PlayerId), GetRoleBasis(pc.GetCustomRole()));
    }

    public override void Init()
    {
        PlayerId = byte.MaxValue;
        VentCount = [];
        CurrentTarget = (byte.MaxValue, Utils.TimeStamp);
    }

    public override void Add(byte id)
    {
        PlayerId = id;
        id.SetAbilityUseLimit(UseLimitOpt.GetInt());
        CurrentTarget = (byte.MaxValue, Utils.TimeStamp);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = CD.GetFloat();
    }

    public static void OnAnyoneEnterVent(PlayerControl pc)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        if (!VentCount.TryAdd(pc.PlayerId, 1))
            VentCount[pc.PlayerId]++;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!IsEnable) return false;
        if (killer == null || target == null) return false;
        if (killer.GetAbilityUseLimit() <= 0) return false;
        if (CurrentTarget.ID != byte.MaxValue) return false;

        CurrentTarget = (target.PlayerId, Utils.TimeStamp);
        killer.SetKillCooldown(Duration.GetFloat());
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);

        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!IsEnable) return;
        if (pc == null) return;
        if (CurrentTarget.ID == byte.MaxValue) return;

        PlayerControl target = Utils.GetPlayerById(CurrentTarget.ID);
        if (target == null) return;

        if (Vector2.Distance(target.Pos(), pc.Pos()) > (pc.Is(CustomRoles.Reach) ? 2.5f : 1.5f))
        {
            CurrentTarget.ID = byte.MaxValue;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: target);
            return;
        }

        if (CurrentTarget.TIME + Duration.GetInt() <= Utils.TimeStamp)
        {
            CurrentTarget.ID = byte.MaxValue;
            pc.RpcRemoveAbilityUse();
            pc.Notify(GetAnalyzeResult(target), 10f);
            pc.SetKillCooldown();
        }
    }

    public override void OnReportDeadBody()
    {
        if (!IsEnable) return;
        CurrentTarget.ID = byte.MaxValue;
    }
}