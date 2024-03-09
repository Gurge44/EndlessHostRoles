using AmongUs.GameOptions;
using System.Text;
using TOHE.Modules;

namespace TOHE.Roles.Crewmate
{
    internal class Veteran : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(Options.VeteranSkillMaxOfUseage.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = Options.VeteranSkillCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1;
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var ProgressText = new StringBuilder();

            ProgressText.Append(Utils.GetAbilityUseLimitDisplay(playerId, Main.VeteranInProtect.ContainsKey(playerId)));
            ProgressText.Append(Utils.GetTaskCount(playerId, comms));

            return ProgressText.ToString();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (Options.UsePets.GetBool())
                hud.PetButton.buttonLabelText.text = Translator.GetString("VeteranVentButtonText");
            else
                hud.AbilityButton.buttonLabelText.text = Translator.GetString("VeteranVentButtonText");
        }

        public override void OnPet(PlayerControl pc)
        {
            Alert(pc);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            Alert(pc);
        }

        static void Alert(PlayerControl pc)
        {
            if (Main.VeteranInProtect.ContainsKey(pc.PlayerId)) return;
            if (pc.GetAbilityUseLimit() >= 1)
            {
                Main.VeteranInProtect[pc.PlayerId] = Utils.TimeStamp;
                pc.RpcRemoveAbilityUse();
                pc.RPCPlayCustomSound("Gunload");
                pc.Notify(Translator.GetString("VeteranOnGuard"), Options.VeteranSkillDuration.GetFloat());
                pc.MarkDirtySettings();
            }
            else
            {
                if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId)) pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            if (Main.VeteranInProtect.ContainsKey(target.PlayerId)
                && killer.PlayerId != target.PlayerId
                && Main.VeteranInProtect[target.PlayerId] + Options.VeteranSkillDuration.GetInt() >= Utils.TimeStamp)
            {
                if (!killer.Is(CustomRoles.Pestilence))
                {
                    killer.SetRealKiller(target);
                    target.Kill(killer);
                    Logger.Info($"{target.GetRealName()} reverse killed：{killer.GetRealName()}", "Veteran Kill");
                    return false;
                }

                target.SetRealKiller(killer);
                killer.Kill(target);
                Logger.Info($"{target.GetRealName()} reverse reverse killed：{target.GetRealName()}", "Pestilence Reflect");
                return false;
            }

            return true;
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            var playerId = player.PlayerId;
            if (Main.VeteranInProtect.TryGetValue(playerId, out var vtime) && vtime + Options.VeteranSkillDuration.GetInt() < Utils.TimeStamp)
            {
                Main.VeteranInProtect.Remove(playerId);
                player.RpcResetAbilityCooldown();
                player.Notify(string.Format(Translator.GetString("VeteranOffGuard"), (int)player.GetAbilityUseLimit()));
            }
        }
    }
}