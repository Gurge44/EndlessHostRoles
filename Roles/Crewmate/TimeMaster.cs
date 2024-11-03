using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Crewmate
{
    internal class TimeMaster : RoleBase
    {
        public static bool On;
        public static Dictionary<byte, Vector2> TimeMasterBackTrack = [];
        private static readonly Dictionary<byte, long> TimeMasterInProtect = [];
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(8950, TabGroup.CrewmateRoles, CustomRoles.TimeMaster);

            TimeMasterSkillCooldown = new FloatOptionItem(8960, "TimeMasterSkillCooldown", new(0f, 180f, 1f), 20f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
                .SetValueFormat(OptionFormat.Seconds);

            TimeMasterSkillDuration = new FloatOptionItem(8961, "TimeMasterSkillDuration", new(0f, 180f, 1f), 15f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
                .SetValueFormat(OptionFormat.Seconds);

            TimeMasterMaxUses = new IntegerOptionItem(8962, "TimeMasterMaxUses", new(0, 180, 1), 1, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
                .SetValueFormat(OptionFormat.Times);

            TimeMasterAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(8963, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.4f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
                .SetValueFormat(OptionFormat.Times);

            TimeMasterAbilityChargesWhenFinishedTasks = new FloatOptionItem(8964, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(TimeMasterMaxUses.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (UsePets.GetBool()) return;

            AURoleOptions.EngineerCooldown = TimeMasterSkillCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var ProgressText = new StringBuilder();

            ProgressText.Append(Utils.GetAbilityUseLimitDisplay(playerId, TimeMasterInProtect.ContainsKey(playerId)));
            ProgressText.Append(Utils.GetTaskCount(playerId, comms));

            return ProgressText.ToString();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (UsePets.GetBool())
                hud.PetButton.buttonLabelText.text = Translator.GetString("TimeMasterVentButtonText");
            else
                hud.AbilityButton.buttonLabelText.text = Translator.GetString("TimeMasterVentButtonText");
        }

        public override void OnPet(PlayerControl pc)
        {
            Rewind(pc);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            pc.MyPhysics?.RpcBootFromVent(vent.Id);
            Rewind(pc);
        }

        private static void Rewind(PlayerControl pc)
        {
            if (TimeMasterInProtect.ContainsKey(pc.PlayerId)) return;

            if (pc.GetAbilityUseLimit() >= 1)
            {
                pc.RpcRemoveAbilityUse();
                TimeMasterInProtect.Remove(pc.PlayerId);
                TimeMasterInProtect.Add(pc.PlayerId, Utils.TimeStamp);
                pc.Notify(Translator.GetString("TimeMasterOnGuard"), TimeMasterSkillDuration.GetFloat());

                foreach (PlayerControl player in Main.AllPlayerControls)
                {
                    if (TimeMasterBackTrack.TryGetValue(player.PlayerId, out Vector2 position))
                    {
                        if (!pc.inVent && !pc.inMovingPlat && pc.IsAlive() && !pc.onLadder && pc.MyPhysics != null && !pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation() && !pc.MyPhysics.Animations.IsPlayingEnterVentAnimation() && player.PlayerId != pc.PlayerId)
                            player.TP(position);
                        else if (pc.PlayerId == player.PlayerId) player.MyPhysics?.RpcBootFromVent(Main.LastEnteredVent.TryGetValue(player.PlayerId, out Vent vent) ? vent.Id : player.PlayerId);

                        TimeMasterBackTrack.Remove(player.PlayerId);
                    }
                    else TimeMasterBackTrack.Add(player.PlayerId, player.Pos());
                }

                if (pc.PlayerId == PlayerControl.LocalPlayer.PlayerId)
                    Achievements.Type.APerfectTimeToRewindIt.Complete();
            }
            else pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            if (killer.PlayerId != target.PlayerId && TimeMasterInProtect.TryGetValue(target.PlayerId, out long ts) && ts + TimeMasterSkillDuration.GetInt() >= Utils.TimeStamp)
            {
                foreach (PlayerControl player in Main.AllPlayerControls)
                    if (!killer.Is(CustomRoles.Pestilence) && TimeMasterBackTrack.TryGetValue(player.PlayerId, out Vector2 pos))
                        player.TP(pos);

                killer.SetKillCooldown(target: target);
                return false;
            }

            return true;
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            byte playerId = player.PlayerId;

            if (TimeMasterInProtect.TryGetValue(playerId, out long ttime) && ttime + TimeMasterSkillDuration.GetInt() < Utils.TimeStamp)
            {
                TimeMasterInProtect.Remove(playerId);
                player.RpcResetAbilityCooldown();
                player.Notify(Translator.GetString("TimeMasterSkillStop"), (int)player.GetAbilityUseLimit());
            }
        }
    }
}