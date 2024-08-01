using System.Collections.Generic;
using AmongUs.GameOptions;
using static EHR.Options;

namespace EHR.Crewmate
{
    internal class Ventguard : RoleBase
    {
        public static bool On;

        public static List<int> BlockedVents = [];

        public static OptionItem VentguardBlockDoesNotAffectCrew;
        public static OptionItem VentguardAbilityUseGainWithEachTaskCompleted;
        public static OptionItem VentguardAbilityChargesWhenFinishedTasks;
        public static OptionItem VentguardMaxGuards;
        public static OptionItem VentguardNotifyOnBlockedVentUse;
        public static OptionItem VentguardBlocksResetOnMeeting;
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            SetupSingleRoleOptions(5525, TabGroup.CrewmateRoles, CustomRoles.Ventguard);
            VentguardAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(5527, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ventguard])
                .SetValueFormat(OptionFormat.Times);
            VentguardAbilityChargesWhenFinishedTasks = new FloatOptionItem(5530, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ventguard])
                .SetValueFormat(OptionFormat.Times);
            VentguardMaxGuards = new IntegerOptionItem(5528, "VentguardMaxGuards", new(1, 30, 1), 3, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ventguard]);
            VentguardBlockDoesNotAffectCrew = new BooleanOptionItem(5529, "VentguardBlockDoesNotAffectCrew", true, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ventguard]);
            VentguardNotifyOnBlockedVentUse = new BooleanOptionItem(5531, "VentguardNotifyOnBlockedVentUse", true, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ventguard]);
            VentguardBlocksResetOnMeeting = new BooleanOptionItem(5532, "VentguardBlocksResetOnMeeting", true, TabGroup.CrewmateRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Ventguard]);
        }

        public override void Add(byte playerId)
        {
            On = true;
            playerId.SetAbilityUseLimit(VentguardMaxGuards.GetInt());
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
                if (!BlockedVents.Contains(vent.Id)) BlockedVents.Add(vent.Id);
                pc.Notify(Translator.GetString("VentBlockSuccess"));
            }
            else
            {
                pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
            }
        }

        public override void AfterMeetingTasks()
        {
            if (VentguardBlocksResetOnMeeting.GetBool())
                BlockedVents.Clear();
        }
    }
}