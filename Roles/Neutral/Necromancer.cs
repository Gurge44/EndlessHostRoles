using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Neutral;

internal class Necromancer : RoleBase
{
    private static byte NecromancerId = byte.MaxValue;
    private static PlayerControl NecromancerPC;

    private static OptionItem CD;
    public static OptionItem Dkcd;
    private static OptionItem KnowTargetRole;
    public static OptionItem UndeadCountMode;

    private static readonly string[] UndeadCountModeStrings =
    [
        "UndeadCountMode.None",
        "UndeadCountMode.Necromancer",
        "UndeadCountMode.Original"
    ];

    public static readonly List<byte> PartiallyRecruitedIds = [];
    private static int Id => 643450;

    public override bool IsEnable => NecromancerId != byte.MaxValue;

    public override void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Necromancer);

        CD = new FloatOptionItem(Id + 2, "NecromancerCD", new(0f, 180f, 0.5f), 30f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Necromancer])
            .SetValueFormat(OptionFormat.Seconds);

        Dkcd = new FloatOptionItem(Id + 10, "DKCD", new(0f, 180f, 0.5f), 30f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Necromancer])
            .SetValueFormat(OptionFormat.Seconds);

        KnowTargetRole = new BooleanOptionItem(Id + 13, "NecromancerKnowTargetRole", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Necromancer]);

        UndeadCountMode = new StringOptionItem(Id + 15, "UndeadCountMode", UndeadCountModeStrings, 0, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Necromancer]);
    }

    public override void Init()
    {
        NecromancerId = byte.MaxValue;
        NecromancerPC = null;

        PartiallyRecruitedIds.Clear();

        Deathknight.DeathknightId = byte.MaxValue;
        Deathknight.DeathknightPC = null;
    }

    public override void Add(byte playerId)
    {
        NecromancerId = playerId;
        NecromancerPC = Utils.GetPlayerById(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = CD.GetFloat();
    }

    public override bool CanUseKillButton(PlayerControl player)
    {
        return player.IsAlive();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
        opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision);
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Main.DefaultCrewmateVision);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Deathknight.DeathknightId == byte.MaxValue && !target.Is(CustomRoles.Loyal) && !target.Is(CustomRoles.Curser) && !target.IsConverted())
        {
            target.RpcSetCustomRole(CustomRoles.Deathknight);
            target.RpcChangeRoleBasis(CustomRoles.Deathknight);

            killer.SetKillCooldown();

            target.ResetKillCooldown();
            target.Notify(GetString("RecruitedToDeathknight"));
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);

            LateTask.New(() => target.SetKillCooldown(), 0.2f, log: false);

            Utils.NotifyRoles(SpecifySeer: target);
            target.MarkDirtySettings();

            new[] { CustomRoles.Damocles, CustomRoles.Stressed }.Do(x => Main.PlayerStates[target.PlayerId].RemoveSubRole(x));

            if (killer.AmOwner)
                Achievements.Type.YoureMyFriendNow.Complete();

            return false;
        }

        if (CanBeUndead(target) && !PartiallyRecruitedIds.Contains(target.PlayerId))
        {
            PartiallyRecruitedIds.Add(target.PlayerId);

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Necromancer), GetString("NecromancerRecruitedPlayer")));

            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            Utils.NotifyRoles(SpecifySeer: Deathknight.DeathknightPC, SpecifyTarget: target);

            killer.SetKillCooldown();

            Logger.Info($"Partial Recruit: {target.GetRealName()}", "Necromancer");

            return false;
        }

        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Necromancer), GetString("InvalidUndeadTarget")));

        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || !IsEnable) return;

        if (!NecromancerPC.IsAlive() && Deathknight.DeathknightPC.IsAlive())
        {
            Deathknight.DeathknightPC.RpcSetCustomRole(CustomRoles.Necromancer);
            Add(Deathknight.DeathknightId);

            Deathknight.DeathknightPC = null;
            Deathknight.DeathknightId = byte.MaxValue;
            return;
        }

        if (!CustomRoles.Deathknight.RoleExist() && (Deathknight.DeathknightId != byte.MaxValue || Deathknight.DeathknightPC != null))
        {
            Deathknight.DeathknightPC = null;
            Deathknight.DeathknightId = byte.MaxValue;
        }
    }

    public override bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (base.KnowRole(player, target)) return true;
        if (player.Is(CustomRoles.Undead) && (target.Is(CustomRoles.Necromancer) || target.Is(CustomRoles.Deathknight))) return true;
        if (KnowTargetRole.GetBool() && (player.Is(CustomRoles.Necromancer) || player.Is(CustomRoles.Deathknight)) && target.Is(CustomRoles.Undead)) return true;
        if (player.Is(CustomRoles.Deathknight) && target.Is(CustomRoles.Necromancer)) return true;
        return player.Is(CustomRoles.Necromancer) && target.Is(CustomRoles.Deathknight);
    }

    public static bool CanBeUndead(PlayerControl pc)
    {
        return pc != null && !pc.Is(CustomRoles.Deathknight) && !pc.Is(CustomRoles.Necromancer) && !pc.Is(CustomRoles.Undead) && !pc.Is(CustomRoles.Loyal) && !pc.Is(CustomRoles.Curser) && !pc.IsConverted() && !pc.Is(Team.Coven);
    }
}

internal class Deathknight : RoleBase
{
    public static byte DeathknightId = byte.MaxValue;
    public static PlayerControl DeathknightPC;

    public override bool IsEnable => DeathknightId != byte.MaxValue;

    public override void SetupCustomOption() { }

    public override void Init()
    {
        DeathknightId = byte.MaxValue;
        DeathknightPC = null;
    }

    public override void Add(byte playerId)
    {
        DeathknightId = playerId;
        DeathknightPC = Utils.GetPlayerById(playerId);
        // if (!UsePets.GetBool()) Deathknight_.ChangeRoleBasis(RoleTypes.Impostor);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = Necromancer.Dkcd.GetFloat();
    }

    public override bool CanUseKillButton(PlayerControl player)
    {
        return player.IsAlive();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(false);
        opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision);
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Main.DefaultCrewmateVision);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Necromancer.CanBeUndead(target) && Necromancer.PartiallyRecruitedIds.Contains(target.PlayerId))
        {
            target.RpcSetCustomRole(CustomRoles.Undead);

            var sender = CustomRpcSender.Create("Deathknight.OnCheckMurder", SendOption.Reliable);
            var hasValue = false;

            hasValue |= sender.Notify(killer, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Necromancer), GetString("DeathknightRecruitedPlayer")));
            hasValue |= sender.SetKillCooldown(killer);
            hasValue |= sender.NotifyRolesSpecific(killer, target, out sender, out bool cleared);
            if (cleared) hasValue = false;

            hasValue |= sender.Notify(target, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Necromancer), GetString("RecruitedByDeathknight")));
            hasValue |= sender.RpcGuardAndKill(target, killer);
            hasValue |= sender.RpcGuardAndKill(target, target);
            hasValue |= sender.NotifyRolesSpecific(target, killer, out sender, out cleared);
            if (cleared) hasValue = false;

            sender.SendMessage(!hasValue);

            Logger.Info($"Recruit: {target.Data?.PlayerName} = {target.GetCustomRole()} + {CustomRoles.Undead}", $"Assign {CustomRoles.Undead}");

            if (killer.AmOwner)
                Achievements.Type.YoureMyFriendNow.Complete();

            return false;
        }

        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Necromancer), GetString("InvalidUndeadTarget")));

        return false;
    }
}