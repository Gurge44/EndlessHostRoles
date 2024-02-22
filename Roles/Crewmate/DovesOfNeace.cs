using AmongUs.GameOptions;
using HarmonyLib;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.Impostor;

namespace TOHE.Roles.Crewmate
{
    internal class DovesOfNeace : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(Options.DovesOfNeaceMaxOfUseage.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = Options.DovesOfNeaceCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (Options.UsePets.GetBool())
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
                .Where(x => isMadMate ? (x.CanUseKillButton() && x.GetCustomRole().IsCrewmate()) : x.CanUseKillButton())
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
