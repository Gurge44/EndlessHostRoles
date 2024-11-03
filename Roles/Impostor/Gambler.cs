using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Neutral;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Impostor
{
    public class Gambler : RoleBase
    {
        private const int Id = 640700;
        private static List<byte> PlayerIdList = [];

        private static OptionItem KillCooldown;
        private static OptionItem KillDelay;
        private static OptionItem BsrDelay;
        private static OptionItem TPDelay;
        private static OptionItem ShieldDur;
        private static OptionItem FreezeDur;
        private static OptionItem LowVision;
        private static OptionItem LowVisionDur;
        private static OptionItem LowKCD;
        private static OptionItem HighKCD;
        private static OptionItem Speed;
        private static OptionItem SpeedDur;
        private static OptionItem WhatToIgnore;
        private static OptionItem IgnoreMedicShield;
        private static OptionItem IgnoreVeteranAlert;
        private static OptionItem IgnoreCursedWolfAndJinx;
        private static OptionItem IgnorePestilence;
        private static OptionItem PositiveEffectChance;

        private static Dictionary<byte, long> WaitingDelayedKills = [];
        public static readonly Dictionary<byte, long> IsShielded = [];
        private static Dictionary<byte, (float, long)> IsSpeedChange = [];
        private static Dictionary<byte, long> IsVisionChange = [];

        private byte EffectID = byte.MaxValue;
        private bool isPositiveEffect;

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Gambler);

            KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 60f, 2.5f), 25f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);

            KillDelay = new IntegerOptionItem(Id + 11, "GamblerKillDelay", new(0, 10, 1), 3, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);

            ShieldDur = new IntegerOptionItem(Id + 12, "GamblerShieldDur", new(1, 30, 1), 15, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);

            FreezeDur = new IntegerOptionItem(Id + 13, "GamblerFreezeDur", new(1, 10, 1), 3, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);

            LowVision = new FloatOptionItem(Id + 14, "GamblerLowVision", new(0f, 1f, 0.05f), 0.7f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Multiplier);

            LowVisionDur = new IntegerOptionItem(Id + 15, "GamblerLowVisionDur", new(1, 30, 1), 10, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);

            Speed = new FloatOptionItem(Id + 16, "GamblerSpeedup", new(0.1f, 3f, 0.05f), 1.5f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Multiplier);

            SpeedDur = new IntegerOptionItem(Id + 17, "GamblerSpeedupDur", new(1, 20, 1), 5, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);

            BsrDelay = new IntegerOptionItem(Id + 18, "GamblerBSRDelay", new(0, 10, 1), 2, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);

            HighKCD = new FloatOptionItem(Id + 19, "GamblerHighKCD", new(10f, 60f, 2.5f), 30f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);

            LowKCD = new FloatOptionItem(Id + 20, "GamblerLowKCD", new(10f, 60f, 2.5f), 17.5f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);

            TPDelay = new IntegerOptionItem(Id + 21, "GamblerTPDelay", new(0, 10, 1), 2, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Seconds);

            WhatToIgnore = new BooleanOptionItem(Id + 22, "GamblerWhatToIgnore", true, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Gambler]);
            IgnoreMedicShield = new BooleanOptionItem(Id + 23, "GamblerIgnoreMedicShield", true, TabGroup.ImpostorRoles).SetParent(WhatToIgnore);
            IgnoreCursedWolfAndJinx = new BooleanOptionItem(Id + 24, "GamblerIgnoreCursedWolfAndJinx", true, TabGroup.ImpostorRoles).SetParent(WhatToIgnore);
            IgnoreVeteranAlert = new BooleanOptionItem(Id + 25, "GamblerIgnoreVeteranAlert", false, TabGroup.ImpostorRoles).SetParent(WhatToIgnore);
            IgnorePestilence = new BooleanOptionItem(Id + 26, "GamblerIgnorePestilence", false, TabGroup.ImpostorRoles).SetParent(WhatToIgnore);

            PositiveEffectChance = new IntegerOptionItem(Id + 27, "GamblerPositiveEffectChance", new(0, 100, 5), 70, TabGroup.ImpostorRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Gambler])
                .SetValueFormat(OptionFormat.Percent);
        }

        public override void Init()
        {
            PlayerIdList = [];
            EffectID = byte.MaxValue;
            WaitingDelayedKills = [];
            IsSpeedChange = [];
            IsVisionChange = [];
            isPositiveEffect = true;
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            EffectID = byte.MaxValue;
            isPositiveEffect = true;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (IsVisionChange.ContainsKey(playerId))
            {
                opt.SetFloat(FloatOptionNames.CrewLightMod, LowVision.GetFloat());
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, LowVision.GetFloat());
            }
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;

            if (target == null) return false;

            if (EffectID != byte.MaxValue) return true;

            var rd = IRandom.Instance;
            isPositiveEffect = rd.Next(1, 101) <= PositiveEffectChance.GetInt();

            if (isPositiveEffect)
            {
                EffectID = (byte)rd.Next(1, 8);

                switch (EffectID)
                {
                    case 1: // Delayed kill
                        killer.Notify(string.Format(GetString("GamblerGet.DelayedKill"), KillDelay.GetInt()));
                        WaitingDelayedKills.TryAdd(target.PlayerId, TimeStamp);
                        return false;
                    case 2: // Shield
                        killer.Notify(string.Format(GetString("GamblerGet.Shield"), ShieldDur.GetInt()));
                        IsShielded.TryAdd(killer.PlayerId, TimeStamp);
                        break;
                    case 3: // No lunge (Swift kill)
                        killer.Notify(GetString("GamblerGet.NoLunge"));
                        if (killer.RpcCheckAndMurder(target, true)) target.Kill(target);

                        return false;
                    case 4: // Swap with random player
                        killer.Notify(GetString("GamblerGet.Swap"));

                        LateTask.New(() =>
                        {
                            if (GameStates.IsInTask && killer.IsAlive())
                            {
                                PlayerControl[] list = Main.AllAlivePlayerControls.Where(a => !Pelican.IsEaten(a.PlayerId) && !a.inVent && a.PlayerId != killer.PlayerId).ToArray();
                                TP(killer.NetTransform, list.RandomElement().Pos());
                            }
                        }, TPDelay.GetInt(), "Gambler Swap");

                        break;
                    case 5: // Ignore defense
                        killer.Notify(GetString("GamblerGet.IgnoreDefense"));

                        if ((target.Is(CustomRoles.Pestilence) && IgnorePestilence.GetBool())
                            || (Veteran.VeteranInProtect.ContainsKey(target.PlayerId) && IgnoreVeteranAlert.GetBool())
                            || (Medic.InProtect(target.PlayerId) && IgnoreMedicShield.GetBool())
                            || ((target.Is(CustomRoles.Jinx) || target.Is(CustomRoles.CursedWolf)) && IgnoreCursedWolfAndJinx.GetBool()))
                        {
                            killer.Kill(target);
                            return false;
                        }

                        if ((target.Is(CustomRoles.Pestilence) && !IgnorePestilence.GetBool())
                            || (Veteran.VeteranInProtect.ContainsKey(target.PlayerId) && !IgnoreVeteranAlert.GetBool())
                            || (Medic.InProtect(target.PlayerId) && !IgnoreMedicShield.GetBool())
                            || ((target.Is(CustomRoles.Jinx) || target.Is(CustomRoles.CursedWolf)) && !IgnoreCursedWolfAndJinx.GetBool()))
                            break;

                        killer.RpcCheckAndMurder(target);
                        return false;
                    case 6: // Low KCD
                        killer.Notify(string.Format(GetString("GamblerGet.LowKCD"), LowKCD.GetFloat()));
                        LateTask.New(() => { killer.SetKillCooldown(LowKCD.GetFloat()); }, 0.1f, "Gambler SetLowKCD");
                        break;
                    case 7: // Speed
                        killer.Notify(string.Format(GetString("GamblerGet.Speedup"), SpeedDur.GetInt(), Speed.GetFloat()));
                        IsSpeedChange.TryAdd(killer.PlayerId, (Main.AllPlayerSpeed[killer.PlayerId], TimeStamp));
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
                EffectID = (byte)rd.Next(1, 5);

                switch (EffectID)
                {
                    case 1: // BSR
                        float delay = Math.Max(0.15f, BsrDelay.GetFloat());
                        if (delay >= 1f) killer.Notify(string.Format(GetString("GamblerGet.BSR"), BsrDelay.GetInt()));

                        LateTask.New(() =>
                        {
                            if (GameStates.IsInTask) killer.CmdReportDeadBody(target.Data);
                        }, delay, "Gambler Self Report");

                        break;
                    case 2: // Freeze
                        killer.Notify(string.Format(GetString("GamblerGet.Freeze"), FreezeDur.GetInt()));
                        IsSpeedChange.TryAdd(killer.PlayerId, (Main.AllPlayerSpeed[killer.PlayerId], TimeStamp));
                        Main.AllPlayerSpeed[killer.PlayerId] = Main.MinSpeed;
                        killer.MarkDirtySettings();
                        break;
                    case 3: // Low vision
                        killer.Notify(string.Format(GetString("GamblerGet.LowVision"), LowVisionDur.GetInt(), LowVision.GetFloat()));
                        IsVisionChange.TryAdd(killer.PlayerId, TimeStamp);
                        killer.MarkDirtySettings();
                        break;
                    case 4: // High KCD
                        killer.Notify(string.Format(GetString("GamblerGet.HighKCD"), HighKCD.GetFloat()));
                        LateTask.New(() => { killer.SetKillCooldown(HighKCD.GetFloat()); }, 0.1f, "Gambler SetHighKCD");
                        break;
                    default:
                        Logger.Error("Invalid Effect ID (negative)", "Gambler.OnCheckMurder");
                        break;
                }
            }

            EffectID = byte.MaxValue;

            return true;
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (!GameStates.IsInTask || player == null || !player.Is(CustomRoles.Gambler) || (WaitingDelayedKills.Count == 0 && IsSpeedChange.Count == 0 && IsVisionChange.Count == 0 && IsShielded.Count == 0)) return;

            var sync = false;

            foreach (KeyValuePair<byte, long> x in WaitingDelayedKills)
            {
                PlayerControl pc = GetPlayerById(x.Key);

                if (!pc.IsAlive())
                {
                    WaitingDelayedKills.Remove(x.Key);
                    continue;
                }

                if (x.Value + KillDelay.GetInt() < TimeStamp)
                {
                    pc.Suicide(PlayerState.DeathReason.Poison, player);
                    WaitingDelayedKills.Remove(x.Key);
                }
            }

            if (IsSpeedChange.TryGetValue(player.PlayerId, out (float, long) p) && p.Item2 + SpeedDur.GetInt() < TimeStamp)
            {
                Main.AllPlayerSpeed[player.PlayerId] = p.Item1;
                IsSpeedChange.Remove(player.PlayerId);
                sync = true;
            }

            if (IsVisionChange.TryGetValue(player.PlayerId, out long v) && v + LowVisionDur.GetInt() < TimeStamp)
            {
                IsVisionChange.Remove(player.PlayerId);
                sync = true;
            }

            if (IsShielded.TryGetValue(player.PlayerId, out long shielded) && shielded + ShieldDur.GetInt() < TimeStamp) IsShielded.Remove(player.PlayerId);

            if (sync) player.MarkDirtySettings();
        }

        public override void OnReportDeadBody()
        {
            EffectID = byte.MaxValue;
            isPositiveEffect = true;

            foreach (byte playerId in WaitingDelayedKills.Keys.ToArray())
            {
                PlayerControl pc = GetPlayerById(playerId);
                if (pc.IsAlive()) pc.Kill(pc);
            }

            WaitingDelayedKills.Clear();
            IsShielded.Clear();
            IsSpeedChange.Clear();
            IsVisionChange.Clear();
        }
    }
}