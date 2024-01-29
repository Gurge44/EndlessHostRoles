﻿using System;
using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    public static class Gambler
    {
        private static readonly int Id = 640700;
        public static List<byte> playerIdList = [];

        public static OptionItem KillCooldown;
        public static OptionItem KillDelay;
        public static OptionItem BSRDelay;
        public static OptionItem TPDelay;
        public static OptionItem ShieldDur;
        public static OptionItem FreezeDur;
        public static OptionItem LowVision;
        public static OptionItem LowVisionDur;
        public static OptionItem LowKCD;
        public static OptionItem HighKCD;
        public static OptionItem Speed;
        public static OptionItem SpeedDur;
        public static OptionItem WhatToIgnore;
        public static OptionItem IgnoreMedicShield;
        public static OptionItem IgnoreVeteranAlert;
        public static OptionItem IgnoreCursedWolfAndJinx;
        public static OptionItem IgnorePestilence;
        public static OptionItem PositiveEffectChance;

        public static Dictionary<byte, byte> EffectID = [];
        public static Dictionary<byte, bool> isPositiveEffect = [];

        public static Dictionary<byte, long> waitingDelayedKills = [];
        public static Dictionary<byte, long> isShielded = [];
        public static Dictionary<byte, (float, long)> isSpeedChange = [];
        public static Dictionary<byte, long> isVisionChange = [];

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Gambler);
            KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 60f, 2.5f), 25f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);
            KillDelay = IntegerOptionItem.Create(Id + 11, "GamblerKillDelay", new(0, 10, 1), 3, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);
            ShieldDur = IntegerOptionItem.Create(Id + 12, "GamblerShieldDur", new(1, 30, 1), 15, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);
            FreezeDur = IntegerOptionItem.Create(Id + 13, "GamblerFreezeDur", new(1, 10, 1), 3, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);
            LowVision = FloatOptionItem.Create(Id + 14, "GamblerLowVision", new(0f, 1f, 0.05f), 0.7f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Multiplier);
            LowVisionDur = IntegerOptionItem.Create(Id + 15, "GamblerLowVisionDur", new(1, 30, 1), 10, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);
            Speed = FloatOptionItem.Create(Id + 16, "GamblerSpeedup", new(0.1f, 3f, 0.05f), 1.5f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Multiplier);
            SpeedDur = IntegerOptionItem.Create(Id + 17, "GamblerSpeedupDur", new(1, 20, 1), 5, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);
            BSRDelay = IntegerOptionItem.Create(Id + 18, "GamblerBSRDelay", new(0, 10, 1), 2, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);
            HighKCD = FloatOptionItem.Create(Id + 19, "GamblerHighKCD", new(10f, 60f, 2.5f), 30f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);
            LowKCD = FloatOptionItem.Create(Id + 20, "GamblerLowKCD", new(10f, 60f, 2.5f), 17.5f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);
            TPDelay = IntegerOptionItem.Create(Id + 21, "GamblerTPDelay", new(0, 10, 1), 2, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);
            WhatToIgnore = BooleanOptionItem.Create(Id + 22, "GamblerWhatToIgnore", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler]);
            IgnoreMedicShield = BooleanOptionItem.Create(Id + 23, "GamblerIgnoreMedicShield", true, TabGroup.ImpostorRoles, false).SetParent(WhatToIgnore);
            IgnoreCursedWolfAndJinx = BooleanOptionItem.Create(Id + 24, "GamblerIgnoreCursedWolfAndJinx", true, TabGroup.ImpostorRoles, false).SetParent(WhatToIgnore);
            IgnoreVeteranAlert = BooleanOptionItem.Create(Id + 25, "GamblerIgnoreVeteranAlert", false, TabGroup.ImpostorRoles, false).SetParent(WhatToIgnore);
            IgnorePestilence = BooleanOptionItem.Create(Id + 26, "GamblerIgnorePestilence", false, TabGroup.ImpostorRoles, false).SetParent(WhatToIgnore);
            PositiveEffectChance = IntegerOptionItem.Create(Id + 27, "GamblerPositiveEffectChance", new(0, 100, 5), 70, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Percent);
        }

        public static void Init()
        {
            playerIdList = [];
            EffectID = [];
            waitingDelayedKills = [];
            isSpeedChange = [];
            isVisionChange = [];
            isPositiveEffect = [];
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }

        public static bool IsEnable => playerIdList.Count > 0;

        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;

            if (EffectID.TryGetValue(killer.PlayerId, out var id) && id != byte.MaxValue)
            {
                return true;
            }

            var rd = IRandom.Instance;
            isPositiveEffect[killer.PlayerId] = rd.Next(1, 101) <= PositiveEffectChance.GetInt();
            if (isPositiveEffect[killer.PlayerId])
            {
                EffectID[killer.PlayerId] = (byte)rd.Next(1, 8);
                switch (EffectID[killer.PlayerId])
                {
                    case 1: // Delayed kill
                        killer.Notify(string.Format(GetString("GamblerGet.DelayedKill"), KillDelay.GetInt()));
                        waitingDelayedKills.TryAdd(target.PlayerId, GetTimeStamp());
                        return false;
                    case 2: // Shield
                        killer.Notify(string.Format(GetString("GamblerGet.Shield"), ShieldDur.GetInt()));
                        isShielded.TryAdd(killer.PlayerId, GetTimeStamp());
                        break;
                    case 3: // No lunge (Swift kill)
                        killer.Notify(GetString("GamblerGet.NoLunge"));
                        if (killer.RpcCheckAndMurder(target, true)) target.Kill(target);
                        return false;
                    case 4: // Swap with random player
                        killer.Notify(GetString("GamblerGet.Swap"));
                        _ = new LateTask(() =>
                        {
                            if (GameStates.IsInTask && killer.IsAlive())
                            {
                                var list = Main.AllAlivePlayerControls.Where(a => !Pelican.IsEaten(a.PlayerId) && !a.inVent && a.PlayerId != killer.PlayerId).ToArray();
                                TP(killer.NetTransform, list[rd.Next(0, list.Length)].Pos());
                            }
                        }, TPDelay.GetInt(), "Gambler Swap");
                        break;
                    case 5: // Ignore defense
                        killer.Notify(GetString("GamblerGet.IgnoreDefense"));
                        if ((target.Is(CustomRoles.Pestilence) && IgnorePestilence.GetBool())
                            || (Main.VeteranInProtect.ContainsKey(target.PlayerId) && IgnoreVeteranAlert.GetBool())
                            || (Medic.InProtect(target.PlayerId) && IgnoreMedicShield.GetBool())
                            || ((target.Is(CustomRoles.Jinx) || target.Is(CustomRoles.CursedWolf)) && IgnoreCursedWolfAndJinx.GetBool()))
                        {
                            killer.Kill(target);
                            return false;
                        }
                        else if ((target.Is(CustomRoles.Pestilence) && !IgnorePestilence.GetBool())
                                 || (Main.VeteranInProtect.ContainsKey(target.PlayerId) && !IgnoreVeteranAlert.GetBool())
                                 || (Medic.InProtect(target.PlayerId) && !IgnoreMedicShield.GetBool())
                                 || ((target.Is(CustomRoles.Jinx) || target.Is(CustomRoles.CursedWolf)) && !IgnoreCursedWolfAndJinx.GetBool()))
                        {
                            break;
                        }
                        else
                        {
                            killer.RpcCheckAndMurder(target);
                            return false;
                        }
                    case 6: // Low KCD
                        killer.Notify(string.Format(GetString("GamblerGet.LowKCD"), LowKCD.GetFloat()));
                        _ = new LateTask(() =>
                        {
                            killer.SetKillCooldown(LowKCD.GetFloat());
                        }, 0.1f, "Gambler SetLowKCD");
                        break;
                    case 7: // Speed
                        killer.Notify(string.Format(GetString("GamblerGet.Speedup"), SpeedDur.GetInt(), Speed.GetFloat()));
                        isSpeedChange.TryAdd(killer.PlayerId, (Main.AllPlayerSpeed[killer.PlayerId], GetTimeStamp()));
                        Main.AllPlayerSpeed[killer.PlayerId] = Speed.GetFloat();
                        killer.MarkDirtySettings();
                        break;
                    default:
                        Logger.Error("Invalid Effect ID (positive)", "Gambler.OnCheckMurder");
                        break;
                }
            }
            else
            {
                EffectID[killer.PlayerId] = (byte)rd.Next(1, 5);
                switch (EffectID[killer.PlayerId])
                {
                    case 1: // BSR
                        var delay = Math.Max(0.15f, BSRDelay.GetFloat());
                        if (delay >= 1f)
                        {
                            killer.Notify(string.Format(GetString("GamblerGet.BSR"), BSRDelay.GetInt()));
                        }
                        _ = new LateTask(() => { if (GameStates.IsInTask) killer.CmdReportDeadBody(target.Data); }, delay, "Gambler Self Report");
                        break;
                    case 2: // Freeze
                        killer.Notify(string.Format(GetString("GamblerGet.Freeze"), FreezeDur.GetInt()));
                        isSpeedChange.TryAdd(killer.PlayerId, (Main.AllPlayerSpeed[killer.PlayerId], GetTimeStamp()));
                        Main.AllPlayerSpeed[killer.PlayerId] = Main.MinSpeed;
                        killer.MarkDirtySettings();
                        break;
                    case 3: // Low vision
                        killer.Notify(string.Format(GetString("GamblerGet.LowVision"), LowVisionDur.GetInt(), LowVision.GetFloat()));
                        isVisionChange.TryAdd(killer.PlayerId, GetTimeStamp());
                        killer.MarkDirtySettings();
                        break;
                    case 4: // High KCD
                        killer.Notify(string.Format(GetString("GamblerGet.HighKCD"), HighKCD.GetFloat()));
                        _ = new LateTask(() =>
                        {
                            killer.SetKillCooldown(HighKCD.GetFloat());
                        }, 0.1f, "Gambler SetHighKCD");
                        break;
                    default:
                        Logger.Error("Invalid Effect ID (negative)", "Gambler.OnCheckMurder");
                        break;
                }
            }

            EffectID[killer.PlayerId] = byte.MaxValue;

            return true;
        }

        public static void OnFixedUpdate(PlayerControl player)
        {
            if (!GameStates.IsInTask || player == null || !player.Is(CustomRoles.Gambler) || (waitingDelayedKills.Count == 0 && isSpeedChange.Count == 0 && isVisionChange.Count == 0 && isShielded.Count == 0)) return;

            bool sync = false;

            foreach (var x in waitingDelayedKills)
            {
                var pc = GetPlayerById(x.Key);
                if (!pc.IsAlive())
                {
                    waitingDelayedKills.Remove(x.Key);
                    continue;
                }
                if (x.Value + KillDelay.GetInt() < GetTimeStamp())
                {
                    pc.Suicide(PlayerState.DeathReason.Poison, player);
                    waitingDelayedKills.Remove(x.Key);
                }
            }

            if (isSpeedChange.TryGetValue(player.PlayerId, out var p) && p.Item2 + SpeedDur.GetInt() < GetTimeStamp())
            {
                Main.AllPlayerSpeed[player.PlayerId] = p.Item1;
                isSpeedChange.Remove(player.PlayerId);
                sync = true;
            }

            if (isVisionChange.TryGetValue(player.PlayerId, out var v) && v + LowVisionDur.GetInt() < GetTimeStamp())
            {
                isVisionChange.Remove(player.PlayerId);
                sync = true;
            }

            if (isShielded.TryGetValue(player.PlayerId, out var shielded) && shielded + ShieldDur.GetInt() < GetTimeStamp())
            {
                isShielded.Remove(player.PlayerId);
            }

            if (sync) { player.MarkDirtySettings(); }
        }

        public static void OnReportDeadBody()
        {
            EffectID.Clear();
            isPositiveEffect.Clear();
            foreach (var playerId in waitingDelayedKills.Keys.ToArray())
            {
                var pc = GetPlayerById(playerId);
                if (pc.IsAlive()) pc.Kill(pc);
            }
            waitingDelayedKills.Clear();
            isShielded.Clear();
            isSpeedChange.Clear();
            isVisionChange.Clear();
        }
    }
}
