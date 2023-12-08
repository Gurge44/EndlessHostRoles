using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    public static class Mastermind
    {
        private static readonly int Id = 640600;
        public static byte mastermindId = byte.MaxValue;

        public static Dictionary<byte, long> ManipulatedPlayers = [];
        public static Dictionary<byte, long> ManipulateDelays = [];
        public static Dictionary<byte, float> TempKCDs = [];

        public static OptionItem KillCooldown;
        public static OptionItem TimeLimit;
        public static OptionItem Delay;

        public static float ManipulateCD;

        private static PlayerControl Mastermind_ => GetPlayerById(mastermindId);

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Mastermind);
            KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Mastermind])
                .SetValueFormat(OptionFormat.Seconds);
            // Manipulation Cooldown = Kill Cooldown + Delay + Time Limit
            TimeLimit = IntegerOptionItem.Create(Id + 12, "MastermindTimeLimit", new(1, 60, 1), 20, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Mastermind])
                .SetValueFormat(OptionFormat.Seconds);
            Delay = IntegerOptionItem.Create(Id + 13, "MastermindDelay", new(0, 30, 1), 7, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Mastermind])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public static void Init()
        {
            mastermindId = byte.MaxValue;
            ManipulatedPlayers = [];
            ManipulateDelays = [];
            TempKCDs = [];
        }

        public static void Add(byte playerId)
        {
            mastermindId = playerId;
            ManipulateCD = KillCooldown.GetFloat() + TimeLimit.GetFloat() + Delay.GetFloat();
        }

        public static bool IsEnable => mastermindId != byte.MaxValue;

        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable) return false;
            if (killer == null) return false;
            if (target == null) return false;

            return killer.CheckDoubleTrigger(target, () =>
            {
                killer.SetKillCooldown(time: ManipulateCD);
                if (target.HasKillButton() || target.GetPlayerTaskState().hasTasks)
                {
                    ManipulateDelays.TryAdd(target.PlayerId, GetTimeStamp());
                    NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                }
            });
        }

        public static void OnFixedUpdate()
        {
            if (!IsEnable) return;
            if (GameStates.IsMeeting) return;
            if (!ManipulatedPlayers.Any() && !ManipulateDelays.Any()) return;

            foreach (var x in ManipulateDelays)
            {
                var pc = GetPlayerById(x.Key);

                if (!pc.IsAlive())
                {
                    ManipulateDelays.Remove(x.Key);
                    continue;
                }
                if (x.Value + Delay.GetInt() < GetTimeStamp())
                {
                    ManipulateDelays.Remove(x.Key);
                    ManipulatedPlayers.TryAdd(x.Key, GetTimeStamp());

                    if (!pc.GetPlayerTaskState().hasTasks)
                    {
                        TempKCDs.TryAdd(pc.PlayerId, pc.killTimer);
                        pc.SetKillCooldown(time: 1f);
                    }

                    NotifyRoles(SpecifySeer: Mastermind_, SpecifyTarget: Mastermind_);
                }
            }

            foreach (var x in ManipulatedPlayers)
            {
                var player = GetPlayerById(x.Key);

                if (!player.IsAlive())
                {
                    ManipulatedPlayers.Remove(x.Key);
                    TempKCDs.Remove(x.Key);
                    continue;
                }
                if (x.Value + TimeLimit.GetInt() < GetTimeStamp())
                {
                    ManipulatedPlayers.Remove(x.Key);
                    TempKCDs.Remove(x.Key);
                    player.Suicide(realKiller: Mastermind_);
                    RPC.PlaySoundRPC(mastermindId, Sounds.KillSound);
                }

                var time = TimeLimit.GetInt() - (GetTimeStamp() - x.Value);

                player.Notify(string.Format(GetString(player.GetPlayerTaskState().hasTasks ? "ManipulateTaskNotify" : "ManipulateNotify"), time), 1.1f);
            }
        }

        public static void OnReportDeadBody()
        {
            if (!IsEnable) return;
            foreach (var x in ManipulatedPlayers)
            {
                var pc = GetPlayerById(x.Key);
                if (pc.IsAlive()) pc.Suicide(realKiller: Mastermind_);
            }
            ManipulateDelays.Clear();
            ManipulatedPlayers.Clear();
            TempKCDs.Clear();
        }

        public static bool ForceKillForManipulatedPlayer(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable) return false;
            if (killer == null) return false;
            if (target == null) return false;

            ManipulatedPlayers.Remove(killer.PlayerId);

            NotifyMastermindTargetSurvived();

            if (target.Is(CustomRoles.Pestilence) || Main.VeteranInProtect.ContainsKey(target.PlayerId) || target.Is(CustomRoles.Mastermind))
            {
                Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
                Main.PlayerStates[killer.PlayerId].SetDead();
                target.Kill(killer);
                TempKCDs.Remove(killer.PlayerId);
                return false;
            }

            killer.Kill(target);

            killer.Notify(GetString("MastermindTargetSurvived"));

            _ = new LateTask(() =>
            {
                killer.SetKillCooldown(time: TempKCDs.TryGetValue(killer.PlayerId, out var cd) ? cd : 0f + Main.AllPlayerKillCooldown[killer.PlayerId]);
                TempKCDs.Remove(killer.PlayerId);
            }, 0.1f, "Set KCD for Manipulated Kill");

            return true;
        }

        public static void OnManipulatedPlayerTaskComplete(PlayerControl pc)
        {
            if (!IsEnable) return;
            ManipulatedPlayers.Remove(pc.PlayerId);

            NotifyMastermindTargetSurvived();

            pc.Notify(GetString("MastermindTargetSurvived"));
            TempKCDs.Remove(pc.PlayerId);
        }

        private static void NotifyMastermindTargetSurvived()
        {
            if (!IsEnable) return;
            Mastermind_.Notify(GetString("ManipulatedKilled"));
            if (Mastermind_.killTimer > KillCooldown.GetFloat()) Mastermind_.SetKillCooldown(time: KillCooldown.GetFloat());
        }
    }
}
