using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Lovers : IAddon
    {
        public static bool IsChatActivated = false;

        public static OptionItem LoverSpawnChances;
        public static OptionItem LoverKnowRoles;
        public static OptionItem LoverSuicide;
        public static OptionItem ImpCanBeInLove;
        public static OptionItem CrewCanBeInLove;
        public static OptionItem NeutralCanBeInLove;
        public static OptionItem LegacyLovers;
        public static OptionItem LovingImpostorSpawnChance;
        public static OptionItem LovingImpostorRoleForOtherImps;
        public static OptionItem PrivateChat;
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

            LoverKnowRoles = new BooleanOptionItem(id + 4, "LoverKnowRoles", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            LoverSuicide = new BooleanOptionItem(id + 3, "LoverSuicide", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            ImpCanBeInLove = new BooleanOptionItem(id + 5, "ImpCanBeInLove", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            CrewCanBeInLove = new BooleanOptionItem(id + 6, "CrewCanBeInLove", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            NeutralCanBeInLove = new BooleanOptionItem(id + 7, "NeutralCanBeInLove", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            LegacyLovers = new BooleanOptionItem(id + 8, "LegacyLovers", false, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            LovingImpostorSpawnChance = new FloatOptionItem(id + 9, "LovingImpostorSpawnChance", new(0, 100, 5), 25, TabGroup.Addons)
                .SetParent(LegacyLovers)
                .SetValueFormat(OptionFormat.Percent)
                .SetGameMode(customGameMode);

            LovingImpostorRoleForOtherImps = new StringOptionItem(id + 12, "LIRoleForOtherImps", LIRole, 2, TabGroup.Addons)
                .SetParent(LovingImpostorSpawnChance)
                .SetGameMode(customGameMode);

            PrivateChat = new BooleanOptionItem(id + 10, "PrivateChat", false, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            GuessAbility = new StringOptionItem(id + 11, "GuessAbility", GuessModes, 1, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);


            var countOption = new IntegerOptionItem(id + 1, "NumberOfLovers", new(2, 2, 1), 2, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetHidden(true)
                .SetGameMode(customGameMode);

            CustomRoleSpawnChances.Add(role, spawnOption);
            CustomRoleCounts.Add(role, countOption);

            CustomAdtRoleSpawnRate.Add(role, rateOption);
        }
    }
}