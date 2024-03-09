using AmongUs.GameOptions;
using Hazel;
using System;
using System.Collections.Generic;
using TOHE.Modules;
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
    public class Mafioso : RoleBase
    {
        private const int Id = 642200;
        private static List<byte> playerIdList = [];

        private static OptionItem Delay;
        private static OptionItem RewardForKilling;
        private static OptionItem RewardForSabotaging;
        private static OptionItem RewardForVenting;
        private static OptionItem RewardForOtherPlayerEjected;

        private List<int> PreviouslyUsedVents = [];

        private int Tier;
        private int XP;

        private int Pistol1CD;
        private int Pistol2CD;
        private long lastUpdate;

        private byte MafiosoId;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Mafioso);
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

        public override void Init()
        {
            playerIdList = [];
            PreviouslyUsedVents = [];
            Tier = 0;
            XP = 0;
            Pistol1CD = 0;
            Pistol2CD = 0;
            lastUpdate = TimeStamp + 30;
            MafiosoId = byte.MaxValue;
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            lastUpdate = TimeStamp + 8;
            MafiosoId = playerId;
            PreviouslyUsedVents = [];
            Tier = 0;
            XP = 0;
            Pistol1CD = 0;
            Pistol2CD = 0;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            opt.SetInt(Int32OptionNames.KillDistance, Tier > 0 ? 2 : 0);
        }

        public override void SetKillCooldown(byte playerId)
        {
            Main.AllPlayerKillCooldown[playerId] = Tier switch
            {
                0 => (float)Math.Round(DefaultKillCooldown * 1.25, 2),
                1 => DefaultKillCooldown,
                2 => (float)Math.Round(DefaultKillCooldown * 0.85, 2),
                _ => 1f
            };
        }

        void SendRPC()
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncMafiosoData, SendOption.Reliable);
            writer.Write(MafiosoId);
            writer.Write(Tier);
            writer.Write(XP);
            writer.Write(PreviouslyUsedVents.Count);
            if (PreviouslyUsedVents.Count > 0)
                foreach (var vent in PreviouslyUsedVents.ToArray())
                    writer.Write(vent);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public void ReceiveRPC(MessageReader reader)
        {
            Tier = reader.ReadInt32();
            XP = reader.ReadInt32();
            PreviouslyUsedVents.Clear();
            var elements = reader.ReadInt32();
            if (elements > 0)
                for (int i = 0; i < elements; i++)
                    PreviouslyUsedVents.Add(reader.ReadInt32());
        }

        void SendRPCSyncPistolCD()
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncMafiosoPistolCD, SendOption.Reliable);
            writer.Write(MafiosoId);
            writer.Write(Pistol1CD);
            writer.Write(Pistol2CD);
            writer.Write(lastUpdate.ToString());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public void ReceiveRPCSyncPistolCD(MessageReader reader)
        {
            Pistol1CD = reader.ReadInt32();
            Pistol2CD = reader.ReadInt32();
            lastUpdate = long.Parse(reader.ReadString());
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!GameStates.IsInTask || !IsEnable || pc == null || !pc.IsAlive()) return;

            if (XP >= 100 && Tier < 5)
            {
                XP -= 100;
                Tier++;

                SendRPC();
                pc.MarkDirtySettings();
                pc.ResetKillCooldown();
                pc.Notify(GetString("MafiosoLevelUp"));
            }

            if (lastUpdate >= TimeStamp) return;
            lastUpdate = TimeStamp;

            var before1CD = Pistol1CD;
            var before2CD = Pistol2CD;

            if (Pistol1CD > 0) Pistol1CD--;
            if (Pistol2CD > 0) Pistol2CD--;

            if (before1CD != Pistol1CD || before2CD != Pistol2CD)
            {
                NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                if (pc.IsNonHostModClient()) SendRPCSyncPistolCD();
            }
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!GameStates.IsInTask || target == null || killer == null || !killer.Is(CustomRoles.Mafioso) || Tier < 3 || !IsEnable) return true;

            if (Pistol1CD > 0 && Pistol2CD > 0)
            {
                return false;
            }

            int KCD = Tier >= 4 ? (int)Math.Round(DefaultKillCooldown) : (int)Math.Round(DefaultKillCooldown * 1.5);
            KCD++;

            if (Pistol1CD <= 0)
            {
                Pistol1CD = KCD;
                NotifyRoles(SpecifySeer: killer, SpecifyTarget: killer);
            }
            else if (Pistol2CD <= 0)
            {
                Pistol2CD = KCD;
                NotifyRoles(SpecifySeer: killer, SpecifyTarget: killer);
            }

            if (Tier >= 5)
            {
                _ = new LateTask(() =>
                {
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse ---- Can be null since it's a task that completes later
                    if (target != null && target.IsAlive() && GameStates.IsInTask)
                    {
                        target.Suicide(realKiller: killer);
                    }
                }, Delay.GetInt(), "Mafioso Tier 5 Kill Delay");

                return false;
            }

            if (Pistol1CD > 1 && Pistol2CD > 1)
            {
                _ = new LateTask(() => { killer.SetKillCooldown(time: Math.Min(Pistol1CD, Pistol2CD) - 1); }, 0.1f, "Mafioso SetKillCooldown");
            }

            return true;
        }

        public override bool IsEnable => playerIdList.Count > 0;

        public override void OnReportDeadBody()
        {
            if (!IsEnable) return;
            PreviouslyUsedVents.Clear();
            int KCD = Tier >= 4 ? (int)Math.Round(DefaultKillCooldown) : (int)Math.Round(DefaultKillCooldown * 1.5);
            KCD++;
            Pistol1CD = KCD;
            Pistol2CD = KCD;
            lastUpdate = TimeStamp;
            SendRPC();
            SendRPCSyncPistolCD();
        }

        public override string GetProgressText(byte id, bool comms) => string.Format(GetString("MafiosoProgressText"), Tier, XP);

        public static string GetHUDText(PlayerControl pc)
        {
            if (Main.PlayerStates[pc.PlayerId].Role is not Mafioso mo || !mo.IsEnable) return string.Empty;

            if (mo.Tier >= 3)
            {
                string CD;
                if (mo.Pistol1CD <= 0 && mo.Pistol2CD <= 0) CD = "<color=#00ff00>Can Kill</color>";
                else CD = $"<color=#ff1919>CD:</color> <b>{Math.Min(mo.Pistol1CD, mo.Pistol2CD)}</b>s";
                return string.Format(GetString("MafiosoHUDTextWithDualPistols"), mo.Tier, mo.XP, CD);
            }

            return string.Format(GetString("MafiosoHUDText"), mo.Tier, mo.XP);
        }

        public override void OnMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable) return;
            XP += RewardForKilling.GetInt();
            SendRPC();
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            if (!IsEnable) return;
            if (PreviouslyUsedVents.Contains(vent.Id)) return;

            PreviouslyUsedVents.Add(vent.Id);
            XP += RewardForVenting.GetInt();
            SendRPC();
        }

        public override bool OnSabotage(PlayerControl pc)
        {
            if (Main.PlayerStates[pc.PlayerId].Role is not Mafioso { IsEnable: true } mo) return true;
            mo.XP += RewardForSabotaging.GetInt();
            mo.SendRPC();
            return true;
        }

        public static void OnCrewmateEjected()
        {
            foreach (var state in Main.PlayerStates)
            {
                if (state.Value.Role is Mafioso { IsEnable: true } mo)
                {
                    mo.XP += RewardForOtherPlayerEjected.GetInt();
                    mo.SendRPC();
                }
            }
        }
    }
}