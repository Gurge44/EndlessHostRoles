using System.Collections.Generic;
using System.Linq;
using EHR.Neutral;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Crewmate
{
    internal class Merchant : RoleBase
    {
        private const int Id = 7300;
        private static readonly List<byte> PlayerIdList = [];

        public static Dictionary<byte, int> AddonsSold = [];
        public static Dictionary<byte, List<byte>> BribedKiller = [];

        private static List<CustomRoles> Addons = [];
        private static Dictionary<AddonTypes, List<CustomRoles>> GroupedAddons = [];

        private static OptionItem OptionMaxSell;
        private static OptionItem OptionMoneyPerSell;
        private static OptionItem OptionMoneyRequiredToBribe;
        private static OptionItem OptionNotifyBribery;
        private static OptionItem OptionCanTargetCrew;
        private static OptionItem OptionCanTargetImpostor;
        private static OptionItem OptionCanTargetNeutral;
        private static OptionItem OptionCanSellMixed;
        private static OptionItem OptionCanSellHelpful;
        private static OptionItem OptionCanSellHarmful;
        private static OptionItem OptionSellOnlyEnabledAddons;
        private static OptionItem OptionSellOnlyHarmfulToEvil;
        private static OptionItem OptionSellOnlyHelpfulToCrew;
        private static OptionItem OptionGivesAllMoneyOnBribe;

        public override bool IsEnable => PlayerIdList.Count > 0;

        private static int GetCurrentAmountOfMoney(byte playerId)
        {
            return (AddonsSold[playerId] * OptionMoneyPerSell.GetInt()) - (BribedKiller[playerId].Count * OptionMoneyRequiredToBribe.GetInt());
        }

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Merchant);
            OptionMaxSell = new IntegerOptionItem(Id + 2, "MerchantMaxSell", new(1, 20, 1), 5, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]).SetValueFormat(OptionFormat.Times);
            OptionMoneyPerSell = new IntegerOptionItem(Id + 3, "MerchantMoneyPerSell", new(1, 20, 1), 1, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]).SetValueFormat(OptionFormat.Times);
            OptionMoneyRequiredToBribe = new IntegerOptionItem(Id + 4, "MerchantMoneyRequiredToBribe", new(1, 70, 1), 5, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]).SetValueFormat(OptionFormat.Times);
            OptionNotifyBribery = new BooleanOptionItem(Id + 5, "MerchantNotifyBribery", false, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanTargetCrew = new BooleanOptionItem(Id + 6, "MerchantTargetCrew", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanTargetImpostor = new BooleanOptionItem(Id + 7, "MerchantTargetImpostor", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanTargetNeutral = new BooleanOptionItem(Id + 8, "MerchantTargetNeutral", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanSellMixed = new BooleanOptionItem(Id + 9, "MerchantSellMixed", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanSellHelpful = new BooleanOptionItem(Id + 10, "MerchantSellHelpful", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionCanSellHarmful = new BooleanOptionItem(Id + 11, "MerchantSellHarmful", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionSellOnlyEnabledAddons = new BooleanOptionItem(Id + 12, "MerchantSellOnlyEnabledAddons", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionSellOnlyHarmfulToEvil = new BooleanOptionItem(Id + 13, "MerchantSellHarmfulToEvil", false, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionSellOnlyHelpfulToCrew = new BooleanOptionItem(Id + 14, "MerchantSellHelpfulToCrew", false, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);
            OptionGivesAllMoneyOnBribe = new BooleanOptionItem(Id + 15, "MerchantGivesAllMoneyOnBribe", true, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Merchant]);

            OverrideTasksData.Create(Id + 18, TabGroup.CrewmateRoles, CustomRoles.Merchant);
        }

        public override void Init()
        {
            PlayerIdList.Clear();

            AddonsSold = [];
            BribedKiller = [];

            GroupedAddons = Options.GroupedAddons.ToDictionary(x => x.Key, x => x.Value.ToList());

            if (!OptionCanSellHarmful.GetBool()) GroupedAddons.Remove(AddonTypes.Harmful);

            if (!OptionCanSellHelpful.GetBool()) GroupedAddons.Remove(AddonTypes.Helpful);

            if (!OptionCanSellMixed.GetBool()) GroupedAddons.Remove(AddonTypes.Mixed);

            GroupedAddons.Remove(AddonTypes.ImpOnly);

            Addons = GroupedAddons.Values.Flatten().ToList();

            if (OptionSellOnlyEnabledAddons.GetBool()) Addons.RemoveAll(x => x.GetMode() == 0);

            Addons.RemoveAll(StartGameHostPatch.BasisChangingAddons.ContainsKey);
            Addons.RemoveAll(x => x.IsNotAssignableMidGame());
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            AddonsSold.Add(playerId, 0);
            BribedKiller.Add(playerId, []);
        }

        public override void OnTaskComplete(PlayerControl player, int completedTaskCount, int totalTaskCount)
        {
            if (!player.IsAlive() || !player.Is(CustomRoles.Merchant) || AddonsSold[player.PlayerId] >= OptionMaxSell.GetInt()) return;

            if (Addons.Count == 0)
            {
                player.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("MerchantAddonSellFail")));
                Logger.Info("No addons to sell.", "Merchant");
                return;
            }

            CustomRoles addon = Addons.RandomElement();

            List<PlayerControl> availableTargets =
                Main.AllAlivePlayerControls.Where(x =>
                    x.PlayerId != player.PlayerId
                    && !Pelican.IsEaten(x.PlayerId)
                    && !x.Is(addon)
                    && CustomRolesHelper.CheckAddonConflict(addon, x)
                    && (Cleanser.CleansedCanGetAddon.GetBool() || (!Cleanser.CleansedCanGetAddon.GetBool() && !x.Is(CustomRoles.Cleansed)))
                    && ((OptionCanTargetCrew.GetBool() && x.IsCrewmate()) ||
                        (OptionCanTargetImpostor.GetBool() && x.GetCustomRole().IsImpostor()) ||
                        (OptionCanTargetNeutral.GetBool() && (x.GetCustomRole().IsNeutral() || x.IsNeutralKiller())))
                ).ToList();

            if (availableTargets.Count <= 0) return;

            bool helpfulAddon = GroupedAddons.TryGetValue(AddonTypes.Helpful, out List<CustomRoles> helpful) && helpful.Contains(addon);
            bool harmfulAddon = GroupedAddons.TryGetValue(AddonTypes.Harmful, out List<CustomRoles> harmful) && harmful.Contains(addon);

            if (helpfulAddon && OptionSellOnlyHarmfulToEvil.GetBool()) availableTargets.RemoveAll(x => !x.Is(Team.Crewmate));

            if (harmfulAddon && OptionSellOnlyHelpfulToCrew.GetBool()) availableTargets.RemoveAll(x => x.Is(Team.Crewmate));

            if (availableTargets.Count == 0)
            {
                player.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("MerchantAddonSellFail")));
                return;
            }

            PlayerControl target = availableTargets.RandomElement();

            target.RpcSetCustomRole(addon);
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("MerchantAddonSell")));
            player.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("MerchantAddonDelivered")));

            Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: target);
            Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: player);

            AddonsSold[player.PlayerId]++;
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
                BribedKiller[target.PlayerId].Add(killer.PlayerId);
                return true;
            }

            return false;
        }

        public static bool IsBribedKiller(PlayerControl killer, PlayerControl target)
        {
            return BribedKiller[target.PlayerId].Contains(killer.PlayerId);
        }

        private static void NotifyBribery(PlayerControl killer, PlayerControl target)
        {
            if (OptionGivesAllMoneyOnBribe.GetBool()) AddonsSold[target.PlayerId] = 0;

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("BribedByMerchant")));

            if (OptionNotifyBribery.GetBool()) target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Merchant), GetString("MerchantKillAttemptBribed")));
        }
    }
}