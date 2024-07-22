using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate;

public class Monarch : RoleBase
{
    private const int Id = 9600;
    private static List<byte> playerIdList = [];

    public static OptionItem KnightCooldown;
    public static OptionItem KnightMax;
    public static OptionItem UsePet;

    public override bool IsEnable => playerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Monarch);
        KnightCooldown = new FloatOptionItem(Id + 10, "MonarchKnightCooldown", new(0f, 60f, 2.5f), 15f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Monarch])
            .SetValueFormat(OptionFormat.Seconds);
        KnightMax = new IntegerOptionItem(Id + 12, "MonarchKnightMax", new(1, 15, 1), 2, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Monarch])
            .SetValueFormat(OptionFormat.Times);
        UsePet = CreatePetUseSetting(Id + 11, CustomRoles.Monarch);
    }

    public override void Init()
    {
        playerIdList = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(KnightMax.GetInt());
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KnightCooldown.GetFloat();
    public override bool CanUseKillButton(PlayerControl player) => !player.Data.IsDead && player.GetAbilityUseLimit() >= 1;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => false;
    public override void ApplyGameOptions(IGameOptions opt, byte playerId) => opt.SetVision(false);

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() < 1) return false;
        if (target != null && !target.GetCustomRole().IsNotKnightable() && !target.Is(CustomRoles.Knighted) && !target.Is(CustomRoles.TicketsStealer))
        {
            killer.RpcRemoveAbilityUse();
            target.RpcSetCustomRole(CustomRoles.Knighted);

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Monarch), GetString("MonarchKnightedPlayer")));
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Monarch), GetString("KnightedByMonarch")));
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

            killer.ResetKillCooldown();
            killer.SetKillCooldown();

            target.RpcGuardAndKill(killer);
            target.RpcGuardAndKill(target);

            Logger.Info("SetRole:" + target.Data?.PlayerName + " = " + target.GetCustomRole() + " + " + CustomRoles.Knighted, "Assign " + CustomRoles.Knighted);
            return false;
        }

        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Monarch), GetString("MonarchInvalidTarget")));
        return false;
    }
}