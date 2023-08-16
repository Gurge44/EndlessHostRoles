using TOHE.Modules;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor;

public static class Disperser
{
    private static readonly int Id = 17000;

    private static OptionItem DisperserShapeshiftCooldown;
    private static OptionItem DisperserShapeshiftDuration;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.OtherRoles, CustomRoles.Disperser);
        DisperserShapeshiftCooldown = FloatOptionItem.Create(Id + 5, "ShapeshiftCooldown", new(1f, 60f, 1f), 40f, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Disperser])
            .SetValueFormat(OptionFormat.Seconds);
        DisperserShapeshiftDuration = FloatOptionItem.Create(Id + 7, "ShapeshiftDuration", new(1f, 30f, 1f), 1f, TabGroup.OtherRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Disperser])
            .SetValueFormat(OptionFormat.Seconds);
    }
    public static void ApplyGameOptions()
    {
        AURoleOptions.ShapeshifterCooldown = DisperserShapeshiftCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = DisperserShapeshiftDuration.GetFloat();
    }
    public static void DispersePlayers(PlayerControl shapeshifter)
    {
        var rd = new System.Random();
        var vents = Object.FindObjectsOfType<Vent>();

        foreach (var pc in PlayerControl.AllPlayerControls)
        {
            if (shapeshifter.PlayerId == pc.PlayerId || pc.Data.IsDead || pc.onLadder || pc.inVent || GameStates.IsMeeting)
            {
                if (!pc.Is(CustomRoles.Disperser))
                    pc.Notify(ColorString(GetRoleColor(CustomRoles.Disperser), string.Format(GetString("ErrorTeleport"), pc.GetRealName())));
                
                continue;
            }

            pc.RPCPlayCustomSound("Teleport");
            var vent = vents[rd.Next(0, vents.Count)];
            TP(pc.NetTransform, new Vector2(vent.transform.position.x, vent.transform.position.y));
            pc.Notify(ColorString(GetRoleColor(CustomRoles.Disperser), string.Format(GetString("TeleportedInRndVentByDisperser"), pc.GetRealName())));
        }
    }
    public static void GetAbilityButtonText(HudManager __instance, PlayerControl pc)
    {
        __instance.AbilityButton.ToggleVisible(pc.IsAlive());
        __instance.AbilityButton.OverrideText(GetString("DisperserVentButtonText"));
    }
}