using AmongUs.GameOptions;

namespace TOHE.Roles.Crewmate
{
    internal class Ventguard : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(Options.VentguardMaxGuards.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerInVentMaxTime = 1f;
            AURoleOptions.EngineerCooldown = 15f;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.AbilityButton.buttonLabelText.text = Translator.GetString("VentguardVentButtonText");
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            if (pc.GetAbilityUseLimit() >= 1)
            {
                pc.RpcRemoveAbilityUse();
                if (!Main.BlockedVents.Contains(vent.Id)) Main.BlockedVents.Add(vent.Id);
                pc.Notify(Translator.GetString("VentBlockSuccess"));
            }
            else
            {
                pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }
    }
}
