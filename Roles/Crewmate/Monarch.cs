using System.Collections.Generic;
using AmongUs.GameOptions;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate;

public class Monarch : RoleBase
{
    private const int Id = 9600;
    private static List<byte> PlayerIdList = [];

    public static OptionItem KnightCooldown;
    public static OptionItem KnightMax;
    public static OptionItem UsePet;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Monarch);

        KnightCooldown = new FloatOptionItem(Id + 10, "MonarchKnightCooldown", new(0f, 60f, 0.5f), 15f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Monarch])
            .SetValueFormat(OptionFormat.Seconds);

        KnightMax = new IntegerOptionItem(Id + 12, "MonarchKnightMax", new(1, 15, 1), 2, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Monarch])
            .SetValueFormat(OptionFormat.Times);

        UsePet = CreatePetUseSetting(Id + 11, CustomRoles.Monarch);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(KnightMax.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KnightCooldown.GetFloat();
    }

    public override bool CanUseKillButton(PlayerControl player)
    {
        return player.IsAlive() && player.GetAbilityUseLimit() >= 1;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() < 1) return false;

        if (target != null && !target.GetCustomRole().IsNotKnightable() && !target.Is(CustomRoles.Knighted) && !target.Is(CustomRoles.Stealer))
        {
            killer.RpcRemoveAbilityUse();
            target.RpcSetCustomRole(CustomRoles.Knighted);

            var sender = CustomRpcSender.Create("Monarch.OnCheckMurder", SendOption.Reliable);
            var hasValue = false;

            killer.ResetKillCooldown();
            hasValue |= sender.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Monarch), GetString("MonarchKnightedPlayer")), setName: false);
            hasValue |= sender.SetKillCooldown(killer);
            hasValue |= sender.NotifyRolesSpecific(killer, target, out sender, out bool cleared);
            if (cleared) hasValue = false;

            hasValue |= sender.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Monarch), GetString("KnightedByMonarch")), setName: false);
            hasValue |= sender.RpcGuardAndKill(target, killer);
            hasValue |= sender.RpcGuardAndKill(target, target);
            hasValue |= sender.NotifyRolesSpecific(target, killer, out sender, out cleared);
            if (cleared) hasValue = false;

            sender.SendMessage(!hasValue);

            Logger.Info($"Set Role: {target.Data?.PlayerName} = {target.GetCustomRole()} + {CustomRoles.Knighted}", $"Assign {CustomRoles.Knighted}");
            return false;
        }

        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Monarch), GetString("MonarchInvalidTarget")));
        return false;
    }
}