using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;

namespace TOHE.Roles.Neutral;

public static class HeadHunter
{
    private static readonly int Id = 12870;
    public static List<byte> playerIdList = new();

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem SuccessKillCooldown;
    private static OptionItem FailureKillCooldown;
    private static OptionItem NumOfTargets;
    private static OptionItem MinKCD;
    private static OptionItem MaxKCD;

    public static List<byte> Targets = new();
    public static float KCD = 25;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.HeadHunter, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 27.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.HeadHunter])
            .SetValueFormat(OptionFormat.Seconds);
        SuccessKillCooldown = FloatOptionItem.Create(Id + 11, "HHSuccessKCDDecrease", new(0f, 180f, 0.5f), 3f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.HeadHunter])
            .SetValueFormat(OptionFormat.Seconds);
        FailureKillCooldown = FloatOptionItem.Create(Id + 12, "HHFailureKCDIncrease", new(0f, 180f, 0.5f), 10f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.HeadHunter])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 13, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.HeadHunter]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 14, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.HeadHunter]);
        NumOfTargets = IntegerOptionItem.Create(Id + 15, "HHNumOfTargets", new(0, 10, 1), 3, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.HeadHunter])
            .SetValueFormat(OptionFormat.Times);
        MaxKCD = FloatOptionItem.Create(Id + 16, "HHMaxKCD", new(0f, 180f, 2.5f), 40f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.HeadHunter])
            .SetValueFormat(OptionFormat.Seconds);
        MinKCD = FloatOptionItem.Create(Id + 17, "HHMinKCD", new(0f, 180f, 2.5f), 10f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.HeadHunter])
            .SetValueFormat(OptionFormat.Seconds);
    }
    public static void Init()
    {
        playerIdList = new();
        Targets = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        _ = new LateTask(ResetTargets, 8f);
        KCD = KillCooldown.GetFloat();

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Any();
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
    public static void OnReportDeadBody()
    {
        ResetTargets();
    }
    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        float tempkcd = KCD;
        if (Targets.Contains(target.PlayerId)) System.Math.Clamp(KCD -= SuccessKillCooldown.GetFloat(), MinKCD.GetFloat(), MaxKCD.GetFloat());
        else System.Math.Clamp(KCD += FailureKillCooldown.GetFloat(), MinKCD.GetFloat(), MaxKCD.GetFloat());
        if (KCD != tempkcd)
        {
            killer.ResetKillCooldown();
            killer.SyncSettings();
        }
    }
    public static string GetHudText(PlayerControl player)
    {
        var targetId = player.PlayerId;
        string output = string.Empty;
        for (int i = 0; i < Targets.Count; i++) { byte playerId = Targets[i]; if (i != 0) output += ", "; output += Utils.GetPlayerById(playerId).GetRealName(); }
        return targetId != 0xff ? $"<color=#00ffa5>Targets:</color> <b>{output}</b>" : string.Empty;
    }
    public static void ResetTargets()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        Targets.Clear();
        for (var i = 0; i < NumOfTargets.GetInt(); i++)
        {
            try
            {
                var cTargets = new List<PlayerControl>(Main.AllAlivePlayerControls.Where(pc => !Targets.Contains(pc.PlayerId) && pc.GetCustomRole() != CustomRoles.HeadHunter));
                if (cTargets == null || !cTargets.Any()) break;
                var target = cTargets[IRandom.Instance.Next(0, cTargets.Count)];
                Targets.Add(target.PlayerId);
            }
            catch (System.Exception ex)
            {
                Logger.Warn($"Not enough targets for Head Hunter could be assigned. This may be due to a low player count or the following error:\n\n{ex}", "HeadHunterAssignTargets");
                break;
            }
        }

        Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(playerIdList[0]));
    }
}
