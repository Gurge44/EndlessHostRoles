using AmongUs.GameOptions;
using System.Collections.Generic;
using static TOHE.Options;

namespace TOHE.Roles.Impostor;

public class Blackmailer : RoleBase
{
    private const int Id = 643050;
    private static List<byte> playerIdList = [];
    public static OptionItem SkillCooldown;
    public static OptionItem BlackmailMode;
    public static Dictionary<byte, int> BlackmailerMaxUp = [];
    public static List<byte> ForBlackmailer = [];

    private static readonly string[] BlackmailModes =
    [
        "EKill",
        "Shapeshift"
    ];

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Blackmailer);
        SkillCooldown = FloatOptionItem.Create(Id + 5, "BlackmailerSkillCooldown", new(2.5f, 60f, 2.5f), 30f, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Blackmailer])
            .SetValueFormat(OptionFormat.Seconds);
        BlackmailMode = StringOptionItem.Create(Id + 4, "BlackmailMode", BlackmailModes, 1, TabGroup.ImpostorRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Blackmailer]);
    }

    public override void Init()
    {
        playerIdList = [];
        BlackmailerMaxUp = [];
        ForBlackmailer = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        AURoleOptions.ShapeshifterCooldown = SkillCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    public override bool IsEnable => playerIdList.Count > 0;

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (BlackmailMode.GetValue() == 1 || ForBlackmailer.Contains(target.PlayerId)) return true;

        return killer.CheckDoubleTrigger(target, () =>
        {
            ForBlackmailer.Add(target.PlayerId);
            killer.SetKillCooldown(3f);
        });
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (BlackmailMode.GetValue() == 1 && !ForBlackmailer.Contains(target.PlayerId)) ForBlackmailer.Add(target.PlayerId);
        return false;
    }
}