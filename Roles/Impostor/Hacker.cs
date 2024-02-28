using System.Collections.Generic;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Impostor;

public class Hacker : RoleBase
{
    private const int Id = 2200;
    private static List<byte> playerIdList = [];

    private static OptionItem HackLimitOpt;
    private static OptionItem KillCooldown;
    public static OptionItem HackerAbilityUseGainWithEachKill;

    private static List<byte> DeadBodyList = [];

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Hacker);
        KillCooldown = FloatOptionItem.Create(Id + 2, "KillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Seconds);
        HackLimitOpt = IntegerOptionItem.Create(Id + 3, "HackLimit", new(0, 5, 1), 0, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Times);
        HackerAbilityUseGainWithEachKill = FloatOptionItem.Create(Id + 4, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.2f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        playerIdList = [];
        DeadBodyList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(HackLimitOpt.GetInt());
    }

    public override bool IsEnable => playerIdList.Count > 0;
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();

    public override void ApplyGameOptions(AmongUs.GameOptions.IGameOptions opt, byte id)
    {
        AURoleOptions.ShapeshifterCooldown = 15f;
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    public override void SetButtonTexts(HudManager __instance, byte playerId)
    {
        if (playerId.GetAbilityUseLimit() >= 1)
        {
            __instance.AbilityButton.OverrideText(GetString("HackerShapeshiftText"));
            __instance.AbilityButton.SetUsesRemaining((int)playerId.GetAbilityUseLimit());
        }
    }

    public override void OnReportDeadBody() => DeadBodyList = [];

    public static void AddDeadBody(PlayerControl target)
    {
        if (target != null && !DeadBodyList.Contains(target.PlayerId))
            DeadBodyList.Add(target.PlayerId);
    }

    public override bool OnShapeshift(PlayerControl pc, PlayerControl ssTarget, bool shapeshifting)
    {
        if (!shapeshifting || pc.GetAbilityUseLimit() < 1 || ssTarget == null || ssTarget.Is(CustomRoles.Needy) || ssTarget.Is(CustomRoles.Lazy)) return false;
        pc.RpcRemoveAbilityUse();

        var targetId = byte.MaxValue;

        // 寻找骇客击杀的尸体
        foreach (byte db in DeadBodyList.ToArray())
        {
            var dp = Utils.GetPlayerById(db);
            if (dp == null || dp.GetRealKiller() == null) continue;
            if (dp.GetRealKiller().PlayerId == pc.PlayerId) targetId = db;
        }

        // 未找到骇客击杀的尸体，寻找其他尸体
        if (targetId == byte.MaxValue && DeadBodyList.Count > 0)
            targetId = DeadBodyList[IRandom.Instance.Next(0, DeadBodyList.Count)];

        _ = targetId == byte.MaxValue ? new(() => ssTarget.NoCheckStartMeeting(ssTarget.Data), 0.15f, "Hacker Hacking Report Self") : new LateTask(() => ssTarget.NoCheckStartMeeting(Utils.GetPlayerById(targetId)?.Data), 0.15f, "Hacker Hacking Report");

        return false;
    }
}