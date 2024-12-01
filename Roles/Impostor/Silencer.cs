using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Impostor;

public class Silencer : RoleBase
{
    private const int Id = 643050;
    private static List<byte> PlayerIdList = [];

    public static OptionItem SkillCooldown;
    public static OptionItem SilenceMode;

    public static List<byte> ForSilencer = [];

    private static int LocalPlayerTotalSilences;

    private static readonly string[] SilenceModes =
    [
        "EKill",
        "Shapeshift"
    ];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
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
        PlayerIdList = [];
        ForSilencer = [];

        LocalPlayerTotalSilences = 0;
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
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

            if (killer.IsLocalPlayer())
            {
                LocalPlayerTotalSilences++;
                if (LocalPlayerTotalSilences >= 5) Achievements.Type.Censorship.Complete();

                if (target.Is(CustomRoles.Snitch) && Snitch.IsExposed.TryGetValue(target.PlayerId, out var exposed) && exposed)
                    Achievements.Type.YouWontTellAnyone.Complete();
            }
        });
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (SilenceMode.GetValue() == 1 && ForSilencer.Count == 0 && shapeshifter.PlayerId != target.PlayerId)
        {
            ForSilencer.Add(target.PlayerId);

            if (shapeshifter.IsLocalPlayer())
            {
                LocalPlayerTotalSilences++;
                if (LocalPlayerTotalSilences >= 5) Achievements.Type.Censorship.Complete();

                if (target.Is(CustomRoles.Snitch) && Snitch.IsExposed.TryGetValue(target.PlayerId, out var exposed) && exposed)
                    Achievements.Type.YouWontTellAnyone.Complete();
            }
        }

        return false;
    }

    public override void AfterMeetingTasks()
    {
        ForSilencer.Clear();
    }
}