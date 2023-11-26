using AmongUs.GameOptions;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    /*
     * Tiers and their perks
     * 
     * Tier 0: Regular Impostor, higher KCD: normal KCD * 1.25, short kill range
     * Tier 1: Increased Kill Range to Long, normal KCD
     * Tier 2: Lower KCD: normal KCD * 0.85
     * Tier 3: Dual Pistols (2 separate KCDs, KCDs = normal KCD * 1.5)
     * Tier 4: Normal KCD for both pistols
     * Tier 5: Delayed Kills
     * 
     * XP rewards
     * 
     * Kill, Sabotage, Unique vent usage, Non-teamed player ejected
     * XP needed to level up: 100
     * 
     */
    public static class Mafioso
    {
        private static readonly int Id = 642200;
        private static List<byte> playerIdList = [];

        private static OptionItem Delay;
        private static OptionItem RewardForKilling;
        private static OptionItem RewardForSabotaging;
        private static OptionItem RewardForVenting;
        private static OptionItem RewardForOtherPlayerEjected;

        private static List<int> PreviouslyUsedVents = [];

        private static int Tier;
        private static int XP;

        private static int Pistol1CD;
        private static int Pistol2CD;
        private static long lastUpdate;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Mafioso, 1);
            Delay = IntegerOptionItem.Create(Id + 10, "MafiosoDelay", new(1, 10, 1), 3, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mafioso])
                .SetValueFormat(OptionFormat.Seconds);
            RewardForKilling = IntegerOptionItem.Create(Id + 11, "MafiosoRewardForKilling", new(0, 100, 5), 40, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mafioso]);
            RewardForSabotaging = IntegerOptionItem.Create(Id + 12, "MafiosoRewardForSabotaging", new(0, 100, 5), 25, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mafioso]);
            RewardForVenting = IntegerOptionItem.Create(Id + 13, "MafiosoRewardForVenting", new(0, 100, 5), 10, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mafioso]);
            RewardForOtherPlayerEjected = IntegerOptionItem.Create(Id + 14, "MafiosoRewardForOtherPlayerEjected", new(0, 100, 5), 30, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mafioso]);
        }

        public static void Init()
        {
            playerIdList = [];
            PreviouslyUsedVents = [];
            Tier = 0;
            XP = 0;
            Pistol1CD = 0;
            Pistol2CD = 0;
            lastUpdate = GetTimeStamp() + 30;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            lastUpdate = GetTimeStamp() + 8;
        }
        public static void ApplyGameOptions(IGameOptions opt)
        {
            opt.SetInt(Int32OptionNames.KillDistance, Tier > 0 ? 2 : 0);
        }
        public static void SetKillCooldown(byte playerId)
        {
            Main.AllPlayerKillCooldown[playerId] = Tier switch
            {
                0 => (float)Math.Round(DefaultKillCooldown * 1.25, 2),
                1 => DefaultKillCooldown,
                2 => (float)Math.Round(DefaultKillCooldown * 0.85, 2),
                _ => 1f
            };
        }
        public static void SendRPC()
        {
            if (!IsEnable) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncMafiosoData, SendOption.Reliable, -1);
            writer.Write(Tier);
            writer.Write(XP);
            writer.Write(PreviouslyUsedVents.Count);
            if (PreviouslyUsedVents.Any()) foreach (var vent in PreviouslyUsedVents.ToArray()) writer.Write(vent);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            if (!IsEnable) return;
            Tier = reader.ReadInt32();
            XP = reader.ReadInt32();
            var elements = reader.ReadInt32();
            if (elements > 0) for (int i = 0; i < elements; i++) PreviouslyUsedVents.Add(reader.ReadInt32());
        }
        public static void SendRPCSyncPistolCD()
        {
            if (!IsEnable) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncMafiosoPistolCD, SendOption.Reliable, -1);
            writer.Write(Pistol1CD);
            writer.Write(Pistol2CD);
            writer.Write(lastUpdate);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPCSyncPistolCD(MessageReader reader)
        {
            if (!IsEnable) return;
            Pistol1CD = reader.ReadInt32();
            Pistol2CD = reader.ReadInt32();
            lastUpdate = long.Parse(reader.ReadString());
        }
        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (!GameStates.IsInTask || !pc.IsAlive() || pc == null || !pc.Is(CustomRoles.Mafioso) || !playerIdList.Any() || lastUpdate >= GetTimeStamp()) return;

            if (XP >= 100 && Tier < 5)
            {
                XP -= 100;
                Tier++;

                SendRPC();
                pc.MarkDirtySettings();
                pc.ResetKillCooldown();
                pc.Notify(GetString("MafiosoLevelUp"));
            }

            if (lastUpdate >= GetTimeStamp()) return;
            lastUpdate = GetTimeStamp();

            var before1CD = Pistol1CD;
            var before2CD = Pistol2CD;

            if (Pistol1CD > 0) Pistol1CD--;
            if (Pistol2CD > 0) Pistol2CD--;

            if (before1CD != Pistol1CD || before2CD != Pistol2CD)
            {
                NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                SendRPCSyncPistolCD();
                Logger.Info($"Pistol 1 CD: {Pistol1CD}; Pistol 2 CD: {Pistol2CD}", "debug");
            }
        }
        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!GameStates.IsInTask || target == null || killer == null || !killer.Is(CustomRoles.Mafioso) || Tier < 3 || !playerIdList.Any()) return true;

            if (Pistol1CD > 0 && Pistol2CD > 0)
            {
                return false;
            }

            int KCD = Tier >= 4 ? (int)Math.Round(DefaultKillCooldown) : (int)Math.Round(DefaultKillCooldown * 1.5);
            KCD++;

            if (Pistol1CD <= 0 && Pistol2CD > 0)
            {
                Pistol1CD = KCD;
                NotifyRoles(SpecifySeer: killer, SpecifyTarget: killer);
            }
            if (Pistol2CD <= 0 && Pistol1CD > 0)
            {
                Pistol2CD = KCD;
                NotifyRoles(SpecifySeer: killer, SpecifyTarget: killer);
            }

            if (Tier >= 5)
            {
                _ = new LateTask(() =>
                {
                    if (target.IsAlive() && killer.IsAlive() && GameStates.IsInTask)
                    {
                        killer.RpcCheckAndMurder(target);
                    }
                }, Delay.GetInt(), "Mafioso Tier 5 Kill Delay");

                return false;
            }

            if (Pistol1CD > 1 && Pistol2CD > 1)
            {
                _ = new LateTask(() =>
                {
                    killer.SetKillCooldown(time: Math.Min(Pistol1CD, Pistol2CD) - 1);
                }, 0.1f, "Mafioso SetKillCooldown");
            }

            return true;
        }
        public static bool IsEnable => playerIdList.Any();
        public static void OnReportDeadBody()
        {
            if (!IsEnable) return;
            PreviouslyUsedVents.Clear();
            int KCD = Tier >= 4 ? (int)Math.Round(DefaultKillCooldown) : (int)Math.Round(DefaultKillCooldown * 1.5);
            KCD++;
            Pistol1CD = KCD;
            Pistol2CD = KCD;
            lastUpdate = GetTimeStamp();
            SendRPC();
            SendRPCSyncPistolCD();
        }
        public static string GetProgressText() => string.Format(GetString("MafiosoProgressText"), Tier, XP);
        public static string GetHUDText()
        {
            if (Tier >= 3)
            {
                string CD;
                if (Pistol1CD <= 0 && Pistol2CD <= 0) CD = "<color=#00ff00>Can Kill</color>";
                else CD = $"<color=#ff1919>CD:</color> <b>{Math.Min(Pistol1CD, Pistol2CD)}</b>s";
                return string.Format(GetString("MafiosoHUDTextWithDualPistols"), Tier, XP, CD);
            }
            else
            {
                return string.Format(GetString("MafiosoHUDText"), Tier, XP);
            }
        }

        public static void OnMurder()
        {
            if (!IsEnable) return;
            XP += RewardForKilling.GetInt();
            SendRPC();
        }
        public static void OnEnterVent(int ventId)
        {
            if (!IsEnable) return;
            if (PreviouslyUsedVents.Contains(ventId)) return;

            PreviouslyUsedVents.Add(ventId);
            XP += RewardForVenting.GetInt();
            SendRPC();
        }
        public static void OnSabotage()
        {
            if (!IsEnable) return;
            XP += RewardForSabotaging.GetInt();
            SendRPC();
        }
        public static void OnCrewmateEjected()
        {
            if (!IsEnable) return;
            XP += RewardForOtherPlayerEjected.GetInt();
            SendRPC();
        }
    }
}
