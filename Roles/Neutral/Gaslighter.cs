using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Patches;

namespace EHR.Neutral
{
    public class Gaslighter : RoleBase
    {
        public static bool On;
        private static List<Gaslighter> Instances = [];

        private static OptionItem KillCooldown;
        public static OptionItem WinCondition;
        public static OptionItem CycleRepeats;

        static readonly string[] WinConditionOptions =
        [
            "GaslighterWinCondition.CrewLoses",
            "GaslighterWinCondition.IfAlive",
            "GaslighterWinCondition.LastStanding"
        ];

        private Round CurrentRound;
        private HashSet<byte> CursedPlayers;
        private bool CycleFinished;

        private byte GaslighterId;
        private HashSet<byte> ShieldedPlayers;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            StartSetup(648350)
                .AutoSetupOption(ref KillCooldown, 22.5f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
                .AutoSetupOption(ref WinCondition, 0, WinConditionOptions)
                .AutoSetupOption(ref CycleRepeats, false);
        }

        public override void Init()
        {
            On = false;
            Instances = [];
        }

        public override void Add(byte playerId)
        {
            On = true;
            Instances.Add(this);
            GaslighterId = playerId;
            CurrentRound = default;
            CursedPlayers = [];
            ShieldedPlayers = [];
            CycleFinished = false;
        }

        public override bool CanUseKillButton(PlayerControl pc) => pc.IsAlive();

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = CurrentRound switch
            {
                Round.Kill => KillCooldown.GetFloat(),
                Round.Knight => Monarch.KnightCooldown.GetFloat(),
                Round.Curse => Main.RealOptionsData.GetFloat(FloatOptionNames.KillCooldown),
                Round.Shield => Medic.CD.GetFloat(),
                _ => Options.DefaultKillCooldown
            };
        }

        public static void OnExile(byte[] exileIds)
        {
            foreach (Gaslighter instance in Instances)
            {
                foreach (byte id in exileIds)
                {
                    if (id == instance.GaslighterId)
                        instance.CursedPlayers.Clear();
                }
            }

            List<byte> curseDeathList = [];
            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                foreach (Gaslighter instance in Instances)
                {
                    if (Main.AfterMeetingDeathPlayers.ContainsKey(pc.PlayerId)) continue;

                    var gaslighter = instance.GaslighterId.GetPlayer();
                    if (instance.CursedPlayers.Contains(pc.PlayerId) && gaslighter != null && gaslighter.IsAlive())
                    {
                        pc.SetRealKiller(gaslighter);
                        curseDeathList.Add(pc.PlayerId);
                    }
                }
            }

            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Spell, [.. curseDeathList]);
        }

        public override void AfterMeetingTasks()
        {
            ShieldedPlayers.Clear();
            CursedPlayers.Clear();

            if (CurrentRound == Round.Shield)
            {
                CycleFinished = true;
                CurrentRound = Round.Kill;
            }
            else if (!CycleFinished || CycleRepeats.GetBool())
            {
                CurrentRound++;
            }

            float limit = CurrentRound switch
            {
                Round.Knight => Monarch.KnightMax.GetFloat(),
                Round.Shield => Medic.SkillLimit,
                _ => 0
            };
            GaslighterId.SetAbilityUseLimit(limit);

            var pc = GaslighterId.GetPlayer();
            pc?.ResetKillCooldown();
            pc?.Notify(Translator.GetString($"Gaslighter.{CurrentRound}"));

            LateTask.New(() => pc?.SetKillCooldown(), 1.5f, log: false);
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            switch (CurrentRound)
            {
                case Round.Kill:
                    return true;
                case Round.Knight when killer.GetAbilityUseLimit() > 0 && !target.Is(CustomRoles.Knighted):
                    target.RpcSetCustomRole(CustomRoles.Knighted);
                    killer.RpcRemoveAbilityUse();
                    killer.SetKillCooldown();
                    return false;
                case Round.Curse:
                    CursedPlayers.Add(target.PlayerId);
                    killer.SetKillCooldown();
                    return false;
                case Round.Shield when killer.GetAbilityUseLimit() > 0:
                    ShieldedPlayers.Add(target.PlayerId);
                    killer.RpcRemoveAbilityUse();
                    killer.SetKillCooldown();
                    return false;
            }

            return false;
        }

        public static bool IsShielded(PlayerControl target) => Instances.Exists(i => i.ShieldedPlayers.Contains(target.PlayerId));

        public override string GetProgressText(byte playerId, bool comms)
        {
            return CurrentRound is Round.Knight or Round.Shield
                ? base.GetProgressText(playerId, comms)
                : Utils.GetTaskCount(playerId, comms);
        }

        public static string GetMark(PlayerControl seer, PlayerControl target)
        {
            if (!IsShielded(target)) return string.Empty;
            if (!seer.Is(CustomRoles.Gaslighter) && seer.PlayerId != target.PlayerId) return string.Empty;
            return $"<color={Utils.GetRoleColorCode(CustomRoles.Medic)}> ●</color>";
        }

        public bool AddAsAdditionalWinner() => WinCondition.GetValue() switch
        {
            0 => CustomWinnerHolder.WinnerTeam != CustomWinner.Crewmate,
            1 => GaslighterId.GetPlayer()?.IsAlive() == true,
            _ => false
        };

        enum Round
        {
            Kill,
            Knight,
            Curse,
            Shield
        }
    }
}