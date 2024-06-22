using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles.Neutral
{
    internal class Necromancer : RoleBase
    {
        public static byte NecromancerId = byte.MaxValue;
        public static PlayerControl Necromancer_;

        private static OptionItem CD;
        public static OptionItem DKCD;
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

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Necromancer);
            CD = new FloatOptionItem(Id + 2, "NecromancerCD", new(0f, 180f, 0.5f), 30f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Necromancer])
                .SetValueFormat(OptionFormat.Seconds);
            DKCD = new FloatOptionItem(Id + 10, "DKCD", new(0f, 180f, 2.5f), 30f, TabGroup.NeutralRoles)
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
            Necromancer_ = null;

            PartiallyRecruitedIds.Clear();

            Deathknight.DeathknightId = byte.MaxValue;
            Deathknight.Deathknight_ = null;
        }

        public override void Add(byte playerId)
        {
            NecromancerId = playerId;
            Necromancer_ = Utils.GetPlayerById(playerId);

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CD.GetFloat();

        public override bool CanUseKillButton(PlayerControl player) => player.IsAlive();

        public override bool CanUseImpostorVentButton(PlayerControl pc) => false;

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(false);
            opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision);
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, Main.DefaultCrewmateVision);
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (Deathknight.DeathknightId == byte.MaxValue)
            {
                target.RpcSetCustomRole(CustomRoles.Deathknight);

                killer.SetKillCooldown();

                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target);

                target.MarkDirtySettings();
                target.ResetKillCooldown();
                target.SetKillCooldown();

                target.Notify(GetString("RecruitedToDeathknight"));

                return false;
            }

            if (CanBeUndead(target) && !PartiallyRecruitedIds.Contains(target.PlayerId))
            {
                PartiallyRecruitedIds.Add(target.PlayerId);

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Necromancer), GetString("NecromancerRecruitedPlayer")));

                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: Deathknight.Deathknight_, SpecifyTarget: target);

                killer.SetKillCooldown();
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info($"Partial Recruit: {target.GetRealName()}", "Necromancer");

                return false;
            }

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Necromancer), GetString("InvalidUndeadTarget")));

            return false;
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!GameStates.IsInTask || !IsEnable || Necromancer_.IsAlive() || !Deathknight.Deathknight_.IsAlive()) return;

            Deathknight.Deathknight_.RpcSetCustomRole(CustomRoles.Necromancer);
            Add(Deathknight.DeathknightId);

            Deathknight.Deathknight_ = null;
            Deathknight.DeathknightId = byte.MaxValue;
        }

        public override bool KnowRole(PlayerControl player, PlayerControl target)
        {
            if (player.Is(CustomRoles.Undead) && (target.Is(CustomRoles.Necromancer) || target.Is(CustomRoles.Deathknight))) return true;
            if (KnowTargetRole.GetBool() && (player.Is(CustomRoles.Necromancer) || player.Is(CustomRoles.Deathknight)) && target.Is(CustomRoles.Undead)) return true;
            if (player.Is(CustomRoles.Deathknight) && target.Is(CustomRoles.Necromancer)) return true;
            return player.Is(CustomRoles.Necromancer) && target.Is(CustomRoles.Deathknight);
        }

        public static bool CanBeUndead(PlayerControl pc) => pc != null && !pc.Is(CustomRoles.Deathknight) && !pc.Is(CustomRoles.Necromancer) && !pc.Is(CustomRoles.Undead) && !pc.Is(CustomRoles.Loyal);
    }

    internal class Deathknight : RoleBase
    {
        public static byte DeathknightId = byte.MaxValue;
        public static PlayerControl Deathknight_;

        public override bool IsEnable => DeathknightId != byte.MaxValue;

        public override void Init()
        {
            DeathknightId = byte.MaxValue;
            Deathknight_ = null;
        }

        public override void Add(byte playerId)
        {
            DeathknightId = playerId;
            Deathknight_ = Utils.GetPlayerById(playerId);
            if (!UsePets.GetBool()) Deathknight_.ChangeRoleBasis(RoleTypes.Impostor);

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Necromancer.DKCD.GetFloat();

        public override bool CanUseKillButton(PlayerControl player) => player.IsAlive();

        public override bool CanUseImpostorVentButton(PlayerControl pc) => false;

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

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Necromancer), GetString("DeathknightRecruitedPlayer")));
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Necromancer), GetString("RecruitedByDeathknight")));

                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer);

                killer.SetKillCooldown();
                target.RpcGuardAndKill(killer);
                target.RpcGuardAndKill(target);

                Logger.Info($"Recruit: {target.Data?.PlayerName} = {target.GetCustomRole()} + {CustomRoles.Undead}", $"Assign {CustomRoles.Undead}");

                return false;
            }

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Necromancer), GetString("InvalidUndeadTarget")));

            return false;
        }
    }
}