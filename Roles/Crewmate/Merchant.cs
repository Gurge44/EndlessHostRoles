using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate
{
    internal class Merchant : RoleBase
    {
        private const int Id = 7300;
        private static readonly List<byte> PlayerIdList = [];

        public static Dictionary<byte, int> addonsSold = [];
        public static Dictionary<byte, List<byte>> bribedKiller = [];

        private static List<CustomRoles> addons = [];

        private static readonly List<CustomRoles> HelpfulAddons =
        [
            CustomRoles.Bait,
            CustomRoles.Trapper,
            CustomRoles.Antidote,
            CustomRoles.Brakar, // Tiebreaker
            CustomRoles.Knighted,
            CustomRoles.Physicist,
            CustomRoles.Nimble,
            CustomRoles.Onbound,
            CustomRoles.Lucky,
            CustomRoles.DualPersonality // Schizophrenic
        ];

        private static readonly List<CustomRoles> BalancedAddons =
        [
            CustomRoles.Watcher,
            CustomRoles.Sleuth,
            CustomRoles.Mischievous,
            CustomRoles.Seer,
            CustomRoles.Busy,
            CustomRoles.Disco,
            CustomRoles.Necroview,
            CustomRoles.Glow,
            CustomRoles.Gravestone,
            CustomRoles.Autopsy,
        ];

        private static readonly List<CustomRoles> HarmfulAddons =
        [
            CustomRoles.Oblivious,
            CustomRoles.Bewilder,
            CustomRoles.Asthmatic,
            CustomRoles.Unreportable, // Disregarded
            CustomRoles.Avanger, // Avenger
            CustomRoles.Diseased,
            CustomRoles.Truant,
            CustomRoles.Unlucky
        ];

        private static readonly List<CustomRoles> NeutralAddons =
        [
            CustomRoles.Undead,
            CustomRoles.Contagious
        ];

        private static readonly List<CustomRoles> ExperimentalAddons =
        [
            CustomRoles.Flashman,
            CustomRoles.Giant,
            CustomRoles.Egoist,
            CustomRoles.Ntr, // Neptune
            CustomRoles.Guesser,
            CustomRoles.Fool
        ];

        private static OptionItem OptionMaxSell;
        private static OptionItem OptionMoneyPerSell;
        private static OptionItem OptionMoneyRequiredToBribe;
        private static OptionItem OptionNotifyBribery;
        private static OptionItem OptionCanTargetCrew;
        private static OptionItem OptionCanTargetImpostor;
        private static OptionItem OptionCanTargetNeutral;
        private static OptionItem OptionCanSellBalanced;
        private static OptionItem OptionCanSellHelpful;
        private static OptionItem OptionCanSellHarmful;
        private static OptionItem OptionCanSellNeutral;
        private static OptionItem OptionSellOnlyEnabledAddons;
        private static OptionItem OptionCanSellExperimental;
        private static OptionItem OptionSellOnlyHarmfulToEvil;
        private static OptionItem OptionSellOnlyHelpfulToCrew;
        private static OptionItem OptionGivesAllMoneyOnBribe;

        private static int GetCurrentAmountOfMoney(byte playerId) => (addonsSold[playerId] * OptionMoneyPerSell.GetInt()) - (bribedKiller[playerId].Count * OptionMoneyRequiredToBribe.GetInt());

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Merchant);
            OptionMaxSell = IntegerOptionItem.Create(Id + 2, "MerchantMaxSell", new(1, 20, 1), 5, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]).SetValueFormat(OptionFormat.Times);
            OptionMoneyPerSell = IntegerOptionItem.Create(Id + 3, "MerchantMoneyPerSell", new(1, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]).SetValueFormat(OptionFormat.Times);
            OptionMoneyRequiredToBribe = IntegerOptionItem.Create(Id + 4, "MerchantMoneyRequiredToBribe", new(1, 70, 1), 5, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]).SetValueFormat(OptionFormat.Times);
            OptionNotifyBribery = BooleanOptionItem.Create(Id + 5, "MerchantNotifyBribery", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanTargetCrew = BooleanOptionItem.Create(Id + 6, "MerchantTargetCrew", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanTargetImpostor = BooleanOptionItem.Create(Id + 7, "MerchantTargetImpostor", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanTargetNeutral = BooleanOptionItem.Create(Id + 8, "MerchantTargetNeutral", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanSellBalanced = BooleanOptionItem.Create(Id + 9, "MerchantSellBalanced", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanSellHelpful = BooleanOptionItem.Create(Id + 10, "MerchantSellHelpful", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanSellHarmful = BooleanOptionItem.Create(Id + 11, "MerchantSellHarmful", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanSellNeutral = BooleanOptionItem.Create(Id + 12, "MerchantSellNeutral", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanSellExperimental = BooleanOptionItem.Create(Id + 13, "MerchantSellExperimental", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionSellOnlyEnabledAddons = BooleanOptionItem.Create(Id + 16, "MerchantSellOnlyEnabledAddons", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionSellOnlyHarmfulToEvil = BooleanOptionItem.Create(Id + 14, "MerchantSellHarmfulToEvil", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionSellOnlyHelpfulToCrew = BooleanOptionItem.Create(Id + 15, "MerchantSellHelpfulToCrew", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionGivesAllMoneyOnBribe = BooleanOptionItem.Create(Id + 17, "MerchantGivesAllMoneyOnBribe", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);

            OverrideTasksData.Create(Id + 18, TabGroup.CrewmateRoles, CustomRoles.Merchant);
        }

        public override void Init()
        {
            PlayerIdList.Clear();

            addons = [];
            addonsSold = [];
            bribedKiller = [];

            if (OptionCanSellHelpful.GetBool()) addons.AddRange(HelpfulAddons);
            if (OptionCanSellBalanced.GetBool()) addons.AddRange(BalancedAddons);
            if (OptionCanSellHarmful.GetBool()) addons.AddRange(HarmfulAddons);
            if (OptionCanSellNeutral.GetBool()) addons.AddRange(NeutralAddons);
            if (OptionCanSellExperimental.GetBool()) addons.AddRange(ExperimentalAddons);
            if (OptionSellOnlyEnabledAddons.GetBool()) addons = addons.Where(role => role.GetMode() != 0).ToList();
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            addonsSold.Add(playerId, 0);
            bribedKiller.Add(playerId, []);
        }

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void OnTaskComplete(PlayerControl player, int completedTaskCount, int totalTaskCount)
        {
            if (!player.IsAlive() || !player.Is(CustomRoles.Merchant) || (addonsSold[player.PlayerId] >= OptionMaxSell.GetInt()))
            {
                return;
            }

            if (addons.Count == 0)
            {
                player.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("MerchantAddonSellFail")));
                Logger.Info("No addons to sell.", "Merchant");
                return;
            }

            var rd = IRandom.Instance;
            CustomRoles addon = addons[rd.Next(0, addons.Count)];

            PlayerControl[] AllAlivePlayer =
                Main.AllAlivePlayerControls.Where(x =>
                    x.PlayerId != player.PlayerId && !Pelican.IsEaten(x.PlayerId)
                                                  && !x.Is(addon)
                                                  && !CustomRolesHelper.CheckAddonConflict(addon, x)
                                                  && (Cleanser.CleansedCanGetAddon.GetBool() || (!Cleanser.CleansedCanGetAddon.GetBool() && !x.Is(CustomRoles.Cleansed)))
                                                  && (
                                                      (OptionCanTargetCrew.GetBool() && x.GetCustomRole().IsCrewmate()) ||
                                                      (OptionCanTargetImpostor.GetBool() && x.GetCustomRole().IsImpostor()) ||
                                                      (OptionCanTargetNeutral.GetBool() && (x.GetCustomRole().IsNeutral() ||
                                                                                            x.GetCustomRole().IsNeutralKilling()))
                                                  )
                ).ToArray();

            if (AllAlivePlayer.Length > 0)
            {
                bool helpfulAddon = HelpfulAddons.Contains(addon);
                bool harmfulAddon = !helpfulAddon;

                if (helpfulAddon && OptionSellOnlyHarmfulToEvil.GetBool())
                {
                    AllAlivePlayer = AllAlivePlayer.Where(a => a.GetCustomRole().IsCrewmate()).ToArray();
                }

                if (harmfulAddon && OptionSellOnlyHelpfulToCrew.GetBool())
                {
                    AllAlivePlayer = AllAlivePlayer.Where(a =>
                        a.GetCustomRole().IsImpostor()
                        ||
                        a.GetCustomRole().IsNeutral()
                        ||
                        a.GetCustomRole().IsNeutralKilling()
                    ).ToArray();
                }

                if (AllAlivePlayer.Length == 0)
                {
                    player.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("MerchantAddonSellFail")));
                    return;
                }

                PlayerControl target = AllAlivePlayer[rd.Next(0, AllAlivePlayer.Length)];

                target.RpcSetCustomRole(addon);
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("MerchantAddonSell")));
                player.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("MerchantAddonDelivered")));

                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: target);
                Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: player);

                addonsSold[player.PlayerId] += 1;
            }
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            if (IsBribedKiller(killer, target))
            {
                NotifyBribery(killer, target);
                return true;
            }

            if (GetCurrentAmountOfMoney(target.PlayerId) >= OptionMoneyRequiredToBribe.GetInt())
            {
                NotifyBribery(killer, target);
                bribedKiller[target.PlayerId].Add(killer.PlayerId);
                return true;
            }

            return false;
        }

        public static bool IsBribedKiller(PlayerControl killer, PlayerControl target) => bribedKiller[target.PlayerId].Contains(killer.PlayerId);

        private static void NotifyBribery(PlayerControl killer, PlayerControl target)
        {
            if (OptionGivesAllMoneyOnBribe.GetBool()) addonsSold[target.PlayerId] = 0;

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("BribedByMerchant")));

            if (OptionNotifyBribery.GetBool())
            {
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("MerchantKillAttemptBribed")));
            }
        }
    }
}