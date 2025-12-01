using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Neutral;

public class Cultist : RoleBase
{
    private const int Id = 11200;
    private static List<byte> PlayerIdList = [];

    private static OptionItem CharmCooldown;
    private static OptionItem CharmCooldownIncrese;
    private static OptionItem CharmMax;
    private static OptionItem KnowTargetRole;
    public static OptionItem TargetKnowOtherTarget;
    private static OptionItem CanCharmNeutral;
    public static OptionItem CharmedCountMode;
    private static OptionItem CharmedDiesOnCultistDeath;

    private static readonly string[] CharmedCountModeStrings =
    [
        "CharmedCountMode.None",
        "CharmedCountMode.Cultist",
        "CharmedCountMode.Original"
    ];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Cultist);

        CharmCooldown = new FloatOptionItem(Id + 10, "CultistCharmCooldown", new(0f, 60f, 0.5f), 30f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Cultist])
            .SetValueFormat(OptionFormat.Seconds);

        CharmCooldownIncrese = new FloatOptionItem(Id + 11, "CultistCharmCooldownIncrese", new(0f, 180f, 0.5f), 10f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Cultist])
            .SetValueFormat(OptionFormat.Seconds);

        CharmMax = new IntegerOptionItem(Id + 12, "CultistCharmMax", new(1, 15, 1), 15, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Cultist])
            .SetValueFormat(OptionFormat.Times);

        KnowTargetRole = new BooleanOptionItem(Id + 13, "CultistKnowTargetRole", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Cultist]);

        TargetKnowOtherTarget = new BooleanOptionItem(Id + 14, "CultistTargetKnowOtherTarget", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Cultist]);

        CharmedCountMode = new StringOptionItem(Id + 15, "CharmedCountMode", CharmedCountModeStrings, 0, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Cultist]);

        CanCharmNeutral = new BooleanOptionItem(Id + 16, "CultistCanCharmNeutral", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Cultist]);

        CharmedDiesOnCultistDeath = new BooleanOptionItem(Id + 17, "CharmedDiesOnCultistDeath", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Cultist]);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(CharmMax.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = CharmCooldown.GetFloat() + ((CharmMax.GetInt() - id.GetAbilityUseLimit()) * CharmCooldownIncrese.GetFloat());
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

        if (CanBeCharmed(target))
        {
            killer.RpcRemoveAbilityUse();

            target.RpcSetCustomRole(CustomRoles.Charmed);

            var sender = CustomRpcSender.Create("Cultist.OnCheckMurder", SendOption.Reliable);
            var hasValue = false;

            hasValue |= sender.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Cultist), GetString("CultistCharmedPlayer")));
            hasValue |= sender.SetKillCooldown(killer);
            hasValue |= sender.NotifyRolesSpecific(killer, target, out sender, out bool cleared);
            if (cleared) hasValue = false;

            hasValue |= sender.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Cultist), GetString("CharmedByCultist")));
            hasValue |= sender.RpcGuardAndKill(target, killer);
            hasValue |= sender.RpcGuardAndKill(target, target);
            hasValue |= sender.NotifyRolesSpecific(target, killer, out sender, out cleared);
            if (cleared) hasValue = false;

            sender.SendMessage(!hasValue);

            Logger.Info("SetRole:" + target?.Data?.PlayerName + " = " + target.GetCustomRole() + " + " + CustomRoles.Charmed, "Assign " + CustomRoles.Charmed);

            if (killer.AmOwner)
                Achievements.Type.YoureMyFriendNow.Complete();

            return false;
        }

        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Cultist), GetString("CultistInvalidTarget")));

        return false;
    }

    public override bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (base.KnowRole(player, target)) return true;
        if (player.Is(CustomRoles.Charmed) && target.Is(CustomRoles.Cultist)) return true;
        if (KnowTargetRole.GetBool() && player.Is(CustomRoles.Cultist) && target.Is(CustomRoles.Charmed)) return true;
        return TargetKnowOtherTarget.GetBool() && player.Is(CustomRoles.Charmed) && target.Is(CustomRoles.Charmed);
    }

    public static bool CanBeCharmed(PlayerControl pc)
    {
        return pc != null && (pc.IsCrewmate() || pc.IsImpostor() ||
                              (CanCharmNeutral.GetBool() && (pc.GetCustomRole().IsNeutral() || pc.IsNeutralKiller()))) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Loyal) && !pc.Is(CustomRoles.Curser) && !pc.Is(Team.Coven);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!CharmedDiesOnCultistDeath.GetBool() || !GameStates.IsInTask || !IsEnable) return;

        if (pc == null || pc.IsAlive()) return;

        foreach (PlayerControl charmed in Main.AllAlivePlayerControls.Where(x => x.Is(CustomRoles.Charmed))) charmed.Suicide(realKiller: pc);
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton?.OverrideText(GetString("CultistKillButtonText"));
    }
}