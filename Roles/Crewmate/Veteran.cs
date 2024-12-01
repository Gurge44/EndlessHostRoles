using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Crewmate
{
    internal class Veteran : RoleBase
    {
        public static Dictionary<byte, long> VeteranInProtect = [];

        public static bool On;
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            const int id = 8990;
            SetupRoleOptions(id, TabGroup.CrewmateRoles, CustomRoles.Veteran);

            VeteranSkillCooldown = new FloatOptionItem(id + 2, "VeteranSkillCooldown", new(0f, 180f, 1f), 20f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
                .SetValueFormat(OptionFormat.Seconds);

            VeteranSkillDuration = new FloatOptionItem(id + 3, "VeteranSkillDuration", new(0f, 180f, 1f), 10f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
                .SetValueFormat(OptionFormat.Seconds);

            VeteranSkillMaxOfUseage = new IntegerOptionItem(id + 4, "VeteranSkillMaxOfUseage", new(0, 180, 1), 1, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
                .SetValueFormat(OptionFormat.Times);

            VeteranAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(id + 5, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.3f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Veteran])
                .SetValueFormat(OptionFormat.Times);

            VeteranAbilityChargesWhenFinishedTasks = new FloatOptionItem(id + 6, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
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

            ProgressText.Append(Utils.GetAbilityUseLimitDisplay(playerId, VeteranInProtect.ContainsKey(playerId)));
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

        private static void Alert(PlayerControl pc)
        {
            if (VeteranInProtect.ContainsKey(pc.PlayerId)) return;

            if (pc.GetAbilityUseLimit() >= 1)
            {
                VeteranInProtect[pc.PlayerId] = Utils.TimeStamp;
                pc.RpcRemoveAbilityUse();
                pc.RPCPlayCustomSound("Gunload");
                pc.Notify(Translator.GetString("VeteranOnGuard"), VeteranSkillDuration.GetFloat());
                pc.MarkDirtySettings();
            }
            else
                pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            if (VeteranInProtect.ContainsKey(target.PlayerId)
                && killer.PlayerId != target.PlayerId
                && VeteranInProtect[target.PlayerId] + VeteranSkillDuration.GetInt() >= Utils.TimeStamp)
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

                if (killer.IsLocalPlayer())
                    Achievements.Type.YoureTooLate.Complete();

                return false;
            }

            return true;
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            byte playerId = player.PlayerId;

            if (VeteranInProtect.TryGetValue(playerId, out long vtime) && vtime + VeteranSkillDuration.GetInt() < Utils.TimeStamp)
            {
                VeteranInProtect.Remove(playerId);
                player.RpcResetAbilityCooldown();
                player.Notify(string.Format(Translator.GetString("VeteranOffGuard"), (int)player.GetAbilityUseLimit()));
            }
        }

        public override bool CanUseVent(PlayerControl pc, int ventId)
        {
            return !IsThisRole(pc) || pc.GetClosestVent()?.Id == ventId;
        }
    }
}