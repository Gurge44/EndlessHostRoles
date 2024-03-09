using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using HarmonyLib;
using TOHE.Modules;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate
{
    internal class Grenadier : RoleBase
    {
        public static Dictionary<byte, long> GrenadierBlinding = [];
        public static Dictionary<byte, long> MadGrenadierBlinding = [];

        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(6800, TabGroup.CrewmateRoles, CustomRoles.Grenadier);
            GrenadierSkillCooldown = FloatOptionItem.Create(6810, "GrenadierSkillCooldown", new(0f, 180f, 1f), 25f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
                .SetValueFormat(OptionFormat.Seconds);
            GrenadierSkillDuration = FloatOptionItem.Create(6811, "GrenadierSkillDuration", new(0f, 180f, 1f), 10f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
                .SetValueFormat(OptionFormat.Seconds);
            GrenadierCauseVision = FloatOptionItem.Create(6812, "GrenadierCauseVision", new(0f, 5f, 0.05f), 0.3f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
                .SetValueFormat(OptionFormat.Multiplier);
            GrenadierCanAffectNeutral = BooleanOptionItem.Create(6813, "GrenadierCanAffectNeutral", false, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier]);
            GrenadierSkillMaxOfUseage = IntegerOptionItem.Create(6814, "GrenadierSkillMaxOfUseage", new(0, 180, 1), 2, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
                .SetValueFormat(OptionFormat.Times);
            GrenadierAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(6815, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.1f), 0.5f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
                .SetValueFormat(OptionFormat.Times);
            GrenadierAbilityChargesWhenFinishedTasks = FloatOptionItem.Create(6816, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Grenadier])
                .SetValueFormat(OptionFormat.Times);
        }

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(GrenadierSkillMaxOfUseage.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = GrenadierSkillCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1;
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var ProgressText = new StringBuilder();

            ProgressText.Append(Utils.GetAbilityUseLimitDisplay(playerId, GrenadierBlinding.ContainsKey(playerId)));
            ProgressText.Append(Utils.GetTaskCount(playerId, comms));

            return ProgressText.ToString();
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (UsePets.GetBool())
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

            if (GrenadierBlinding.TryGetValue(playerId, out var gtime) && gtime + GrenadierSkillDuration.GetInt() < now)
            {
                GrenadierBlinding.Remove(playerId);
                player.RpcResetAbilityCooldown();
                player.Notify(string.Format(Translator.GetString("GrenadierSkillStop"), (int)player.GetAbilityUseLimit()));
                Utils.MarkEveryoneDirtySettingsV3();
            }

            if (MadGrenadierBlinding.TryGetValue(playerId, out var mgtime) && mgtime + GrenadierSkillDuration.GetInt() < now)
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
                    Main.AllPlayerControls.Where(x => x.IsModClient()).Where(x => x.GetCustomRole().IsImpostor() || (x.GetCustomRole().IsNeutral() && GrenadierCanAffectNeutral.GetBool())).Do(x => x.RPCPlayCustomSound("FlashBang"));
                }

                pc.RPCPlayCustomSound("FlashBang");
                pc.Notify(Translator.GetString("GrenadierSkillInUse"), GrenadierSkillDuration.GetFloat());
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