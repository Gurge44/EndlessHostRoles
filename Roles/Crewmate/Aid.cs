using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.Roles.Crewmate
{
    public class Aid : RoleBase
    {
        private const int Id = 640200;
        public static Dictionary<byte, long> ShieldedPlayers = [];

        public byte TargetId;

        public static OptionItem AidDur;
        public static OptionItem AidCD;
        public static OptionItem TargetKnowsShield;
        public static OptionItem UseLimitOpt;
        public static OptionItem UsePet;
        public override bool IsEnable => playerIdList.Count > 0;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Aid);
            AidCD = new FloatOptionItem(Id + 10, "AidCD", new(0f, 60f, 1f), 15f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
                .SetValueFormat(OptionFormat.Seconds);
            AidDur = new FloatOptionItem(Id + 11, "AidDur", new(0f, 60f, 1f), 10f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
                .SetValueFormat(OptionFormat.Seconds);
            TargetKnowsShield = new BooleanOptionItem(Id + 14, "AidTargetKnowsAboutShield", true, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
            UseLimitOpt = new IntegerOptionItem(Id + 12, "AbilityUseLimit", new(1, 20, 1), 5, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Aid])
                .SetValueFormat(OptionFormat.Times);
            UsePet = Options.CreatePetUseSetting(Id + 13, CustomRoles.Aid);
        }

        public override void Init()
        {
            ShieldedPlayers = [];
        }

        public override void Add(byte playerId)
        {
            playerId.SetAbilityUseLimit(UseLimitOpt.GetInt());
            TargetId = byte.MaxValue;

            if (!AmongUsClient.Instance.AmHost || (Options.UsePets.GetBool() && UsePet.GetBool())) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override void SetKillCooldown(byte playerId) => Main.AllPlayerKillCooldown[playerId] = AidCD.GetInt();
        public override void ApplyGameOptions(IGameOptions opt, byte playerId) => opt.SetVision(false);
        public override bool CanUseKillButton(PlayerControl pc) => pc.GetAbilityUseLimit() >= 1;
        public override bool CanUseImpostorVentButton(PlayerControl pc) => pc.GetAbilityUseLimit() >= 1 && TargetId != byte.MaxValue;

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;

            TargetId = target.PlayerId;
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            return false;
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (pc == null || !pc.Is(CustomRoles.Aid) || ShieldedPlayers.Count == 0) return;

            bool change = false;

            foreach (var x in ShieldedPlayers.ToArray())
            {
                if (x.Value + AidDur.GetInt() <= Utils.TimeStamp || !GameStates.IsInTask)
                {
                    ShieldedPlayers.Remove(x.Key);
                    change = true;
                }
            }

            if (change && GameStates.IsInTask)
            {
                Utils.NotifyRoles(SpecifySeer: pc);
            }
        }

        public override void OnCoEnterVent(PlayerPhysics physics, Vent vent)
        {
            var pc = physics.myPlayer;
            if (pc.GetAbilityUseLimit() >= 1 && TargetId != byte.MaxValue)
            {
                pc.RpcRemoveAbilityUse();
                ShieldedPlayers[TargetId] = Utils.TimeStamp;
                var target = Utils.GetPlayerById(TargetId);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: target);
                TargetId = byte.MaxValue;
            }

            LateTask.New(() => physics.RpcBootFromVent(vent), 0.5f, log: false);
        }

        public override GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
        {
            if (seer.PlayerId != target.PlayerId || (seer.IsModClient() && !hud)) return string.Empty;
            if (TargetKnowsShield.GetBool() && ShieldedPlayers.TryGetValue(seer.PlayerId, out var ts))
            {
                var timeLeft = AidDur.GetInt() - (Utils.TimeStamp - ts);
                return string.Format(Translator.GetString("AidCounterSelf"), timeLeft);
            }

            if (seer.Is(CustomRoles.Aid))
            {
                var duration = AidDur.GetInt();
                var now = Utils.TimeStamp;
                var formatted = ShieldedPlayers.Select(x => string.Format(Translator.GetString("AidCounterTarget"), Utils.ColorString(Main.PlayerColors.GetValueOrDefault(x.Key, Color.white), Main.AllPlayerNames.GetValueOrDefault(x.Key, "Someone")), duration - (now - x.Value)));
                return string.Join("\n", formatted);
            }

            return string.Empty;
        }
    }
}