using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Roles.Impostor;
using HarmonyLib;
using static EHR.Options;

namespace EHR.Roles.Crewmate
{
    internal class DovesOfNeace : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(7700, TabGroup.CrewmateRoles, CustomRoles.DovesOfNeace);
            DovesOfNeaceCooldown = FloatOptionItem.Create(7710, "DovesOfNeaceCooldown", new(0f, 180f, 1f), 7f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DovesOfNeace])
                .SetValueFormat(OptionFormat.Seconds);
            DovesOfNeaceMaxOfUseage = IntegerOptionItem.Create(7711, "DovesOfNeaceMaxOfUseage", new(0, 180, 1), 0, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DovesOfNeace])
                .SetValueFormat(OptionFormat.Times);
            DovesOfNeaceAbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(7712, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DovesOfNeace])
                .SetValueFormat(OptionFormat.Times);
            DovesOfNeaceAbilityChargesWhenFinishedTasks = FloatOptionItem.Create(7713, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.DovesOfNeace])
                .SetValueFormat(OptionFormat.Times);
        }


        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(DovesOfNeaceMaxOfUseage.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = DovesOfNeaceCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (UsePets.GetBool())
                hud.PetButton.buttonLabelText.text = Translator.GetString("DovesOfNeaceVentButtonText");
            else
                hud.AbilityButton.buttonLabelText.text = Translator.GetString("DovesOfNeaceVentButtonText");
        }

        public override void OnPet(PlayerControl pc)
        {
            ResetCooldowns(pc);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            ResetCooldowns(pc);
        }

        static void ResetCooldowns(PlayerControl pc)
        {
            if (pc.GetAbilityUseLimit() < 1)
            {
                if (!NameNotifyManager.Notice.ContainsKey(pc.PlayerId))
                    pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
                return;
            }

            pc.RpcRemoveAbilityUse();
            bool isMadMate = pc.Is(CustomRoles.Madmate);
            Main.AllAlivePlayerControls
                .Where(x => isMadMate ? (x.CanUseKillButton() && x.IsCrewmate()) : x.CanUseKillButton())
                .Do(x =>
                {
                    x.RPCPlayCustomSound("Dove");
                    x.ResetKillCooldown();
                    x.SetKillCooldown();
                    if (Main.PlayerStates[x.PlayerId].Role is SerialKiller sk)
                    {
                        sk.OnReportDeadBody();
                    }

                    x.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.DovesOfNeace), Translator.GetString("DovesOfNeaceSkillNotify")));
                });
            pc.RPCPlayCustomSound("Dove");
            pc.Notify(string.Format(Translator.GetString("DovesOfNeaceOnGuard"), pc.GetAbilityUseLimit()));
        }
    }
}