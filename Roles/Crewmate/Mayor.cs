using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmongUs.GameOptions;

namespace TOHE.Roles.Crewmate
{
    internal class Mayor : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            Main.MayorUsedButtonCount[playerId] = 0;
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown =
                !Main.MayorUsedButtonCount.TryGetValue(playerId, out var count) || count < Options.MayorNumOfUseButton.GetInt()
                    ? opt.GetInt(Int32OptionNames.EmergencyCooldown)
                    : 300f;
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (Options.UsePets.GetBool())
                hud.PetButton.buttonLabelText.text = Translator.GetString("MayorVentButtonText");
            else
                hud.AbilityButton.buttonLabelText.text = Translator.GetString("MayorVentButtonText");
        }

        public override void OnPet(PlayerControl pc)
        {
            Button(pc);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            pc.MyPhysics?.RpcBootFromVent(vent.Id);
            Button(pc);
        }

        private static void Button(PlayerControl pc)
        {
            if (Main.MayorUsedButtonCount.TryGetValue(pc.PlayerId, out var count) && count < Options.MayorNumOfUseButton.GetInt())
            {
                pc.ReportDeadBody(null);
            }
        }
    }
}
