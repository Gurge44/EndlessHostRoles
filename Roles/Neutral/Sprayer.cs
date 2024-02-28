using AmongUs.GameOptions;
using System;
using System.Collections.Generic;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Neutral
{
    internal class Sprayer : RoleBase
    {
        private static int Id => 643240;

        private static PlayerControl Sprayer_ => GetPlayerById(SprayerId);
        private static byte SprayerId = byte.MaxValue;

        private static OptionItem KillCooldown;
        private static OptionItem HasImpostorVision;
        public static OptionItem CanVent;
        private static OptionItem CD;
        private static OptionItem UseLimitOpt;
        public static OptionItem LoweredVision;
        private static OptionItem LoweredSpeed;
        private static OptionItem EffectDuration;
        private static OptionItem MaxTrappedTimes;

        private static readonly List<Vector2> Traps = [];
        private static readonly Dictionary<byte, int> TrappedCount = [];
        public static readonly List<byte> LowerVisionList = [];
        private static readonly Dictionary<byte, long> LastUpdate = [];

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Sprayer, 1, zeroOne: false);
            KillCooldown = FloatOptionItem.Create(Id + 2, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer])
                .SetValueFormat(OptionFormat.Seconds);
            HasImpostorVision = BooleanOptionItem.Create(Id + 3, "ImpostorVision", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer]);
            CanVent = BooleanOptionItem.Create(Id + 4, "CanVent", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer]);
            CD = IntegerOptionItem.Create(Id + 5, "AbilityCooldown", new(0, 90, 1), 15, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = IntegerOptionItem.Create(Id + 6, "AbilityUseLimit", new(1, 90, 1), 5, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer])
                .SetValueFormat(OptionFormat.Times);
            LoweredVision = FloatOptionItem.Create(Id + 7, "FFA_LowerVision", new(0.05f, 1.5f, 0.05f), 0.25f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer])
                .SetValueFormat(OptionFormat.Multiplier);
            LoweredSpeed = FloatOptionItem.Create(Id + 8, "FFA_DecreasedSpeed", new(0.05f, 3f, 0.05f), 0.8f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer])
                .SetValueFormat(OptionFormat.Multiplier);
            EffectDuration = IntegerOptionItem.Create(Id + 10, "NegativeEffectDuration", new(1, 90, 1), 10, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer])
                .SetValueFormat(OptionFormat.Seconds);
            MaxTrappedTimes = IntegerOptionItem.Create(Id + 9, "SprayerMaxTrappedTimes", new(1, 90, 1), 3, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Init()
        {
            SprayerId = byte.MaxValue;
            Traps.Clear();
            TrappedCount.Clear();
            LowerVisionList.Clear();
            LastUpdate.Clear();
        }

        public override void Add(byte playerId)
        {
            SprayerId = playerId;
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());

            foreach (var pc in Main.AllAlivePlayerControls) TrappedCount[pc.PlayerId] = 0;

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override bool IsEnable => SprayerId != byte.MaxValue;
        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
        public override bool CanUseSabotage(PlayerControl pc) => true;

        public override void OnSabotage(PlayerControl pc)
        {
            PlaceTrap();
        }

        public override void OnPet(PlayerControl pc)
        {
            PlaceTrap();
        }

        void PlaceTrap()
        {
            if (!IsEnable || SprayerId.GetAbilityUseLimit() <= 0 || Sprayer_.HasAbilityCD()) return;

            Traps.Add(Sprayer_.Pos());
            Sprayer_.RpcRemoveAbilityUse();

            if (SprayerId.GetAbilityUseLimit() > 0) Sprayer_.AddAbilityCD(CD.GetInt());

            Sprayer_.Notify(GetString("SprayerNotify"));
        }

        public override void OnCheckPlayerPosition(PlayerControl pc)
        {
            if (!IsEnable || !GameStates.IsInTask || Traps.Count == 0) return;

            long now = TimeStamp;

            if (pc.PlayerId == SprayerId) return;

            LastUpdate.TryAdd(pc.PlayerId, now);
            if (LastUpdate[pc.PlayerId] + 3 > now) return;
            LastUpdate[pc.PlayerId] = now;

            foreach (var trap in Traps)
            {
                if (Vector2.Distance(pc.Pos(), trap) <= 2f)
                {
                    byte playerId = pc.PlayerId;
                    var tempSpeed = Main.AllPlayerSpeed[playerId];
                    Main.AllPlayerSpeed[playerId] = LoweredSpeed.GetFloat();
                    LowerVisionList.Add(playerId);
                    TrappedCount[playerId]++;
                    if (TrappedCount[playerId] > MaxTrappedTimes.GetInt())
                    {
                        pc.Suicide(realKiller: Sprayer_);
                        TrappedCount.Remove(playerId);
                    }
                    else
                    {
                        pc.MarkDirtySettings();
                        _ = new LateTask(() =>
                        {
                            Main.AllPlayerSpeed[playerId] = tempSpeed;
                            LowerVisionList.Remove(playerId);
                            if (GameStates.IsInTask) pc.MarkDirtySettings();
                        }, EffectDuration.GetFloat(), "Sprayer Revert Effects");
                    }
                }
            }
        }

        public override void OnReportDeadBody()
        {
            Traps.Clear();
        }

        public override void AfterMeetingTasks()
        {
            if (SprayerId.GetAbilityUseLimit() > 0)
            {
                Sprayer_.AddAbilityCD(Math.Max(15, CD.GetInt()));
            }
        }
    }
}