using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Roles.Impostor;

public class Morphling : RoleBase
{
    private const int Id = 3000;
    public static List<byte> playerIdList = [];

    public static OptionItem KillCooldown;
    public static OptionItem ShapeshiftCD;
    public static OptionItem ShapeshiftDur;
    public override bool IsEnable => playerIdList.Count > 0;


    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Morphling);
        KillCooldown = new FloatOptionItem(Id + 14, "KillCooldown", new(0f, 60f, 2.5f), 10f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Morphling])
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshiftCD = new FloatOptionItem(Id + 15, "ShapeshiftCooldown", new(1f, 60f, 1f), 20f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Morphling])
            .SetValueFormat(OptionFormat.Seconds);
        ShapeshiftDur = new FloatOptionItem(Id + 16, "ShapeshiftDuration", new(1f, 30f, 1f), 10f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Morphling])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        playerIdList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
    }

    public override bool CanUseKillButton(PlayerControl pc) => !Main.PlayerStates[pc.PlayerId].IsDead && pc.IsShifted();

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        AURoleOptions.ShapeshifterCooldown = ShapeshiftCD.GetFloat();
        AURoleOptions.ShapeshifterDuration = ShapeshiftDur.GetFloat();
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
}