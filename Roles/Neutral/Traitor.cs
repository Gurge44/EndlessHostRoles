using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Neutral;

public class Traitor : RoleBase
{
    private const int Id = 13100;
    public static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    public static OptionItem CanSabotage;
    public static OptionItem CanGetImpostorOnlyAddons;
    private static OptionItem LegacyTraitor;
    private static OptionItem TraitorShapeshiftCD;
    private static OptionItem TraitorShapeshiftDur;
    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Traitor);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Traitor])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 11, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Traitor]);

        HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Traitor]);

        CanSabotage = new BooleanOptionItem(Id + 15, "CanUseSabotage", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Traitor]);
            
        CanGetImpostorOnlyAddons = new BooleanOptionItem(Id + 16, "CanGetImpostorOnlyAddons", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Traitor]);

        LegacyTraitor = new BooleanOptionItem(Id + 17, "LegacyTraitor", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Traitor]);

        TraitorShapeshiftCD = new FloatOptionItem(Id + 19, "ShapeshiftCooldown", new(1f, 180f, 1f), 15f, TabGroup.NeutralRoles)
                .SetParent(LegacyTraitor)
                .SetValueFormat(OptionFormat.Seconds);

        TraitorShapeshiftDur = new FloatOptionItem(Id + 21, "ShapeshiftDuration", new(1f, 180f, 1f), 30f, TabGroup.NeutralRoles)
                .SetParent(LegacyTraitor)
                .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
    }

    public override void SetButtonTexts(HudManager __instance, byte id)
    {
        __instance.SabotageButton.ToggleVisible(CanSabotage.GetBool());
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());
        AURoleOptions.ShapeshifterCooldown = TraitorShapeshiftCD.GetFloat();
        AURoleOptions.ShapeshifterDuration = TraitorShapeshiftDur.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool CanUseSabotage(PlayerControl pc)
    {
        return base.CanUseSabotage(pc) || (CanSabotage.GetBool() && pc.IsAlive());

    }
}
