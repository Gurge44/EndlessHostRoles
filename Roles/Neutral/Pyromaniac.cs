using System.Collections.Generic;
using AmongUs.GameOptions;
using static TOHE.Options;

namespace TOHE.Roles.Neutral;

public class Pyromaniac : RoleBase
{
    private const int Id = 128020;
    public static List<byte> playerIdList = [];

    public List<byte> DousedList = [];

    private static OptionItem KillCooldown;
    private static OptionItem DouseCooldown;
    private static OptionItem BurnCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Pyromaniac);
        KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Pyromaniac])
            .SetValueFormat(OptionFormat.Seconds);
        DouseCooldown = FloatOptionItem.Create(Id + 11, "PyroDouseCooldown", new(0f, 180f, 2.5f), 20f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Pyromaniac])
            .SetValueFormat(OptionFormat.Seconds);
        BurnCooldown = FloatOptionItem.Create(Id + 12, "PyroBurnCooldown", new(0f, 180f, 2.5f), 5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Pyromaniac])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 13, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Pyromaniac]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 14, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Pyromaniac]);
    }

    public override void Init()
    {
        playerIdList = [];
        DousedList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        DousedList = [];

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null) return true;
        if (target == null) return true;

        if (DousedList.Contains(target.PlayerId))
        {
            _ = new LateTask(() => { killer.SetKillCooldown(BurnCooldown.GetFloat()); }, 0.1f);
            return true;
        }

        return killer.CheckDoubleTrigger(target, () =>
        {
            DousedList.Add(target.PlayerId);
            killer.SetKillCooldown(DouseCooldown.GetFloat());
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
        });
    }
}