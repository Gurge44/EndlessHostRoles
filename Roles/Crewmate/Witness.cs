using AmongUs.GameOptions;
using System.Collections.Generic;

namespace TOHE.Roles.Crewmate
{
    internal class Witness : RoleBase
    {
        public static Dictionary<byte, long> AllKillers = [];

        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = Options.WitnessCD.GetFloat();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(false);
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.KillButton?.OverrideText(Translator.GetString("WitnessButtonText"));
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            killer.SetKillCooldown();
            killer.Notify(AllKillers.ContainsKey(target.PlayerId) ? Translator.GetString("WitnessFoundKiller") : Translator.GetString("WitnessFoundInnocent"));
            return false;
        }
    }
}