using System.Collections.Generic;
using static TOHE.Options;

namespace TOHE.Roles.Impostor;

public static class Blackmailer
{
    private static readonly int Id = 643050;
    private static List<byte> playerIdList = [];
    public static OptionItem SkillCooldown;
    public static Dictionary<byte, int> BlackmailerMaxUp;
    public static List<byte> ForBlackmailer = [];
    public static bool IsEnable;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.OtherRoles, CustomRoles.Blackmailer);
        SkillCooldown = FloatOptionItem.Create(Id + 5, "BlackmailerSkillCooldown", new(2.5f, 60f, 2.5f), 30f, TabGroup.OtherRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Blackmailer])
            .SetValueFormat(OptionFormat.Seconds);
    }
    public static void Init()
    {
        playerIdList = [];
        BlackmailerMaxUp = [];
        ForBlackmailer = [];
        IsEnable = false;
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        IsEnable = true;
    }
    public static void ApplyGameOptions()
    {
        AURoleOptions.ShapeshifterCooldown = SkillCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = 1f;
    }
}