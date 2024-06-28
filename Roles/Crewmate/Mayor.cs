using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Crewmate
{
    internal class Mayor : RoleBase
    {
        public static Dictionary<byte, int> MayorUsedButtonCount = [];

        public static bool On;

        public static OptionItem MayorAdditionalVote;
        public static OptionItem MayorHasPortableButton;
        public static OptionItem MayorNumOfUseButton;
        public static OptionItem MayorHideVote;
        public static OptionItem MayorRevealWhenDoneTasks;
        public static OptionItem MayorSeesVoteColorsWhenDoneTasks;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
            MayorUsedButtonCount[playerId] = 0;
        }

        public override void Init()
        {
            On = false;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown =
                !MayorUsedButtonCount.TryGetValue(playerId, out var count) || count < MayorNumOfUseButton.GetInt()
                    ? opt.GetInt(Int32OptionNames.EmergencyCooldown)
                    : 300f;
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (!MayorHasPortableButton.GetBool()) return;

            if (UsePets.GetBool()) hud.PetButton.buttonLabelText.text = Translator.GetString("MayorVentButtonText");
            else hud.AbilityButton.buttonLabelText.text = Translator.GetString("MayorVentButtonText");
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
            if (!MayorHasPortableButton.GetBool()) return;

            if (MayorUsedButtonCount.TryGetValue(pc.PlayerId, out var count) && count < MayorNumOfUseButton.GetInt())
            {
                pc.ReportDeadBody(null);
            }
        }

        public static void SetupCustomOption()
        {
            SetupRoleOptions(9500, TabGroup.CrewmateRoles, CustomRoles.Mayor);
            MayorAdditionalVote = new IntegerOptionItem(9510, "MayorAdditionalVote", new(0, 90, 1), 3, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor])
                .SetValueFormat(OptionFormat.Votes);
            MayorHasPortableButton = new BooleanOptionItem(9511, "MayorHasPortableButton", false, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);
            MayorNumOfUseButton = new IntegerOptionItem(9512, "MayorNumOfUseButton", new(1, 90, 1), 1, TabGroup.CrewmateRoles)
                .SetParent(MayorHasPortableButton)
                .SetValueFormat(OptionFormat.Times);
            MayorHideVote = new BooleanOptionItem(9513, "MayorHideVote", false, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);
            MayorRevealWhenDoneTasks = new BooleanOptionItem(9514, "MayorRevealWhenDoneTasks", false, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);
            MayorSeesVoteColorsWhenDoneTasks = new BooleanOptionItem(9515, "MayorSeesVoteColorsWhenDoneTasks", false, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mayor]);
            OverrideTasksData.Create(9516, TabGroup.CrewmateRoles, CustomRoles.Mayor);
        }
    }
}