using AmongUs.GameOptions;
using Hazel;
using System;
using System.Collections.Generic;
using System.Text;
using static TOHE.Options;

namespace TOHE.Roles.Impostor;

public static class Morphling
{
    private static readonly int Id = 3000;
    public static List<byte> playerIdList = new();

    public static OptionItem KillCooldown;
    public static OptionItem ShapeshiftCD;
    public static OptionItem ShapeshiftDur;


    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Morphling);
        KillCooldown = FloatOptionItem.Create(Id + 14, "KillCooldown", new(0f, 60f, 2.5f), 10f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Morphling])
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshiftCD = FloatOptionItem.Create(Id + 15, "ShapeshiftCooldown", new(1f, 60f, 1f), 20f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Morphling])
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshiftDur = FloatOptionItem.Create(Id + 16, "ShapeshiftDuration", new(1f, 30f, 1f), 10f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Morphling])
            .SetValueFormat(OptionFormat.Seconds);
    }
    public static void Init()
    {
        playerIdList = new();

    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        
    }
    public static bool CanUseKillButton(byte playerId)
        => !Main.PlayerStates[playerId].IsDead
        && Main.CheckShapeshift[playerId];

    public static void ApplyGameOptions()
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCD.GetFloat();
            AURoleOptions.ShapeshifterDuration = ShapeshiftDur.GetFloat();
        }
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();


    public static bool IsEnable => playerIdList.Count > 0;


}