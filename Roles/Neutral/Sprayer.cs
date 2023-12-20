using AmongUs.GameOptions;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Utils;

namespace TOHE.Roles.Neutral
{
    internal class Sprayer
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

        private static int UseLimit = 0;
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
        public static void Init()
        {
            SprayerId = byte.MaxValue;
            Traps.Clear();
            TrappedCount.Clear();
            LowerVisionList.Clear();
            LastUpdate.Clear();
            UseLimit = 0;
        }
        public static void Add(byte playerId)
        {
            SprayerId = playerId;
            UseLimit = UseLimitOpt.GetInt();

            foreach (var pc in Main.AllAlivePlayerControls) TrappedCount[pc.PlayerId] = 0;

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }
        public static bool IsEnable => SprayerId != byte.MaxValue;
        public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
        private static void SendRPC()
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncSprayer, SendOption.Reliable, -1);
            writer.Write(UseLimit);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader) => UseLimit = reader.ReadInt32();
        public static void PlaceTrap()
        {
            if (!IsEnable || Sprayer_.HasAbilityCD() || UseLimit <= 0) return;

            Traps.Add(Sprayer_.Pos());
            UseLimit--;
            SendRPC();

            if (UseLimit > 0) Sprayer_.AddAbilityCD(CD.GetInt());
        }
        public static void OnFixedUpdate()
        {
            if (!IsEnable || !GameStates.IsInTask || !Traps.Any()) return;

            long now = GetTimeStamp();

            foreach (var pc in Main.AllAlivePlayerControls)
            {
                if (pc.PlayerId == SprayerId) continue;

                if (!LastUpdate.ContainsKey(pc.PlayerId)) LastUpdate.Add(pc.PlayerId, now);
                if (LastUpdate[pc.PlayerId] + 3 > now) continue;
                LastUpdate[pc.PlayerId] = now;

                foreach (var trap in Traps.ToArray())
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
        }
        public static void OnReportDeadBody()
        {
            Traps.Clear();
        }
        public static void AfterMeetingTasks()
        {
            if (UseLimit > 0)
            {
                Sprayer_.AddAbilityCD(Math.Max(15, CD.GetInt()));
            }
        }
        public static string ProgressText => $"<#777777>-</color> <#ffffff>{UseLimit}</color>";
    }
}
