using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Neutral;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate
{
    internal class Merchant
    {
        private static readonly int Id = 7300;
        private static readonly List<byte> playerIdList = new();

        public static Dictionary<byte, int> addonsSold = new();
        public static Dictionary<byte, List<byte>> bribedKiller = new();

        private static List<CustomRoles> addons = new();

        private static readonly List<CustomRoles> helpfulAddons = new List<CustomRoles>
        {
            CustomRoles.Bait,
            CustomRoles.Trapper,
            CustomRoles.Antidote,
            CustomRoles.Brakar, // Tiebreaker
            CustomRoles.Knighted,
            CustomRoles.Onbound,
            CustomRoles.Lucky,
            CustomRoles.DualPersonality // Schizophrenic
        };

        private static readonly List<CustomRoles> balancedAddons = new List<CustomRoles>
        {
            CustomRoles.Watcher,
            CustomRoles.Seer,
            CustomRoles.Necroview,
            CustomRoles.Glow,
            CustomRoles.Gravestone,
            CustomRoles.Autopsy,
        };

        private static readonly List<CustomRoles> harmfulAddons = new List<CustomRoles>
        {
            CustomRoles.Oblivious,
            CustomRoles.Bewilder,
            CustomRoles.Unreportable, // Disregarded
            CustomRoles.Avanger, // Avenger
            CustomRoles.Diseased,
            CustomRoles.Unlucky
        };

        private static readonly List<CustomRoles> neutralAddons = new List<CustomRoles>
        {
           CustomRoles.Soulless
        };

        private static readonly List<CustomRoles> experimentalAddons = new List<CustomRoles>
        {
        //    CustomRoles.Flashman,
            CustomRoles.Egoist,
            CustomRoles.Ntr, // Neptune
            CustomRoles.Guesser,
            CustomRoles.Fool
        };

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
        private static OptionItem OptionCanSellExperimental;
        private static OptionItem OptionSellOnlyHarmfulToEvil;
        private static OptionItem OptionSellOnlyHelpfulToCrew;

        private static int GetCurrentAmountOfMoney(byte playerId)
        {
            return (addonsSold[playerId] * OptionMoneyPerSell.GetInt()) - (bribedKiller[playerId].Count * OptionMoneyRequiredToBribe.GetInt());
        }

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Merchant);
            OptionMaxSell = IntegerOptionItem.Create(Id + 2, "MerchantMaxSell", new(1, 20, 1), 5, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]).SetValueFormat(OptionFormat.Times);
            OptionMoneyPerSell = IntegerOptionItem.Create(Id + 3, "MerchantMoneyPerSell", new(1, 20, 1), 1, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]).SetValueFormat(OptionFormat.Times);
            OptionMoneyRequiredToBribe = IntegerOptionItem.Create(Id + 4, "MerchantMoneyRequiredToBribe", new(1, 99, 1), 5, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]).SetValueFormat(OptionFormat.Times);
            OptionNotifyBribery = BooleanOptionItem.Create(Id + 5, "MerchantNotifyBribery", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanTargetCrew = BooleanOptionItem.Create(Id + 6, "MerchantTargetCrew", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanTargetImpostor = BooleanOptionItem.Create(Id + 7, "MerchantTargetImpostor", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanTargetNeutral = BooleanOptionItem.Create(Id + 8, "MerchantTargetNeutral", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanSellBalanced = BooleanOptionItem.Create(Id + 9, "MerchantSellBalanced", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanSellHelpful = BooleanOptionItem.Create(Id + 10, "MerchantSellHelpful", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanSellHarmful = BooleanOptionItem.Create(Id + 11, "MerchantSellHarmful", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanSellNeutral = BooleanOptionItem.Create(Id + 12, "MerchantSellNeutral", true, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanSellExperimental = BooleanOptionItem.Create(Id + 13, "MerchantSellExperimental", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionSellOnlyHarmfulToEvil = BooleanOptionItem.Create(Id + 14, "MerchantSellHarmfulToEvil", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionSellOnlyHelpfulToCrew = BooleanOptionItem.Create(Id + 15, "MerchantSellHelpfulToCrew", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);

            OverrideTasksData.Create(Id + 16, TabGroup.CrewmateRoles, CustomRoles.Merchant);
        }
        public static void Init()
        {
            playerIdList.Clear();

            addons = new List<CustomRoles>();
            addonsSold = new Dictionary<byte, int>();
            bribedKiller = new Dictionary<byte, List<byte>>();

            if (OptionCanSellHelpful.GetBool())
            {
                addons.AddRange(helpfulAddons);
            }

            if (OptionCanSellBalanced.GetBool())
            {
                addons.AddRange(balancedAddons);
            }

            if (OptionCanSellHarmful.GetBool())
            {
                addons.AddRange(harmfulAddons);
            }

            if (OptionCanSellNeutral.GetBool())
            {
                addons.AddRange(neutralAddons);
            }

            if (OptionCanSellExperimental.GetBool())
            {
                addons.AddRange(experimentalAddons);
            }
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            addonsSold.Add(playerId, 0);
            bribedKiller.Add(playerId, new List<byte>());
        }

        public static void OnTaskFinished(PlayerControl player)
        {
            if (!player.IsAlive() || !player.Is(CustomRoles.Merchant) || (addonsSold[player.PlayerId] >= OptionMaxSell.GetInt()))
            {
                return;
            }

            var rd = IRandom.Instance;
            CustomRoles addon = addons[rd.Next(0, addons.Count)];

            List<PlayerControl> AllAlivePlayer =
                Main.AllAlivePlayerControls.Where(x =>
                    (x.PlayerId != player.PlayerId && !Pelican.IsEaten(x.PlayerId))
                    &&
                    !x.Is(addon)
                    &&
                    !CustomRolesHelper.CheckAddonConfilct(addon, x)
                    &&
                    (
                        (OptionCanTargetCrew.GetBool() && CustomRolesHelper.IsCrewmate(x.GetCustomRole())) 
                        ||
                        (OptionCanTargetImpostor.GetBool() && CustomRolesHelper.IsImpostor(x.GetCustomRole()))
                        ||
                        (OptionCanTargetNeutral.GetBool() && (CustomRolesHelper.IsNeutral(x.GetCustomRole()) || CustomRolesHelper.IsNeutralKilling(x.GetCustomRole())))
                    )
                ).ToList();

            if (AllAlivePlayer.Count >= 1)
            {
                bool helpfulAddon = helpfulAddons.Contains(addon);
                bool harmfulAddon = !helpfulAddon;

                if (helpfulAddon && OptionSellOnlyHarmfulToEvil.GetBool())
                {
                    AllAlivePlayer = AllAlivePlayer.Where(a => CustomRolesHelper.IsCrewmate(a.GetCustomRole())).ToList();
                }

                if (harmfulAddon && OptionSellOnlyHelpfulToCrew.GetBool())
                {
                    AllAlivePlayer = AllAlivePlayer.Where(a =>
                        CustomRolesHelper.IsImpostor(a.GetCustomRole())
                        ||
                        CustomRolesHelper.IsNeutral(a.GetCustomRole())
                        ||
                        CustomRolesHelper.IsNeutralKilling(a.GetCustomRole())
                    ).ToList();
                }

                if (AllAlivePlayer.Count == 0)
                {
                    player.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("MerchantAddonSellFail")));
                    return;
                }

                PlayerControl target = AllAlivePlayer[rd.Next(0, AllAlivePlayer.Count)];

                target.RpcSetCustomRole(addon);
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("MerchantAddonSell")));
                player.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("MerchantAddonDelivered")));

                Utils.NotifyRoles();

                addonsSold[player.PlayerId] += 1;
            }
        }

        public static bool OnClientMurder(PlayerControl killer, PlayerControl target)
        {
            if (!target.Is(CustomRoles.Merchant))
            {
                return false;
            }

            if (bribedKiller[target.PlayerId].Contains(killer.PlayerId))
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

        public static bool IsBribedKiller(PlayerControl killer, PlayerControl target)
        {
            return bribedKiller[target.PlayerId].Contains(killer.PlayerId);
        }

        private static void NotifyBribery(PlayerControl killer, PlayerControl target)
        {
            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("BribedByMerchant")));

            if (OptionNotifyBribery.GetBool())
            {
                target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("MerchantKillAttemptBribed")));
            }
        }
    }
}
