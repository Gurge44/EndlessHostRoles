using System.Collections.Generic;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral
{
    internal class Necromancer
    {
        private static int Id => 643450;
        public static byte NecromancerId = byte.MaxValue;
        public static PlayerControl Necromancer_ = null;

        private static OptionItem CD;
        public static OptionItem DKCD;
        private static OptionItem KnowTargetRole;
        public static OptionItem UndeadCountMode;

        private static readonly string[] undeadCountMode =
        [
            "UndeadCountMode.None",
            "UndeadCountMode.Necromancer",
            "UndeadCountMode.Original",
        ];

        public static readonly List<byte> PartiallyRecruitedIds = [];

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Necromancer, 1, zeroOne: false);
            CD = FloatOptionItem.Create(Id + 2, "NecromancerCD", new(0f, 180f, 2.5f), 30f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Necromancer])
                .SetValueFormat(OptionFormat.Seconds);
            DKCD = FloatOptionItem.Create(Id + 10, "DKCD", new(0f, 180f, 2.5f), 30f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Necromancer])
                .SetValueFormat(OptionFormat.Seconds);
            KnowTargetRole = BooleanOptionItem.Create(Id + 13, "NecromancerKnowTargetRole", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Necromancer]);
            UndeadCountMode = StringOptionItem.Create(Id + 15, "UndeadCountMode", undeadCountMode, 0, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Necromancer]);
        }

        public static void Init()
        {
            NecromancerId = byte.MaxValue;
            Necromancer_ = null;

            PartiallyRecruitedIds.Clear();

            Deathknight.Init();
        }

        public static void Add(byte playerId)
        {
            NecromancerId = playerId;
            Necromancer_ = Utils.GetPlayerById(playerId);

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public static bool IsEnable => NecromancerId != byte.MaxValue;

        public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = CD.GetFloat();

        public static bool CanUseKillButton(PlayerControl player) => player.IsAlive();

        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!Deathknight.IsEnable)
            {
                target.RpcSetCustomRole(CustomRoles.Deathknight);
                Deathknight.Add(target.PlayerId);

                killer.SetKillCooldown();

                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target);

                target.MarkDirtySettings();
                target.ResetKillCooldown();
                target.SetKillCooldown();

                target.Notify(GetString("RecruitedToDeathknight"));

                return;
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

                Logger.Info($"Partial Recruit: {target.GetRealName()}", $"Necromancer");

                return;
            }

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Necromancer), GetString("InvalidUndeadTarget")));
        }

        public static bool KnowRole(PlayerControl player, PlayerControl target)
        {
            if (player.Is(CustomRoles.Undead) && (target.Is(CustomRoles.Necromancer) || target.Is(CustomRoles.Deathknight))) return true;
            if (KnowTargetRole.GetBool() && (player.Is(CustomRoles.Necromancer) || player.Is(CustomRoles.Deathknight)) && target.Is(CustomRoles.Undead)) return true;
            if (player.Is(CustomRoles.Deathknight) && target.Is(CustomRoles.Necromancer)) return true;
            if (player.Is(CustomRoles.Necromancer) && target.Is(CustomRoles.Deathknight)) return true;
            return false;
        }

        public static bool CanBeUndead(PlayerControl pc) => pc != null && !pc.Is(CustomRoles.Deathknight) && !pc.Is(CustomRoles.Necromancer) && !pc.Is(CustomRoles.Undead) && !pc.Is(CustomRoles.Loyal);
    }

    internal class Deathknight
    {
        public static byte DeathknightId = byte.MaxValue;
        public static PlayerControl Deathknight_ = null;

        public static void Init()
        {
            DeathknightId = byte.MaxValue;
            Deathknight_ = null;
        }

        public static void Add(byte playerId)
        {
            DeathknightId = playerId;
            Deathknight_ = Utils.GetPlayerById(playerId);

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public static bool IsEnable => DeathknightId != byte.MaxValue;

        public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Necromancer.DKCD.GetFloat();

        public static bool CanUseKillButton(PlayerControl player) => player.IsAlive();

        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
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

                Logger.Info($"Recruit: {target?.Data?.PlayerName} = {target.GetCustomRole()} + {CustomRoles.Undead}", $"Assign {CustomRoles.Undead}");

                return;
            }

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Necromancer), GetString("InvalidUndeadTarget")));
        }
    }
}
