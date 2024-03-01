using System.Text;
using AmongUs.GameOptions;

namespace TOHE.Roles.Crewmate
{
    internal class TimeMaster : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            Main.TimeMasterNum[playerId] = 0;
            playerId.SetAbilityUseLimit(Options.TimeMasterMaxUses.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = Options.TimeMasterSkillCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var ProgressText = new StringBuilder();

            ProgressText.Append(Utils.GetTaskCount(playerId, comms));
            ProgressText.Append(Utils.GetAbilityUseLimitDisplay(playerId, Main.TimeMasterInProtect.ContainsKey(playerId)));

            return ProgressText.ToString();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (Options.UsePets.GetBool())
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

        static void Rewind(PlayerControl pc)
        {
            if (Main.TimeMasterInProtect.ContainsKey(pc.PlayerId)) return;
            if (pc.GetAbilityUseLimit() >= 1)
            {
                pc.RpcRemoveAbilityUse();
                Main.TimeMasterInProtect.Remove(pc.PlayerId);
                Main.TimeMasterInProtect.Add(pc.PlayerId, Utils.TimeStamp);
                pc.Notify(Translator.GetString("TimeMasterOnGuard"), Options.TimeMasterSkillDuration.GetFloat());
                foreach (PlayerControl player in Main.AllPlayerControls)
                {
                    if (Main.TimeMasterBackTrack.TryGetValue(player.PlayerId, out var position))
                    {
                        if (!pc.inVent && !pc.inMovingPlat && pc.IsAlive() && !pc.onLadder && pc.MyPhysics != null && !pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation() && !pc.MyPhysics.Animations.IsPlayingEnterVentAnimation() && player.PlayerId != pc.PlayerId)
                        {
                            player.TP(position);
                        }
                        else if (pc.PlayerId == player.PlayerId)
                        {
                            player.MyPhysics?.RpcBootFromVent(Main.LastEnteredVent.TryGetValue(player.PlayerId, out var vent) ? vent.Id : player.PlayerId);
                        }

                        Main.TimeMasterBackTrack.Remove(player.PlayerId);
                    }
                    else
                    {
                        Main.TimeMasterBackTrack.Add(player.PlayerId, player.Pos());
                    }
                }
            }
            else
            {
                if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId))
                    pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            if (killer.PlayerId != target.PlayerId && Main.TimeMasterInProtect.TryGetValue(target.PlayerId, out var ts) && ts + Options.TimeMasterSkillDuration.GetInt() >= Utils.TimeStamp)
            {
                foreach (var player in Main.AllPlayerControls)
                {
                    if (!killer.Is(CustomRoles.Pestilence) && Main.TimeMasterBackTrack.TryGetValue(player.PlayerId, out var pos))
                    {
                        player.TP(pos);
                    }
                }

                killer.SetKillCooldown(target: target);
                return false;
            }

            return true;
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            var playerId = player.PlayerId;
            if (Main.TimeMasterInProtect.TryGetValue(playerId, out var ttime) && ttime + Options.TimeMasterSkillDuration.GetInt() < Utils.TimeStamp)
            {
                Main.TimeMasterInProtect.Remove(playerId);
                player.RpcResetAbilityCooldown();
                player.Notify(Translator.GetString("TimeMasterSkillStop"), (int)player.GetAbilityUseLimit());
            }
        }
    }
}
