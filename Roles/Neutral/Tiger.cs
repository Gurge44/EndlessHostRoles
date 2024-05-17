﻿using System.Linq;
using EHR.Modules;
using UnityEngine;

namespace EHR.Roles.Neutral
{
    internal class Tiger : RoleBase
    {
        private const int Id = 643500;

        public static OptionItem Radius;
        public static OptionItem EnrageCooldown;
        public static OptionItem EnrageDuration;
        public static OptionItem KillCooldown;
        public static OptionItem CanVent;

        public static bool On;

        private int Count;

        public float EnrageTimer;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Tiger);
            Radius = FloatOptionItem.Create(Id + 2, "TigerRadius", new(0.5f, 10f, 0.5f), 3f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger])
                .SetValueFormat(OptionFormat.Multiplier);
            EnrageCooldown = FloatOptionItem.Create(Id + 3, "EnrageCooldown", new(0f, 60f, 0.5f), 30f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger])
                .SetValueFormat(OptionFormat.Seconds);
            EnrageDuration = FloatOptionItem.Create(Id + 4, "EnrageDuration", new(1f, 30f, 1f), 10f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger])
                .SetValueFormat(OptionFormat.Seconds);
            KillCooldown = FloatOptionItem.Create(Id + 5, "KillCooldown", new(0f, 60f, 0.5f), 30f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = BooleanOptionItem.Create(Id + 6, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger]);
        }

        public override void Add(byte playerId)
        {
            On = true;
            EnrageTimer = float.NaN;
        }

        public override void Init()
        {
            On = false;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return CanVent.GetBool();
        }

        public override bool CanUseSabotage(PlayerControl pc)
        {
            return pc.IsAlive();
        }

        public override bool OnSabotage(PlayerControl pc)
        {
            if (!pc.HasAbilityCD())
            {
                StartEnraging();
                pc.AddAbilityCD();
            }

            return false;
        }

        public override void OnPet(PlayerControl pc)
        {
            StartEnraging();
        }

        void StartEnraging()
        {
            EnrageTimer = EnrageDuration.GetFloat();
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (float.IsNaN(EnrageTimer)) return;

            EnrageTimer -= Time.fixedDeltaTime;

            Count++;
            if (Count < 10) return;
            Count = 0;

            Utils.SendRPC(CustomRPC.SyncTiger, pc.PlayerId, EnrageTimer);

            switch (EnrageTimer)
            {
                case <= 0f:
                    EnrageTimer = float.NaN;
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                    break;
                case <= 5f:
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                    break;
            }
        }

        public override void OnMurder(PlayerControl killer, PlayerControl target)
        {
            if (float.IsNaN(EnrageTimer)) return;

            var victim = Main.AllAlivePlayerControls.Where(x => x.PlayerId != killer.PlayerId && x.PlayerId != target.PlayerId).MinBy(x => Vector2.Distance(killer.Pos(), x.Pos()));
            if (victim == null || Vector2.Distance(killer.Pos(), victim.Pos()) > Radius.GetFloat()) return;

            if (killer.RpcCheckAndMurder(victim, check: true))
            {
                victim.Suicide(realKiller: killer);
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool m = false)
        {
            if (seer.PlayerId != target.PlayerId) return string.Empty;
            if (Main.PlayerStates[seer.PlayerId].Role is not Tiger { IsEnable: true } tg) return string.Empty;
            if (float.IsNaN(tg.EnrageTimer)) return string.Empty;
            return tg.EnrageTimer > 5 ? "\u25a9" : $"\u25a9 ({(int)(tg.EnrageTimer + 1)}s)";
        }
    }
}