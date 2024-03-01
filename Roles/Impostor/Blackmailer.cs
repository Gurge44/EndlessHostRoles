using System.Collections.Generic;
using AmongUs.GameOptions;
using static TOHE.Options;

namespace TOHE.Roles.Impostor;

public class Blackmailer : RoleBase
{
    private const int Id = 643050;
    private static List<byte> playerIdList = [];
    public static OptionItem SkillCooldown;
    public static Dictionary<byte, int> BlackmailerMaxUp = [];
    public static List<byte> ForBlackmailer = [];

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.OtherRoles, CustomRoles.Blackmailer);
        SkillCooldown = FloatOptionItem.Create(Id + 5, "BlackmailerSkillCooldown", new(2.5f, 60f, 2.5f), 30f, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Blackmailer])
            .SetValueFormat(OptionFormat.Seconds);
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

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        ForBlackmailer.Add(target.PlayerId);
        return false;
    }
}