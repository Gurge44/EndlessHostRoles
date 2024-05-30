using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Lovers : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public static OptionItem LegacyLovers;
        public static OptionItem LovingImpostorSpawnChance;
        public static OptionItem PrivateChat;
        public static OptionItem CannotBeGuessed;

        public void SetupCustomOption()
        {
            const CustomRoles role = CustomRoles.Lovers;
            const int id = 16200;
            const CustomGameMode customGameMode = CustomGameMode.Standard;

            var spawnOption = StringOptionItem.Create(id, role.ToString(), RatesZeroOne, 0, TabGroup.Addons)
                .SetColor(Utils.GetRoleColor(role))
                .SetHeader(true)
                .SetGameMode(customGameMode) as StringOptionItem;

            var rateOption = IntegerOptionItem.Create(id + 2, "LoverSpawnChances", new(0, 100, 5), 50, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetValueFormat(OptionFormat.Percent)
                .SetGameMode(customGameMode) as IntegerOptionItem;
            LoverSpawnChances = rateOption;

            LoverKnowRoles = BooleanOptionItem.Create(id + 4, "LoverKnowRoles", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            LoverSuicide = BooleanOptionItem.Create(id + 3, "LoverSuicide", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            ImpCanBeInLove = BooleanOptionItem.Create(id + 5, "ImpCanBeInLove", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            CrewCanBeInLove = BooleanOptionItem.Create(id + 6, "CrewCanBeInLove", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            NeutralCanBeInLove = BooleanOptionItem.Create(id + 7, "NeutralCanBeInLove", true, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            LegacyLovers = BooleanOptionItem.Create(id + 8, "LegacyLovers", false, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            LovingImpostorSpawnChance = FloatOptionItem.Create(id + 9, "LovingImpostorSpawnChance", new(0, 100, 5), 25, TabGroup.Addons)
                .SetParent(LegacyLovers)
                .SetGameMode(customGameMode);

            PrivateChat = BooleanOptionItem.Create(id + 10, "PrivateChat", false, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);

            CannotBeGuessed = BooleanOptionItem.Create(id + 11, "CannotBeGuessed", false, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetGameMode(customGameMode);


            var countOption = IntegerOptionItem.Create(id + 1, "NumberOfLovers", new(2, 2, 1), 2, TabGroup.Addons)
                .SetParent(spawnOption)
                .SetHidden(true)
                .SetGameMode(customGameMode);

            CustomRoleSpawnChances.Add(role, spawnOption);
            CustomRoleCounts.Add(role, countOption);

            CustomAdtRoleSpawnRate.Add(role, rateOption);
        }
    }
}
