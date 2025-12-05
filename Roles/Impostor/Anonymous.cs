using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Neutral;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Impostor;

public class Anonymous : RoleBase
{
    private const int Id = 2200;
    private static List<byte> PlayerIdList = [];

    private static OptionItem HackLimitOpt;
    private static OptionItem KillCooldown;
    public static OptionItem AnonymousAbilityUseGainWithEachKill;

    private static List<byte> DeadBodyList = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Anonymous);

        KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Anonymous])
            .SetValueFormat(OptionFormat.Seconds);

        HackLimitOpt = new IntegerOptionItem(Id + 3, "HackLimit", new(0, 5, 1), 0, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Anonymous])
            .SetValueFormat(OptionFormat.Times);

        AnonymousAbilityUseGainWithEachKill = new FloatOptionItem(Id + 4, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.4f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Anonymous])
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
        playerId.SetAbilityUseLimit(HackLimitOpt.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
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
            __instance.AbilityButton.OverrideText(GetString("AnonymousShapeshiftText"));
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
        if (!shapeshifting || pc.GetAbilityUseLimit() < 1 || ssTarget == null || ssTarget.Is(CustomRoles.LazyGuy) || ssTarget.Is(CustomRoles.Lazy) || Thanos.IsImmune(ssTarget)) return false;

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
            LateTask.New(() => ssTarget.NoCheckStartMeeting(ssTarget.Data), 0.15f, "Anonymous Hacking Report Self");
        else
            LateTask.New(() => ssTarget.NoCheckStartMeeting(Utils.GetPlayerById(targetId)?.Data), 0.15f, "Anonymous Hacking Report");

        return false;
    }
}