using System.Collections.Generic;
using EHR.Crewmate;
using EHR.Modules;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Impostor
{
    public class Mastermind : RoleBase
    {
        private const int Id = 640600;

        private static List<byte> PlayerIdList = [];

        public static Dictionary<byte, long> ManipulatedPlayers = [];
        public static Dictionary<byte, long> ManipulateDelays = [];
        private static Dictionary<byte, float> TempKCDs = [];

        private static OptionItem KillCooldown;
        private static OptionItem TimeLimit;
        private static OptionItem Delay;

        private static float ManipulateCD;
        private byte MastermindId = byte.MaxValue;

        private PlayerControl Mastermind_ => GetPlayerById(MastermindId);

        public override bool IsEnable => MastermindId != byte.MaxValue || Randomizer.Exists;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Mastermind);
            KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Mastermind])
                .SetValueFormat(OptionFormat.Seconds);
            // Manipulation Cooldown = Kill Cooldown + Delay + Time Limit
            TimeLimit = new IntegerOptionItem(Id + 12, "MastermindTimeLimit", new(1, 60, 1), 20, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Mastermind])
                .SetValueFormat(OptionFormat.Seconds);
            Delay = new IntegerOptionItem(Id + 13, "MastermindDelay", new(0, 30, 1), 7, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Mastermind])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            MastermindId = byte.MaxValue;
            ManipulatedPlayers = [];
            ManipulateDelays = [];
            TempKCDs = [];
            PlayerIdList = [];
        }

        public override void Add(byte playerId)
        {
            MastermindId = playerId;
            ManipulateCD = KillCooldown.GetFloat() + TimeLimit.GetFloat() + Delay.GetFloat();
            PlayerIdList.Add(playerId);
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable) return false;
            if (killer == null) return false;
            if (target == null) return false;

            return killer.CheckDoubleTrigger(target, () =>
            {
                killer.SetKillCooldown(time: ManipulateCD);
                ManipulateDelays.TryAdd(target.PlayerId, TimeStamp);
                NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            });
        }

        public override void OnFixedUpdate(PlayerControl _)
        {
            if (!IsEnable || GameStates.IsMeeting || (ManipulatedPlayers.Count == 0 && ManipulateDelays.Count == 0)) return;

            foreach (var x in ManipulateDelays)
            {
                var pc = GetPlayerById(x.Key);

                if (!pc.IsAlive())
                {
                    ManipulateDelays.Remove(x.Key);
                    continue;
                }

                if (x.Value + Delay.GetInt() < TimeStamp)
                {
                    ManipulateDelays.Remove(x.Key);
                    ManipulatedPlayers.TryAdd(x.Key, TimeStamp);

                    if (pc.HasKillButton())
                    {
                        TempKCDs.TryAdd(pc.PlayerId, Main.KillTimers[pc.PlayerId]);
                        pc.SetKillCooldown(time: 1f);
                    }
                    else pc.RpcChangeRoleBasis(CustomRoles.NSerialKiller);

                    NotifyRoles(SpecifySeer: Mastermind_, SpecifyTarget: pc);
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

                if (x.Value + TimeLimit.GetInt() < TimeStamp)
                {
                    ManipulatedPlayers.Remove(x.Key);
                    TempKCDs.Remove(x.Key);
                    player.Suicide(realKiller: Mastermind_);
                    RPC.PlaySoundRPC(MastermindId, Sounds.KillSound);
                    player.RpcChangeRoleBasis(player.GetCustomRole());
                }

                var time = TimeLimit.GetInt() - (TimeStamp - x.Value);

                player.Notify(string.Format(GetString("ManipulateNotify"), time), 1.1f);
            }
        }

        public override void OnReportDeadBody()
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
            if (killer == null) return false;
            if (target == null) return false;

            ManipulatedPlayers.Remove(killer.PlayerId);

            foreach (var id in PlayerIdList)
            {
                (Main.PlayerStates[id].Role as Mastermind)?.NotifyMastermindTargetSurvived();
            }

            if (target.Is(CustomRoles.Pestilence) || Veteran.VeteranInProtect.ContainsKey(target.PlayerId) || target.Is(CustomRoles.Mastermind))
            {
                Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
                Main.PlayerStates[killer.PlayerId].SetDead();
                target.Kill(killer);
                TempKCDs.Remove(killer.PlayerId);
                return false;
            }

            killer.Kill(target);

            killer.Notify(GetString("MastermindTargetSurvived"));
            killer.RpcChangeRoleBasis(killer.GetCustomRole());

            LateTask.New(() =>
            {
                var kcd = TempKCDs.TryGetValue(killer.PlayerId, out var cd) ? cd : Main.AllPlayerKillCooldown.GetValueOrDefault(killer.PlayerId, DefaultKillCooldown);
                killer.SetKillCooldown(time: kcd);
                TempKCDs.Remove(killer.PlayerId);
            }, 0.1f, "Set KCD for Manipulated Kill");

            return false;
        }

        private void NotifyMastermindTargetSurvived()
        {
            if (!IsEnable) return;
            Mastermind_.Notify(GetString("ManipulatedKilled"));
            if (Main.KillTimers[MastermindId] > KillCooldown.GetFloat())
                Mastermind_.SetKillCooldown(time: KillCooldown.GetFloat());
        }
    }
}