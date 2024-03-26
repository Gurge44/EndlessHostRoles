using AmongUs.GameOptions;
using UnityEngine;
using static EHR.Options;

namespace EHR.Roles.Neutral;

public class Maverick : RoleBase
{
    private const int Id = 10000;

    public byte MaverickId = byte.MaxValue;
    public int NumOfKills;

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    public static OptionItem MinKillsToWin;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Maverick);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 35f, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Maverick])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Maverick]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Maverick]);
        MinKillsToWin = IntegerOptionItem.Create(Id + 12, "DQNumOfKillsNeeded", new(0, 14, 1), 2, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Maverick]);
    }

    public override void Init()
    {
        MaverickId = byte.MaxValue;
        NumOfKills = 0;
    }

    public override void Add(byte playerId)
    {
        MaverickId = playerId;
        NumOfKills = 0;

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => MaverickId != byte.MaxValue;
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
    public override bool CanUseKillButton(PlayerControl pc) => pc.IsAlive();
    public override bool CanUseSabotage(PlayerControl pc) => false;

    public override string GetProgressText(byte playerId, bool comms)
    {
        if (Main.PlayerStates[playerId].Role is not Maverick mr) return string.Empty;
        int kills = mr.NumOfKills;
        int min = MinKillsToWin.GetInt();
        Color color = kills >= min ? Color.green : Color.red;
        return Utils.ColorString(color, $"{kills}/{min}");
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        NumOfKills++;
    }
}
