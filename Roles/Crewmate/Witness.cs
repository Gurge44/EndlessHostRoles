using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Crewmate
{
    internal class Witness : RoleBase
    {
        public static Dictionary<byte, long> AllKillers = [];

        public static bool On;
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(8550, TabGroup.CrewmateRoles, CustomRoles.Witness);
            WitnessCD = new FloatOptionItem(8552, "AbilityCD", new(0f, 60f, 2.5f), 15f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Witness])
                .SetValueFormat(OptionFormat.Seconds);
            WitnessTime = new IntegerOptionItem(8553, "WitnessTime", new(0, 90, 1), 10, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Witness])
                .SetValueFormat(OptionFormat.Seconds);
            WitnessUsePet = CreatePetUseSetting(8554, CustomRoles.Witness);
        }

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
            Main.AllPlayerKillCooldown[id] = WitnessCD.GetFloat();
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