using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Impostor;

public class Hacker : RoleBase
{
    private const int Id = 2200;
    private static List<byte> PlayerIdList = [];

    private static OptionItem HackLimitOpt;
    private static OptionItem KillCooldown;
    public static OptionItem HackerAbilityUseGainWithEachKill;

    private static List<byte> DeadBodyList = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Hacker);

        KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Seconds);

        HackLimitOpt = new IntegerOptionItem(Id + 3, "HackLimit", new(0, 5, 1), 0, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Times);

        HackerAbilityUseGainWithEachKill = new FloatOptionItem(Id + 4, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.2f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Hacker])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = [];
        DeadBodyList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(HackLimitOpt.GetInt());
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
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

    public override void OnReportDeadBody()
    {
        DeadBodyList = [];
    }

    public static void AddDeadBody(PlayerControl target)
    {
        if (target != null && !DeadBodyList.Contains(target.PlayerId)) DeadBodyList.Add(target.PlayerId);
    }

    public override bool OnShapeshift(PlayerControl pc, PlayerControl ssTarget, bool shapeshifting)
    {
        if (!shapeshifting || pc.GetAbilityUseLimit() < 1 || ssTarget == null || ssTarget.Is(CustomRoles.Needy) || ssTarget.Is(CustomRoles.Lazy)) return false;

        pc.RpcRemoveAbilityUse();

        var targetId = byte.MaxValue;

        foreach (byte db in DeadBodyList.ToArray())
        {
            PlayerControl dp = Utils.GetPlayerById(db);
            if (dp == null || dp.GetRealKiller() == null) continue;

            if (dp.GetRealKiller().PlayerId == pc.PlayerId) targetId = db;
        }

        if (targetId == byte.MaxValue && DeadBodyList.Count > 0) targetId = DeadBodyList.RandomElement();

        if (targetId == byte.MaxValue)
            LateTask.New(() => ssTarget.NoCheckStartMeeting(ssTarget.Data), 0.15f, "Hacker Hacking Report Self");
        else
            LateTask.New(() => ssTarget.NoCheckStartMeeting(Utils.GetPlayerById(targetId)?.Data), 0.15f, "Hacker Hacking Report");

        return false;
    }
}