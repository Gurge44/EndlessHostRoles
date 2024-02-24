using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AmongUs.GameOptions;

namespace TOHE.Roles.Crewmate
{
    internal class Paranoia : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            Main.ParaUsedButtonCount[playerId] = 0;
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown =
                !Main.ParaUsedButtonCount.TryGetValue(playerId, out var count2) || count2 < Options.ParanoiaNumOfUseButton.GetInt()
                    ? Options.ParanoiaVentCooldown.GetFloat()
                    : 300f;
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (Options.UsePets.GetBool())
                hud.PetButton.buttonLabelText.text = Translator.GetString("ParanoiaVentButtonText");
            else
                hud.AbilityButton.buttonLabelText.text = Translator.GetString("ParanoiaVentButtonText");
        }

        public override void OnPet(PlayerControl pc)
        {
            Panic(pc);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            pc.MyPhysics?.RpcBootFromVent(vent.Id);
            Panic(pc);
        }

        static void Panic(PlayerControl pc)
        {
            if (Main.ParaUsedButtonCount.TryGetValue(pc.PlayerId, out var count2) && count2 < Options.ParanoiaNumOfUseButton.GetInt())
            {
                Main.ParaUsedButtonCount[pc.PlayerId] += 1;
                if (AmongUsClient.Instance.AmHost)
                {
                    _ = new LateTask(() => { Utils.SendMessage(Translator.GetString("SkillUsedLeft") + (Options.ParanoiaNumOfUseButton.GetInt() - Main.ParaUsedButtonCount[pc.PlayerId]), pc.PlayerId); }, 4.0f, "Paranoia Skill Remain Message");
                }

                pc.NoCheckStartMeeting(pc.Data);
            }
        }
    }
}
