using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Neutral
{
    internal class Sprayer : RoleBase
    {
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
        private static int Id => 643240;

        private static PlayerControl Sprayer_ => GetPlayerById(SprayerId);

        public override bool IsEnable => SprayerId != byte.MaxValue;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Sprayer);
            KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer])
                .SetValueFormat(OptionFormat.Seconds);
            HasImpostorVision = new BooleanOptionItem(Id + 3, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer]);
            CanVent = new BooleanOptionItem(Id + 4, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer]);
            CD = new IntegerOptionItem(Id + 5, "AbilityCooldown", new(0, 90, 1), 15, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer])
                .SetValueFormat(OptionFormat.Seconds);
            UseLimitOpt = new IntegerOptionItem(Id + 6, "AbilityUseLimit", new(1, 90, 1), 5, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer])
                .SetValueFormat(OptionFormat.Times);
            LoweredVision = new FloatOptionItem(Id + 7, "FFA_LowerVision", new(0.05f, 1.5f, 0.05f), 0.25f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer])
                .SetValueFormat(OptionFormat.Multiplier);
            LoweredSpeed = new FloatOptionItem(Id + 8, "FFA_DecreasedSpeed", new(0.05f, 3f, 0.05f), 0.8f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer])
                .SetValueFormat(OptionFormat.Multiplier);
            EffectDuration = new IntegerOptionItem(Id + 10, "NegativeEffectDuration", new(1, 90, 1), 10, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Sprayer])
                .SetValueFormat(OptionFormat.Seconds);
            MaxTrappedTimes = new IntegerOptionItem(Id + 9, "SprayerMaxTrappedTimes", new(1, 90, 1), 3, TabGroup.NeutralRoles)
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
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
        public override bool CanUseSabotage(PlayerControl pc) => pc.IsAlive() && !(UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool());

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            opt.SetVision(HasImpostorVision.GetBool());
            if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool())
                AURoleOptions.PhantomCooldown = CD.GetInt();
            if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool())
                AURoleOptions.ShapeshifterCooldown = CD.GetInt();
        }

        public override bool OnSabotage(PlayerControl pc)
        {
            PlaceTrap();
            return false;
        }

        public override void OnPet(PlayerControl pc)
        {
            PlaceTrap();
        }

        public override bool OnVanish(PlayerControl pc)
        {
            PlaceTrap();
            return false;
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting && !UseUnshiftTrigger.GetBool()) return true;
            PlaceTrap();
            return false;
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
                        LateTask.New(() =>
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