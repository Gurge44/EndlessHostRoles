using AmongUs.GameOptions;
using System.Text;
using TOHE.Modules;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    internal class Veteran : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(8908, TabGroup.CrewmateRoles, CustomRoles.Veteran);
            VeteranSkillCooldown = FloatOptionItem.Create(8910, "VeteranSkillCooldown", new(0f, 180f, 1f), 20f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
                .SetValueFormat(OptionFormat.Seconds);
            VeteranSkillDuration = FloatOptionItem.Create(8911, "VeteranSkillDuration", new(0f, 180f, 1f), 10f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
                .SetValueFormat(OptionFormat.Seconds);
            VeteranSkillMaxOfUseage = IntegerOptionItem.Create(8912, "VeteranSkillMaxOfUseage", new(0, 180, 1), 1, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
                .SetValueFormat(OptionFormat.Times);
            VeteranAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(8913, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.3f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
                .SetValueFormat(OptionFormat.Times);
            VeteranAbilityChargesWhenFinishedTasks = FloatOptionItem.Create(8914, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(VeteranSkillMaxOfUseage.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = VeteranSkillCooldown.GetFloat();
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
            if (UsePets.GetBool())
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
                pc.Notify(Translator.GetString("VeteranOnGuard"), VeteranSkillDuration.GetFloat());
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
                && Main.VeteranInProtect[target.PlayerId] + VeteranSkillDuration.GetInt() >= Utils.TimeStamp)
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
            if (Main.VeteranInProtect.TryGetValue(playerId, out var vtime) && vtime + VeteranSkillDuration.GetInt() < Utils.TimeStamp)
            {
                Main.VeteranInProtect.Remove(playerId);
                player.RpcResetAbilityCooldown();
                player.Notify(string.Format(Translator.GetString("VeteranOffGuard"), (int)player.GetAbilityUseLimit()));
            }
        }
    }
}