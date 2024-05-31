using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Roles.Impostor;

public class Silencer : RoleBase
{
    private const int Id = 643050;
    private static List<byte> playerIdList = [];

    public static OptionItem SkillCooldown;
    public static OptionItem SilenceMode;

    public static List<byte> ForSilencer = [];

    private static readonly string[] SilenceModes =
    [
        "EKill",
        "Shapeshift"
    ];

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Silencer);
        SkillCooldown = new FloatOptionItem(Id + 5, "SilencerSkillCooldown", new(2.5f, 60f, 2.5f), 30f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Silencer])
            .SetValueFormat(OptionFormat.Seconds);
        SilenceMode = new StringOptionItem(Id + 4, "SilenceMode", SilenceModes, 1, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Silencer]);
    }

    public override void Init()
    {
        playerIdList = [];
        ForSilencer = [];
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

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (SilenceMode.GetValue() == 1 || ForSilencer.Count >= 1) return true;

        return killer.CheckDoubleTrigger(target, () =>
        {
            ForSilencer.Add(target.PlayerId);
            killer.SetKillCooldown(3f);
        });
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (SilenceMode.GetValue() == 1 && ForSilencer.Count == 0) ForSilencer.Add(target.PlayerId);
        return false;
    }

    public override void AfterMeetingTasks()
    {
        ForSilencer.Clear();
    }
}