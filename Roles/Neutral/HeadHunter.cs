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

    public static List<byte> Targets = new();

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.HeadHunter, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.HeadHunter])
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
    }
    public static void Init()
    {
        playerIdList = new();
        Targets = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        ResetTargets();

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Any();
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
    public static void CanUseVent(PlayerControl player)
    {
        bool NSerialKiller_canUse = CanVent.GetBool();
        DestroyableSingleton<HudManager>.Instance.ImpostorVentButton.ToggleVisible(NSerialKiller_canUse && !player.Data.IsDead);
        player.Data.Role.CanVent = NSerialKiller_canUse;
    }
    public static void OnReportDeadBody()
    {
        ResetTargets();
    }
    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Targets.Contains(target.PlayerId)) Main.AllPlayerKillCooldown[killer.PlayerId] -= SuccessKillCooldown.GetFloat();
        else Main.AllPlayerKillCooldown[killer.PlayerId] += FailureKillCooldown.GetFloat();
        killer.SyncSettings();
    }
    public static string GetHudText(PlayerControl player)
    {
        var targetId = player.PlayerId;
        return targetId != 0xff ? $"<color=#00ffa5>{"Targets"}:</color> <b>{Targets.ToString().RemoveHtmlTags().Replace("\r\n", string.Empty)}</b>" : string.Empty;
    }
    public static void ResetTargets()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        Targets.Clear();
        for (var i = 0; i < NumOfTargets.GetInt(); i++)
        {
            var cTargets = new List<PlayerControl>(Main.AllAlivePlayerControls.Where(pc => !Targets.Contains(pc.PlayerId)));
            var rand = IRandom.Instance;
            var target = cTargets[rand.Next(0, cTargets.Count)];
            var targetId = target.PlayerId;
            Targets.Add(targetId);
        }

        if (Targets.Count <= NumOfTargets.GetInt()) { Logger.Warn("Not enough targets", "HeadHunter"); }
    }
    //public static void SetAbilityButtonText(HudManager __instance) => __instance.AbilityButton.OverrideText(GetString("BountyHunterChangeButtonText"));
    //public static void AfterMeetingTasks()
    //{
    //    foreach (var id in playerIdList)
    //    {
    //        if (!Main.PlayerStates[id].IsDead)
    //        {

    //        }
    //    }
    //}
}
