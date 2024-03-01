using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using HarmonyLib;
using TOHE.Modules;

namespace TOHE.Roles.Crewmate
{
    internal class Grenadier : RoleBase
    {
        public static Dictionary<byte, long> GrenadierBlinding = [];
        public static Dictionary<byte, long> MadGrenadierBlinding = [];

        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(Options.GrenadierSkillMaxOfUseage.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = Options.GrenadierSkillCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1;
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var ProgressText = new StringBuilder();

            ProgressText.Append(Utils.GetTaskCount(playerId, comms));
            ProgressText.Append(Utils.GetAbilityUseLimitDisplay(playerId, GrenadierBlinding.ContainsKey(playerId)));

            return ProgressText.ToString();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (Options.UsePets.GetBool())
                hud.PetButton.buttonLabelText.text = Translator.GetString("GrenadierVentButtonText");
            else
                hud.AbilityButton.buttonLabelText.text = Translator.GetString("GrenadierVentButtonText");
        }

        public override void OnPet(PlayerControl pc)
        {
            BlindPlayers(pc);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            BlindPlayers(pc);
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (!GameStates.IsInTask) return;

            var playerId = player.PlayerId;
            var now = Utils.TimeStamp;

            if (GrenadierBlinding.TryGetValue(playerId, out var gtime) && gtime + Options.GrenadierSkillDuration.GetInt() < now)
            {
                GrenadierBlinding.Remove(playerId);
                player.RpcResetAbilityCooldown();
                player.Notify(string.Format(Translator.GetString("GrenadierSkillStop"), (int)player.GetAbilityUseLimit()));
                Utils.MarkEveryoneDirtySettingsV3();
            }

            if (MadGrenadierBlinding.TryGetValue(playerId, out var mgtime) && mgtime + Options.GrenadierSkillDuration.GetInt() < now)
            {
                MadGrenadierBlinding.Remove(playerId);
                player.RpcResetAbilityCooldown();
                player.Notify(string.Format(Translator.GetString("GrenadierSkillStop"), (int)player.GetAbilityUseLimit()));
                Utils.MarkEveryoneDirtySettingsV3();
            }
        }

        static void BlindPlayers(PlayerControl pc)
        {
            if (GrenadierBlinding.ContainsKey(pc.PlayerId) || MadGrenadierBlinding.ContainsKey(pc.PlayerId)) return;
            if (pc.GetAbilityUseLimit() >= 1)
            {
                if (pc.Is(CustomRoles.Madmate))
                {
                    MadGrenadierBlinding.Remove(pc.PlayerId);
                    MadGrenadierBlinding.Add(pc.PlayerId, Utils.TimeStamp);
                    Main.AllPlayerControls.Where(x => x.IsModClient()).Where(x => !x.GetCustomRole().IsImpostorTeam() && !x.Is(CustomRoles.Madmate)).Do(x => x.RPCPlayCustomSound("FlashBang"));
                }
                else
                {
                    GrenadierBlinding.Remove(pc.PlayerId);
                    GrenadierBlinding.Add(pc.PlayerId, Utils.TimeStamp);
                    Main.AllPlayerControls.Where(x => x.IsModClient()).Where(x => x.GetCustomRole().IsImpostor() || (x.GetCustomRole().IsNeutral() && Options.GrenadierCanAffectNeutral.GetBool())).Do(x => x.RPCPlayCustomSound("FlashBang"));
                }

                pc.RPCPlayCustomSound("FlashBang");
                pc.Notify(Translator.GetString("GrenadierSkillInUse"), Options.GrenadierSkillDuration.GetFloat());
                pc.RpcRemoveAbilityUse();
                Utils.MarkEveryoneDirtySettingsV3();
            }
            else
            {
                if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }
    }
}