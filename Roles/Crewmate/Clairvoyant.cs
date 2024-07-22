using System;
using System.Collections.Generic;
using AmongUs.GameOptions;

namespace EHR.Crewmate
{
    public class Clairvoyant : RoleBase
    {
        public static bool On;

        public static Dictionary<Options.GameStateInfo, OptionItem> Settings = [];
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(644970, TabGroup.CrewmateRoles, CustomRoles.Clairvoyant);

            int i = 2;
            foreach (var s in Enum.GetValues<Options.GameStateInfo>())
            {
                Settings[s] = new BooleanOptionItem(644970 + i, $"GameStateCommand.Show{s}", true, TabGroup.CrewmateRoles)
                    .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Clairvoyant]);
                i++;
            }
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (Options.UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = 1f;
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        static void UseAbility(PlayerControl pc) => pc.Notify(Utils.GetGameStateData(clairvoyant: true));
        public override void OnPet(PlayerControl pc) => UseAbility(pc);
        public override void OnEnterVent(PlayerControl pc, Vent vent) => UseAbility(pc);
    }
}