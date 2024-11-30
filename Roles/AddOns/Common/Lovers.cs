using System;
using System.Linq;
using static EHR.Options;

namespace EHR.AddOns.Common
{
    internal class Lovers : IAddon
    {
        public static bool IsChatActivated = false;

        public static OptionItem LoverSpawnChances;
        public static OptionItem LoverKnowRoles;
        public static OptionItem LoverDieConsequence;
        public static OptionItem LoverSuicideTime;
        public static OptionItem ImpCanBeInLove;
        public static OptionItem CrewCanBeInLove;
        public static OptionItem NeutralCanBeInLove;
        public static OptionItem CrewLoversWinWithCrew;
        public static OptionItem LegacyLovers;
        public static OptionItem LovingImpostorSpawnChance;
        public static OptionItem LovingImpostorRoleForOtherImps;
        public static OptionItem PrivateChat;
        public static OptionItem PrivateChatForLoversOnly;
        public static OptionItem GuessAbility;

        private static readonly string[] GuessModes =
        [
            "RoleOff", // 0
            "Untouched", // 1
            "RoleOn" // 2
        ];

        private static readonly string[] LIRole =
        [
            "Impostor",
            "RandomONImpRole",
            "LovingImpostor"
        ];

        private static readonly string[] Consequences =
        [
            "Nothing",
            "Suicide",
            "HalvedVision"
        ];

        private static readonly string[] SuicideTimes =
        [
            "Immediately",
            "WhenNextMeetingStarts",
            "WhenNextMeetingEnds"
        ];

        public static CustomRoles LovingImpostorRole;

        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            const CustomRoles role = CustomRoles.Lovers;
            const int id = 16200;
            const CustomGameMode customGameMode = CustomGameMode.Standard;

            var spawnOption = new StringOptionItem(id, role.ToString(), RatesZeroOne, 0, TabGroup.Addons)
                .SetColor(Utils.GetRoleColor(role))
                .SetHeader(true)
                .SetGameMode(customGameMode) as StringOptionItem;

            var rateOption = new IntegerOptionItem(id + 2, "LoverSpawnChances", new(0, 100, 5), 50, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetValueFormat(OptionFormat.Percent)
                .SetGameMode(customGameMode) as IntegerOptionItem;

            LoverSpawnChances = rateOption;

            LoverDieConsequence = new StringOptionItem(id + 3, "LoverDieConsequence", Consequences, 0, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            LoverSuicideTime = new StringOptionItem(id + 4, "LoverSuicideTime", SuicideTimes, 0, TabGroup.Addons)
                .SetParent(LoverDieConsequence)
                .SetValueFormat(OptionFormat.Seconds)
                .SetGameMode(customGameMode);

            LoverKnowRoles = new BooleanOptionItem(id + 5, "LoverKnowRoles", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            PrivateChat = new BooleanOptionItem(id + 6, "PrivateChat", false, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            PrivateChatForLoversOnly = new BooleanOptionItem(id + 7, "PrivateChatForLoversOnly", false, TabGroup.Addons)
                .SetParent(PrivateChat)
                .SetGameMode(customGameMode);

            CrewLoversWinWithCrew = new BooleanOptionItem(id + 8, "CrewLoversWinWithCrew", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            GuessAbility = new StringOptionItem(id + 9, "GuessAbility", GuessModes, 1, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            LegacyLovers = new BooleanOptionItem(id + 10, "LegacyLovers", false, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode)
                .RegisterUpdateValueEvent((_, _) => new[] { ImpCanBeInLove, CrewCanBeInLove, NeutralCanBeInLove }.Do(x => x.SetHidden(LegacyLovers.GetBool())));

            LovingImpostorSpawnChance = new FloatOptionItem(id + 11, "LovingImpostorSpawnChance", new(0, 100, 5), 25, TabGroup.Addons)
                .SetParent(LegacyLovers)
                .SetValueFormat(OptionFormat.Percent)
                .SetGameMode(customGameMode);

            LovingImpostorRoleForOtherImps = new StringOptionItem(id + 12, "LIRoleForOtherImps", LIRole, 2, TabGroup.Addons)
                .SetParent(LovingImpostorSpawnChance)
                .SetGameMode(customGameMode);

            ImpCanBeInLove = new BooleanOptionItem(id + 13, "ImpCanBeInLove", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetHidden(LegacyLovers.GetBool())
                .SetGameMode(customGameMode);

            CrewCanBeInLove = new BooleanOptionItem(id + 14, "CrewCanBeInLove", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetHidden(LegacyLovers.GetBool())
                .SetGameMode(customGameMode);

            NeutralCanBeInLove = new BooleanOptionItem(id + 15, "NeutralCanBeInLove", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetHidden(LegacyLovers.GetBool())
                .SetGameMode(customGameMode);


            OptionItem countOption = new IntegerOptionItem(id + 1, "NumberOfLovers", new(2, 2, 1), 2, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetHidden(true)
                .SetGameMode(customGameMode);

            CustomRoleSpawnChances.Add(role, spawnOption);
            CustomRoleCounts.Add(role, countOption);

            CustomAdtRoleSpawnRate.Add(role, rateOption);
        }

        public static void Init()
        {
            try
            {
                LovingImpostorRole = Enum.GetValues<CustomRoles>().Where(x => x.IsEnable() && x.IsImpostor() && x != CustomRoles.LovingImpostor && !x.RoleExist(true) && !HnSManager.AllHnSRoles.Contains(x)).Shuffle()[0];
            }
            catch
            {
                LovingImpostorRole = CustomRoles.LovingImpostor;
            }
        }
    }
}