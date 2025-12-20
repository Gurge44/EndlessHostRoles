using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR;

public static class BedWars
{
    private static int InventorySlots = 9;
    private static int SpeedPotionDuration = 30;
    private static int InvisPotionDuration = 20;
    private static int ReviveTime = 15;
    private static int IronGenerationInterval = 3;
    private static int GoldGenerationInterval = 10;
    private static int EmeraldGenerationInterval = 20;
    private static int DiamondGenerationInterval = 25;
    private static int GracePeriod = 90;
    private static int HealWaitAfterDamage = 5;
    private static int MaxHealth = 20;
    private static int TrapEffectDuration = 10;
    private static int TNTDamage = 20;
    private static int TNTBedDamage = 10;
    private static float TNTRange = 3f;
    private static float TrapTriggerRange = 3f;
    private static float HealPoolRange = 2.5f;
    private static float ShopAndItemGeneratorRange = 1f;
    private static float BedBreakAndProtectRange = 1f;
    private static float IronArmorDamageDivision = 1.25f;
    private static float DiamondArmorDamageDivision = 2f;
    private static float SpeedPotionSpeedIncrease = 0.5f;
    private static float TrappedSpeedDecrease = 0.5f;
    private static float TrappedVision = 0.2f;
    private static float WoodenSwordDamageMultiplier = 2f;
    private static float IronSwordDamageMultiplier = 4f;
    private static float DiamondSwordDamageMultiplier = 7f;
    private static bool SuddenDeath;
    private static int AllBedsBrokenAfterTime = 300;

    private static OptionItem InventorySlotsOption;
    private static OptionItem SpeedPotionDurationOption;
    private static OptionItem InvisPotionDurationOption;
    private static OptionItem ReviveTimeOption;
    private static OptionItem IronGenerationIntervalOption;
    private static OptionItem GoldGenerationIntervalOption;
    private static OptionItem EmeraldGenerationIntervalOption;
    private static OptionItem DiamondGenerationIntervalOption;
    private static OptionItem GracePeriodOption;
    private static OptionItem HealWaitAfterDamageOption;
    private static OptionItem MaxHealthOption;
    private static OptionItem TrapEffectDurationOption;
    private static OptionItem TNTDamageOption;
    private static OptionItem TNTBedDamageOption;
    private static OptionItem TNTRangeOption;
    private static OptionItem TrapTriggerRangeOption;
    private static OptionItem HealPoolRangeOption;
    private static OptionItem ShopAndItemGeneratorRangeOption;
    private static OptionItem BedBreakAndProtectRangeOption;
    private static OptionItem IronArmorDamageDivisionOption;
    private static OptionItem DiamondArmorDamageDivisionOption;
    private static OptionItem SpeedPotionSpeedIncreaseOption;
    private static OptionItem TrappedSpeedDecreaseOption;
    private static OptionItem TrappedVisionOption;
    private static OptionItem WoodenSwordDamageMultiplierOption;
    private static OptionItem IronSwordDamageMultiplierOption;
    private static OptionItem DiamondSwordDamageMultiplierOption;
    private static OptionItem SuddenDeathOption;
    private static OptionItem AllBedsBrokenAfterTimeOption;

    public static (Color Color, string Team) WinnerData = (Color.white, "No one wins");

    public static void SetupCustomOption()
    {
        var id = 69_222_001;
        Color color = Utils.GetRoleColor(CustomRoles.BedWarsPlayer);
        const CustomGameMode gameMode = CustomGameMode.BedWars;
        const TabGroup tab = TabGroup.GameSettings;

        InventorySlotsOption = new IntegerOptionItem(id++, "BedWars.InventorySlotsOption", new(1, 12, 1), 9, tab)
            .SetHeader(true)
            .SetColor(color)
            .SetGameMode(gameMode);

        SpeedPotionDurationOption = new IntegerOptionItem(id++, "BedWars.SpeedPotionDurationOption", new(1, 120, 1), 30, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);

        InvisPotionDurationOption = new IntegerOptionItem(id++, "BedWars.InvisPotionDurationOption", new(1, 120, 1), 20, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);

        ReviveTimeOption = new IntegerOptionItem(id++, "BedWars.ReviveTimeOption", new(1, 120, 1), 15, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);

        IronGenerationIntervalOption = new IntegerOptionItem(id++, "BedWars.IronGenerationIntervalOption", new(1, 60, 1), 3, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);

        GoldGenerationIntervalOption = new IntegerOptionItem(id++, "BedWars.GoldGenerationIntervalOption", new(1, 60, 1), 10, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);

        EmeraldGenerationIntervalOption = new IntegerOptionItem(id++, "BedWars.EmeraldGenerationIntervalOption", new(1, 60, 1), 20, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);

        DiamondGenerationIntervalOption = new IntegerOptionItem(id++, "BedWars.DiamondGenerationIntervalOption", new(1, 60, 1), 25, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);

        GracePeriodOption = new IntegerOptionItem(id++, "BedWars.GracePeriodOption", new(5, 300, 5), 90, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);

        HealWaitAfterDamageOption = new IntegerOptionItem(id++, "BedWars.HealWaitAfterDamageOption", new(1, 60, 1), 5, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);

        MaxHealthOption = new IntegerOptionItem(id++, "BedWars.MaxHealthOption", new(1, 100, 1), 20, tab)
            .SetValueFormat(OptionFormat.Health)
            .SetColor(color)
            .SetGameMode(gameMode);

        TrapEffectDurationOption = new IntegerOptionItem(id++, "BedWars.TrapEffectDurationOption", new(1, 60, 1), 10, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);

        TNTDamageOption = new IntegerOptionItem(id++, "BedWars.TNTDamageOption", new(1, 100, 1), 20, tab)
            .SetColor(color)
            .SetGameMode(gameMode);

        TNTBedDamageOption = new IntegerOptionItem(id++, "BedWars.TNTBedDamageOption", new(1, 100, 1), 10, tab)
            .SetColor(color)
            .SetGameMode(gameMode);

        TNTRangeOption = new FloatOptionItem(id++, "BedWars.TNTRangeOption", new(0.25f, 10f, 0.25f), 3f, tab)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color)
            .SetGameMode(gameMode);

        TrapTriggerRangeOption = new FloatOptionItem(id++, "BedWars.TrapTriggerRangeOption", new(0.25f, 10f, 0.25f), 3f, tab)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color)
            .SetGameMode(gameMode);

        HealPoolRangeOption = new FloatOptionItem(id++, "BedWars.HealPoolRangeOption", new(0.25f, 10f, 0.25f), 2.5f, tab)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color)
            .SetGameMode(gameMode);

        ShopAndItemGeneratorRangeOption = new FloatOptionItem(id++, "BedWars.ShopAndItemGeneratorRangeOption", new(0.25f, 10f, 0.25f), 1f, tab)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color)
            .SetGameMode(gameMode);

        BedBreakAndProtectRangeOption = new FloatOptionItem(id++, "BedWars.BedBreakAndProtectRangeOption", new(0.25f, 10f, 0.25f), 1f, tab)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color)
            .SetGameMode(gameMode);

        IronArmorDamageDivisionOption = new FloatOptionItem(id++, "BedWars.IronArmorDamageDivisionOption", new(1f, 10f, 0.05f), 1.25f, tab)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color)
            .SetGameMode(gameMode);

        DiamondArmorDamageDivisionOption = new FloatOptionItem(id++, "BedWars.DiamondArmorDamageDivisionOption", new(1f, 10f, 0.05f), 2f, tab)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color)
            .SetGameMode(gameMode);

        SpeedPotionSpeedIncreaseOption = new FloatOptionItem(id++, "BedWars.SpeedPotionSpeedIncreaseOption", new(0.1f, 3f, 0.1f), 0.5f, tab)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color)
            .SetGameMode(gameMode);

        TrappedSpeedDecreaseOption = new FloatOptionItem(id++, "BedWars.TrappedSpeedDecreaseOption", new(0.1f, 1f, 0.1f), 0.5f, tab)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color)
            .SetGameMode(gameMode);

        TrappedVisionOption = new FloatOptionItem(id++, "BedWars.TrappedVisionOption", new(0f, 1f, 0.05f), 0.2f, tab)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color)
            .SetGameMode(gameMode);

        WoodenSwordDamageMultiplierOption = new FloatOptionItem(id++, "BedWars.WoodenSwordDamageMultiplierOption", new(1f, 10f, 0.1f), 2f, tab)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color)
            .SetGameMode(gameMode);

        IronSwordDamageMultiplierOption = new FloatOptionItem(id++, "BedWars.IronSwordDamageMultiplierOption", new(1f, 10f, 0.1f), 4f, tab)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color)
            .SetGameMode(gameMode);

        DiamondSwordDamageMultiplierOption = new FloatOptionItem(id++, "BedWars.DiamondSwordDamageMultiplierOption", new(1f, 10f, 0.1f), 7f, tab)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(color)
            .SetGameMode(gameMode);

        SuddenDeathOption = new BooleanOptionItem(id++, "BedWars.SuddenDeathOption", false, tab)
            .SetColor(color)
            .SetGameMode(gameMode);

        AllBedsBrokenAfterTimeOption = new IntegerOptionItem(id, "BedWars.AllBedsBrokenAfterTimeOption", new(10, 900, 10), 300, tab)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color)
            .SetGameMode(gameMode);
    }

    public static string GetNameColor(PlayerControl player)
    {
        if (!Data.TryGetValue(player.PlayerId, out PlayerData data)) return "#ffffff";

        return data.Team switch
        {
            BedWarsTeam.Blue => "#00ffff",
            BedWarsTeam.Yellow => "#ffff00",
            BedWarsTeam.Red => "#ff0000",
            BedWarsTeam.Green => "#00ff00",
            _ => "#ffffff"
        };
    }

    public static string GetSuffix(PlayerControl seer, PlayerControl target)
    {
        if (seer.PlayerId == target.PlayerId && Data.TryGetValue(seer.PlayerId, out PlayerData seerData))
            return seerData.BuildSuffix(seer);

        return Data.TryGetValue(target.PlayerId, out PlayerData targetData) ? targetData.GetInfoAsTarget(seer) : string.Empty;
    }

    public static string GetStatistics(byte id)
    {
        return !Data.TryGetValue(id, out PlayerData data) ? string.Empty : Utils.ColorString(data.Team.GetColor(), data.Team.GetName());
    }

    public static string GetHudText()
    {
        long gracePeriodSecondsLeft = GracePeriodEnd - Utils.TimeStamp;
        if (gracePeriodSecondsLeft > 0) return string.Format(Translator.GetString("Bedwars.HudText.GracePeriodLeft"), gracePeriodSecondsLeft);

        var sb = new StringBuilder();

        foreach (BedWarsTeam team in Enum.GetValues<BedWarsTeam>())
        {
            if (!AllNetObjects.TryGetValue(team, out NetObjectCollection netObjectCollection)) continue;

            if (!netObjectCollection.Bed.IsBroken)
                sb.AppendLine(Utils.ColorString(team.GetColor(), $"{team.GetName()} {Utils.ColorString(Color.green, "✓")}"));
            else
            {
                int playersLeftInTeam = Main.AllAlivePlayerControls.Count(x => Data.TryGetValue(x.PlayerId, out PlayerData data) && data.Team == team);
                sb.AppendLine($"{Utils.ColorString(team.GetColor(), team.GetName())} ({playersLeftInTeam})");
            }
        }

        return sb.ToString().Trim();
    }

    public static bool CheckForGameEnd(out GameOverReason reason)
    {
        reason = GameOverReason.ImpostorsByKill;

        if (!Main.IntroDestroyed || IsGracePeriod || GameStates.IsEnded) return false;

        if (Enum.GetValues<BedWarsTeam>().FindFirst(x => Main.AllAlivePlayerControls.Select(p => p.PlayerId).Concat(Reviving).All(p => !Data.TryGetValue(p, out PlayerData data) || data.Team == x), out BedWarsTeam team))
        {
            WinnerData = (team.GetColor(), team.GetName() + Translator.GetString("Win"));
            CustomWinnerHolder.WinnerIds = Data.Where(x => x.Value.Team == team).Select(x => x.Key).ToHashSet();
            Logger.Info($"Winners: {team.GetName()} - {string.Join(", ", CustomWinnerHolder.WinnerIds.Select(id => Main.AllPlayerNames.GetValueOrDefault(id, string.Empty)))}", "BedWars");
            SendRPC();
            return true;
        }

        return false;
    }

    public static void OnDisconnect(PlayerControl pc)
    {
        Data.Remove(pc.PlayerId);
        InShop.Remove(pc.PlayerId);
        Suffix.Remove(pc.PlayerId);
        Trapped.Remove(pc.PlayerId);
        Reviving.Remove(pc.PlayerId);
    }

    private static void SendRPC()
    {
        var w = Utils.CreateRPC(CustomRPC.BedWarsSync);

        w.Write(WinnerData.Color);
        w.Write(WinnerData.Team);

        w.Write(Data.Count);

        foreach ((byte id, PlayerData data) in Data)
        {
            w.Write(id);
            w.Write((byte)data.Team);
        }

        Utils.EndRPC(w);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        WinnerData.Color = reader.ReadColor();
        WinnerData.Team = reader.ReadString();

        int count = reader.ReadInt32();

        for (int i = 0; i < count; i++)
        {
            byte id = reader.ReadByte();

            if (!Data.TryGetValue(id, out PlayerData data))
                Data[id] = data = new PlayerData();

            data.Team = (BedWarsTeam)reader.ReadInt32();
        }
    }

    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (IsGracePeriod || !Data.TryGetValue(killer.PlayerId, out PlayerData killerData) || !Data.TryGetValue(target.PlayerId, out PlayerData targetData) || killerData.Team == targetData.Team) return;

        if (AllNetObjects.Values.Any(x => x.Bed.Breaking.Contains(killer.PlayerId)))
        {
            killer.Notify(Translator.GetString("Bedwars.CannotKillWhileBreakingBed"));
            return;
        }
        
        float damage = 1;

        damage *= killerData.Sword switch
        {
            Item.WoodenSword => WoodenSwordDamageMultiplier,
            Item.IronSword => IronSwordDamageMultiplier,
            Item.DiamondSword => DiamondSwordDamageMultiplier,
            _ => 1
        };

        if (killerData.Sword.HasValue && Upgrades.TryGetValue(killerData.Team, out HashSet<Upgrade> upgrades) && upgrades.Contains(Upgrade.Sharpness))
            damage *= 1.25f;
        
        if (killerData.IsBuffedTeam(out var buffRatio))
            damage *= buffRatio;

        RPC.PlaySoundRPC(killer.PlayerId, Sounds.KillSound);
        RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
        
        targetData.Damage(target, damage, killer);
        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);

        killer.SetKillCooldown();
    }

    public static void Initialize()
    {
        GracePeriodEnd = Utils.TimeStamp + 60;

        Data = [];
        InShop = [];
        Suffix = [];
        AllNetObjects = [];
        Upgrades = [];
        ItemGenerators = [];
        Trapped = [];
        Reviving = [];

        WinnerData = (Color.white, "No one wins");
        FixedUpdatePatch.LastUpdate = [];

        InventorySlots = InventorySlotsOption.GetInt();
        SpeedPotionDuration = SpeedPotionDurationOption.GetInt();
        InvisPotionDuration = InvisPotionDurationOption.GetInt();
        ReviveTime = ReviveTimeOption.GetInt();
        IronGenerationInterval = IronGenerationIntervalOption.GetInt();
        GoldGenerationInterval = GoldGenerationIntervalOption.GetInt();
        EmeraldGenerationInterval = EmeraldGenerationIntervalOption.GetInt();
        DiamondGenerationInterval = DiamondGenerationIntervalOption.GetInt();
        GracePeriod = GracePeriodOption.GetInt();
        HealWaitAfterDamage = HealWaitAfterDamageOption.GetInt();
        MaxHealth = MaxHealthOption.GetInt();
        TrapEffectDuration = TrapEffectDurationOption.GetInt();
        TNTDamage = TNTDamageOption.GetInt();
        TNTBedDamage = TNTBedDamageOption.GetInt();
        TNTRange = TNTRangeOption.GetFloat();
        TrapTriggerRange = TrapTriggerRangeOption.GetFloat();
        HealPoolRange = HealPoolRangeOption.GetFloat();
        ShopAndItemGeneratorRange = ShopAndItemGeneratorRangeOption.GetFloat();
        BedBreakAndProtectRange = BedBreakAndProtectRangeOption.GetFloat();
        IronArmorDamageDivision = IronArmorDamageDivisionOption.GetFloat();
        DiamondArmorDamageDivision = DiamondArmorDamageDivisionOption.GetFloat();
        SpeedPotionSpeedIncrease = SpeedPotionSpeedIncreaseOption.GetFloat();
        TrappedSpeedDecrease = TrappedSpeedDecreaseOption.GetFloat();
        TrappedVision = TrappedVisionOption.GetFloat();
        WoodenSwordDamageMultiplier = WoodenSwordDamageMultiplierOption.GetFloat();
        IronSwordDamageMultiplier = IronSwordDamageMultiplierOption.GetFloat();
        DiamondSwordDamageMultiplier = DiamondSwordDamageMultiplierOption.GetFloat();
        SuddenDeath = SuddenDeathOption.GetBool();
        AllBedsBrokenAfterTime = AllBedsBrokenAfterTimeOption.GetInt();
    }

    public static IEnumerator OnGameStart()
    {
        if (Options.CurrentGameMode != CustomGameMode.BedWars) yield break;

        WinnerData = (Color.white, "No one wins");

        yield return new WaitForSecondsRealtime(3f);

        if (GameStates.IsEnded || !GameStates.InGame || GameStates.IsLobby)
        {
            Logger.Error("Cannot start BedWars game due to invalid game state.", "BedWars");
            yield break;
        }

        // Assign players to teams
        List<PlayerControl> players = Main.AllAlivePlayerControls.Shuffle().ToList();
        if (Main.GM.Value) players.RemoveAll(x => x.IsHost());
        if (ChatCommands.Spectators.Count > 0) players.RemoveAll(x => ChatCommands.Spectators.Contains(x.PlayerId));

        Dictionary<byte, BedWarsTeam> playerTeams = players
            .Select(x => x.PlayerId)
            .Partition(4)
            .Zip(Enum.GetValues<BedWarsTeam>(), (pcs, team) => pcs.ToDictionary(x => x, _ => team))
            .Flatten()
            .ToDictionary(x => x.Key, x => x.Value);

        yield return new WaitForSeconds(0.2f);

        Utils.SetChatVisibleForAll();

        yield return new WaitForSeconds(0.2f);

        MapNames map = Main.CurrentMap;
        Dictionary<BedWarsTeam, Base> bases = Bases[map];

        foreach ((byte id, BedWarsTeam team) in playerTeams)
        {
            PlayerControl pc = id.GetPlayer();
            if (pc == null) continue;

            var data = new PlayerData
            {
                Team = team,
                Base = bases[team]
            };

            {
                var sender = CustomRpcSender.Create($"BedWars OnGameStart ({pc.GetRealName()})", SendOption.Reliable);

                sender.TP(pc, data.Base.SpawnPosition);

                byte colorId = team.GetColorId();

                pc.SetColor(colorId);

                sender.AutoStartRpc(pc.NetId, RpcCalls.SetColor)
                    .Write(pc.Data.NetId)
                    .Write(colorId)
                    .EndRpc();

                sender.SendMessage();
            }

            if (!pc.AmOwner)
            {
                var sender = CustomRpcSender.Create($"BedWars OnGameStart ({pc.GetRealName()}) (2)", SendOption.Reliable);
                sender.StartMessage(pc.OwnerId);

                sender.StartRpc(pc.NetId, RpcCalls.ProtectPlayer)
                    .WriteNetObject(pc)
                    .Write(0)
                    .EndRpc();

                foreach ((byte otherId, BedWarsTeam otherTeam) in playerTeams)
                {
                    PlayerControl target = otherId.GetPlayer();
                    if (target == null || target.PlayerId == pc.PlayerId || otherTeam != team) continue;

                    sender.StartRpc(target.NetId, RpcCalls.SetRole)
                        .Write((ushort)RoleTypes.Impostor)
                        .Write(true)
                        .EndRpc();
                }

                sender.SendMessage();
            }
            else
            {
                pc.Data.Role.SetCooldown();
            }

            Data[pc.PlayerId] = data;

            yield return new WaitForSeconds(0.2f);
        }

        yield return new WaitForSeconds(0.5f);

        if (GameStates.IsEnded || !GameStates.InGame || GameStates.IsLobby)
        {
            Logger.Error("Cannot start BedWars game due to invalid game state.", "BedWars");
            yield break;
        }

        string itemShopText = Utils.ColorString(Palette.Orange, Translator.GetString("Bedwars.Shop.ItemShop"));
        string upgradeShopText = Utils.ColorString(Color.magenta, Translator.GetString("Bedwars.Shop.UpgradeShop"));
        List<string> rooms = [Translator.GetString("Bedwars.Rooms")];

        foreach ((BedWarsTeam team, (Vector2 bedPos, SystemTypes room, Vector2 itemShopPos, Vector2 upgradeShopPos, _)) in bases)
        {
            Bed bed = team switch
            {
                BedWarsTeam.Blue => new BlueBed(bedPos),
                BedWarsTeam.Yellow => new YellowBed(bedPos),
                BedWarsTeam.Red => new RedBed(bedPos),
                BedWarsTeam.Green => new GreenBed(bedPos),
                _ => null
            };
            var itemShop = new BedWarsShop(itemShopPos, itemShopText);
            var upgradeShop = new BedWarsShop(upgradeShopPos, upgradeShopText);
            if (bed != null) AllNetObjects[team] = new(bed, new(itemShop), new(upgradeShop));
            rooms.Add(Utils.ColorString(team.GetColor(), Translator.GetString($"{room}")));

            yield return new WaitForSeconds(0.2f);
        }

        yield return new WaitForSeconds(0.2f);

        players.NotifyPlayers($"{rooms[0]}\n{string.Join(" | ", rooms.Skip(1))}", 20f, setName: false);

        yield return new WaitForSeconds(0.2f);

        foreach ((Item item, List<Vector2> positions) in ItemGeneratorPositions[map])
        {
            if (!ItemDisplayData.TryGetValue(item, out ItemDisplay display)) continue;
            string colorString = Utils.ColorString(display.Color, display.Icon.ToString());

            positions.ForEach(x =>
            {
                BedWarsItemGenerator cno = new(x, colorString);
                ItemGenerator generator = item switch
                {
                    Item.Iron => new IronGenerator(cno),
                    Item.Gold => new GoldGenerator(cno),
                    Item.Emerald => new EmeraldGenerator(cno),
                    Item.Diamond => new DiamondGenerator(cno),
                    _ => null
                };
                if (generator != null) ItemGenerators.Add(generator);
            });

            yield return new WaitForSeconds(0.2f);
        }

        GracePeriodEnd = Utils.TimeStamp + GracePeriod;
    }

    private static Dictionary<byte, PlayerData> Data = [];
    private static Dictionary<byte, Shop> InShop = [];
    private static Dictionary<byte, string> Suffix = [];
    private static Dictionary<BedWarsTeam, NetObjectCollection> AllNetObjects = [];
    private static Dictionary<BedWarsTeam, HashSet<Upgrade>> Upgrades = [];
    private static List<ItemGenerator> ItemGenerators = [];
    private static List<byte> Trapped = [];
    private static HashSet<byte> Reviving = [];

    private static long GracePeriodEnd;

    public static bool IsGracePeriod => GracePeriodEnd > Utils.TimeStamp;

    public static bool IsNotInLocalPlayersTeam(PlayerControl pc)
    {
        return !Data.TryGetValue(pc.PlayerId, out PlayerData data) || !Data.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out PlayerData lpData) || data.Team != lpData.Team;
    }

    //[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class FixedUpdatePatch
    {
        public static Dictionary<byte, long> LastUpdate = [];

        public static void Postfix(PlayerControl __instance)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || ExileController.Instance || Options.CurrentGameMode != CustomGameMode.BedWars || !Main.IntroDestroyed || GameStates.IsEnded || __instance == null || __instance.PlayerId >= 254 || IntroCutsceneDestroyPatch.IntroDestroyTS + 10 > Utils.TimeStamp) return;

            long now = Utils.TimeStamp;

            if (__instance.AmOwner)
            {
                ItemGenerators.ForEach(x => x.Update());

                if (SuddenDeath && now >= GracePeriodEnd + AllBedsBrokenAfterTime)
                    AllNetObjects.Values.DoIf(x => !x.Bed.IsBroken, x => x.Bed.Broken());
            }

            if (!Data.TryGetValue(__instance.PlayerId, out PlayerData data)) return;

            if (!LastUpdate.TryGetValue(__instance.PlayerId, out long lastUpdate) || lastUpdate != now)
            {
                LastUpdate[__instance.PlayerId] = now;

                Vector2 pos = __instance.Pos();

                if (GracePeriodEnd > now)
                {
                    PlainShipRoom room = __instance.GetPlainShipRoom();

                    bool allowed = (room != null && data.Base.Room == room.RoomId) || (Main.CurrentMap, data.Base.Room) switch
                    {
                        (MapNames.Skeld, SystemTypes.Nav) => pos.x > 13f,
                        (MapNames.Dleks, SystemTypes.Nav) => pos.x < -13f,
                        (MapNames.MiraHQ, SystemTypes.Launchpad) => pos.x < 5f,
                        (MapNames.MiraHQ, SystemTypes.Reactor) => pos.y > 10f,
                        (MapNames.MiraHQ, SystemTypes.Balcony) => pos.y < 2f,
                        (MapNames.Polus, SystemTypes.LifeSupp) => room != null && room.RoomId == SystemTypes.BoilerRoom,
                        (MapNames.Airship, SystemTypes.CargoBay) => room != null && room.RoomId == SystemTypes.Ventilation,
                        (MapNames.Airship, SystemTypes.MeetingRoom) => (room != null && room.RoomId == SystemTypes.GapRoom) || __instance.inMovingPlat || __instance.onLadder || __instance.MyPhysics.Animations.IsPlayingAnyLadderAnimation(),
                        (MapNames.Fungle, SystemTypes.Kitchen) => room != null && room.RoomId == SystemTypes.FishingDock,
                        (MapNames.Fungle, SystemTypes.Comms) => pos is { y: > 8f, x: > 19f },
                        (MapNames.Fungle, SystemTypes.Jungle) => pos is { x: > 10f, y: < -11f },
                        _ => false
                    };

                    if (!allowed)
                    {
                        RPC.PlaySoundRPC(__instance.PlayerId, Sounds.ImpDiscovered);
                        __instance.Notify(Translator.GetString("Bedwars.YouCannotLeaveRoomInGracePeriod"));
                        __instance.TP(data.Base.SpawnPosition);
                    }
                }

                if (InShop.TryGetValue(__instance.PlayerId, out Shop shop) && Vector2.Distance(pos, shop.NetObject.Position) > ShopAndItemGeneratorRange)
                {
                    shop.ExitShop(__instance);
                    InShop.Remove(__instance.PlayerId);
                    Logger.Info($"{__instance.GetRealName()} exited {shop.GetType().Name}", "BedWars");
                    if (__instance.AmOwner) Utils.DirtyName.Add(PlayerControl.LocalPlayer.PlayerId);
                }

                if (!InShop.ContainsKey(__instance.PlayerId))
                {
                    (Shop shop, float distance) nearestShop = AllNetObjects.Values.SelectMany(x => new Shop[] { x.ItemShop, x.UpgradeShop }).Select(x => (shop: x, distance: Vector2.Distance(pos, x.NetObject.Position))).MinBy(x => x.distance);

                    if (nearestShop.distance <= ShopAndItemGeneratorRange)
                    {
                        InShop[__instance.PlayerId] = nearestShop.shop;
                        nearestShop.shop.EnterShop(__instance);
                        Logger.Info($"{__instance.GetRealName()} entered {nearestShop.shop.GetType().Name}", "BedWars");
                        if (__instance.AmOwner) Utils.DirtyName.Add(PlayerControl.LocalPlayer.PlayerId);
                    }
                }

                bool hasUpgrades = Upgrades.TryGetValue(data.Team, out HashSet<Upgrade> upgrades);

                if (data.Health < MaxHealth && data.LastHeal != now)
                {
                    bool healPool = hasUpgrades && upgrades.Contains(Upgrade.HealPool) && Vector2.Distance(pos, data.Base.BedPosition) <= HealPoolRange;
                    bool shouldRegen = data.LastDamage + HealWaitAfterDamage <= now;

                    if (healPool || shouldRegen)
                    {
                        if (healPool && shouldRegen) data.Health++;
                        data.Health++;
                        data.LastHeal = now;
                        data.Health = Math.Min(data.Health, MaxHealth);
                        Utils.NotifyRoles(SpecifyTarget: __instance);
                        Logger.Info($"{__instance.GetRealName()}'s health: {data.Health}", "BedWars");
                    }
                }

                if (hasUpgrades && upgrades.Contains(Upgrade.Trap))
                {
                    PlayerControl enemy = null;

                    foreach ((byte id, PlayerData playerData) in Data)
                    {
                        PlayerControl player = id.GetPlayer();
                        if (player == null || !player.IsAlive() || playerData.Team == data.Team || Vector2.Distance(player.Pos(), data.Base.BedPosition) > TrapTriggerRange) continue;
                        enemy = player;
                        break;
                    }

                    if (enemy != null)
                    {
                        Logger.Info($"{enemy.GetRealName()} triggered trap for {data.Team} team", "BedWars");
                        upgrades.Remove(Upgrade.Trap);

                        enemy.RPCPlayCustomSound("FlashBang");
                        Trapped.Add(enemy.PlayerId);
                        Main.AllPlayerSpeed[enemy.PlayerId] -= TrappedSpeedDecrease;
                        enemy.MarkDirtySettings();

                        LateTask.New(() =>
                        {
                            if (GameStates.IsEnded || !GameStates.InGame || GameStates.IsLobby || enemy == null) return;
                            RPC.PlaySoundRPC(enemy.PlayerId, Sounds.TaskComplete);
                            Trapped.Remove(enemy.PlayerId);
                            Main.AllPlayerSpeed[enemy.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                            enemy.MarkDirtySettings();
                        }, TrapEffectDuration);

                        foreach ((byte id, PlayerData otherData) in Data)
                        {
                            PlayerControl player = id.GetPlayer();
                            if (player == null || !player.IsAlive() || otherData.Team != data.Team) continue;

                            RPC.PlaySoundRPC(player.PlayerId, Sounds.SabotageSound);
                            player.Notify(string.Format(Translator.GetString("Bedwars.TrapTriggered"), enemy.PlayerId.ColoredPlayerName()));
                        }
                    }
                }

                string lastSuffix = Suffix.TryGetValue(__instance.PlayerId, out string suffix) ? suffix : string.Empty;
                string currentSuffix = data.BuildSuffix(__instance);

                if (lastSuffix != currentSuffix)
                {
                    Suffix[__instance.PlayerId] = currentSuffix;
                    Utils.NotifyRoles(SpecifySeer: __instance, SpecifyTarget: __instance);
                }
            }
        }
    }

    private class PlayerData
    {
        public readonly Inventory Inventory = new();
        public float Health = MaxHealth;
        public long LastHeal = Utils.TimeStamp;
        public long LastDamage = Utils.TimeStamp;
        public BedWarsTeam Team;
        public Base Base;
        public Item? Armor;
        public Item? Sword;

        public void Damage(PlayerControl pc, float damage, PlayerControl killer = null)
        {
            if (Health <= 0 || IsGracePeriod) return;

            damage /= Armor switch
            {
                Item.IronArmor => IronArmorDamageDivision,
                Item.DiamondArmor => DiamondArmorDamageDivision,
                _ => 1f
            };

            if (Armor.HasValue && Upgrades.TryGetValue(Team, out HashSet<Upgrade> upgrades) && upgrades.Contains(Upgrade.ReinforcedArmor))
                damage *= 0.75f;

            if (IsBuffedTeam(out var buffRatio))
                damage /= buffRatio;
            
            Health -= damage;
            LastDamage = Utils.TimeStamp;
            Logger.Info($"{pc.GetRealName()}'s health (after damage): {Math.Round(Health, 2)}", "BedWars");

            if (Health <= 0 && pc != null && pc.IsAlive())
            {
                if (killer != null) killer.KillFlash();
                if (Main.GM.Value && AmongUsClient.Instance.AmHost) PlayerControl.LocalPlayer.KillFlash();
                ChatCommands.Spectators.ToValidPlayers().Do(x => x.KillFlash());
                
                if (!AllNetObjects.TryGetValue(Team, out NetObjectCollection netObjectCollection) || !netObjectCollection.Bed.IsBroken)
                {
                    if (killer != null && Data.TryGetValue(killer.PlayerId, out PlayerData killerData))
                        Inventory.Items.DoIf(x => ItemCategories[x.Key] != ItemCategory.Tool, x => killerData.Inventory.Adjust(x.Key, x.Value));

                    Inventory.Clear();
                    if (Reviving.Add(pc.PlayerId)) Main.Instance.StartCoroutine(ReviveCountdown(pc));

                    pc.ExileTemporarily();
                }
                else
                {
                    Inventory.Items = [];
                    pc.Suicide(PlayerState.DeathReason.Kill);
                }
            }
        }

        private IEnumerator ReviveCountdown(PlayerControl pc)
        {
            int time = ReviveTime;
            if (IsBuffedTeam(out var buffRatio)) time = (int)(time / buffRatio);

            while (time > 0)
            {
                if (pc == null) yield break;

                pc.Notify(string.Format(Translator.GetString("Bedwars.ReviveCountdown"), time), overrideAll: true, sendOption: SendOption.None);
                yield return new WaitForSeconds(1f);
                time--;
            }

            Health = MaxHealth;
            NameNotifyManager.Notifies.Remove(pc.PlayerId);
            RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskComplete);
            pc.ReviveFromTemporaryExile();
            pc.RpcSetColor(Team.GetColorId());
            pc.TP(Base.SpawnPosition);
            pc.SetChatVisible(true);
            Utils.NotifyRoles(SpecifyTarget: pc, SendOption: SendOption.None);

            Reviving.Remove(pc.PlayerId);
        }

        public string BuildSuffix(PlayerControl pc)
        {
            var sb = new StringBuilder();

            if (NameNotifyManager.GetNameNotify(pc, out string notify) && notify.Length > 0)
                sb.AppendLine(notify);

            if (!pc.IsAlive()) return $"<#ffffff>{sb.ToString().Trim()}</color>";

            sb.AppendLine(IsGracePeriod ? Translator.GetString("Bedwars.GracePeriod") : GetHealthInfo());

            var topLines = 12;
            
            if (InShop.TryGetValue(pc.PlayerId, out Shop shop))
            {
                sb.AppendLine();
                sb.AppendLine(shop.GetSuffix(pc));
                topLines += 4;
            }

            int lines = sb.ToString().Count(x => x == '\n');
            sb.Insert(0, Utils.ColorString(Color.clear, ".") + new string('\n', Math.Max(0, topLines - lines)));
            sb.Append(new string('\n', Math.Max(0, 5 - lines)));

            sb.Append(GetArmorInfo());
            sb.Append(" - ");
            sb.AppendLine(GetSwordInfo());

            sb.Append(Inventory);

            return $"<#ffffff>{sb.ToString().Trim()}</color>";
        }

        public string GetInfoAsTarget(PlayerControl pc)
        {
            if (!pc.IsAlive()) return string.Empty;

            var sb = new StringBuilder();

            if (GracePeriodEnd <= Utils.TimeStamp)
            {
                sb.Append(GetHealthInfo());
                sb.Append(' ');
                sb.Append(' ');
                sb.Append(' ');
                sb.Append(' ');
                sb.Append(GetArmorInfo());
                sb.Append(' ');
                sb.Append(' ');
                sb.Append(GetSwordInfo());

                if (Inventory.Items.ContainsKey(Item.TNT) && ItemDisplayData.TryGetValue(Item.TNT, out ItemDisplay display))
                    sb.Append(Utils.ColorString(display.Color, $"    {display.Icon} {display.Name.Invoke()} {display.Icon}"));
            }

            return $"<#ffffff>{sb.ToString().Trim()}</color>";
        }

        private string GetArmorInfo()
        {
            return $"<size=80%>{Translator.GetString("BedWars.Suffix.Armor")} {(Armor.HasValue && ItemDisplayData.TryGetValue(Armor.Value, out ItemDisplay display) ? Utils.ColorString(display.Color, display.Icon.ToString()) : Translator.GetString("None"))}</size>";
        }

        private string GetSwordInfo()
        {
            return $"<size=80%>{Translator.GetString("BedWars.Suffix.Sword")} {(Sword.HasValue && ItemDisplayData.TryGetValue(Sword.Value, out ItemDisplay display) ? Utils.ColorString(display.Color, display.Icon.ToString()) : Translator.GetString("None"))}</size>";
        }

        private string GetHealthInfo()
        {
            return Utils.ColorString(GetHealthColor(), $"{Math.Round(Health, 1)} ♥");
        }

        private Color GetHealthColor()
        {
            float t = Mathf.Clamp01(Health / MaxHealth);

            if (t < 0.5f)
            {
                float lerpT = t / 0.5f;
                return new Color(1f, lerpT, 0f);
            }
            else
            {
                float lerpT = (t - 0.5f) / 0.5f;
                return new Color(1f - lerpT, 1f, lerpT);
            }
        }

        public bool IsBuffedTeam(out float buffRatio)
        {
            int maxTeamSize = Data.Values.GroupBy(x => x.Team).Max(x => x.Count());
            int myTeamSize = Data.Values.Count(x => x.Team == Team);
            buffRatio = (float)maxTeamSize / myTeamSize; // e.g. 4/2 = 2, 4/3 = 1.33, 4/4 = 1
            return maxTeamSize > myTeamSize;
        }
    }

    private readonly record struct NetObjectCollection(Bed Bed, ItemShop ItemShop, UpgradeShop UpgradeShop);

    private readonly record struct Base(Vector2 BedPosition, SystemTypes Room, Vector2 ItemShopPosition, Vector2 UpgradeShopPosition, Vector2 SpawnPosition);

    private static readonly Dictionary<MapNames, Dictionary<BedWarsTeam, Base>> Bases = new()
    {
        [MapNames.Skeld] = new()
        {
            [BedWarsTeam.Blue] = new(new(-20.29f, -5.31f), SystemTypes.Reactor, new(-21.61f, -8.2f), new(-21.47f, -2.24f), new(-22.56f, -2.91f)),
            [BedWarsTeam.Yellow] = new(new(-9.04f, -1.79f), SystemTypes.MedBay, new(-7.68f, -3.7f), new(-7.68f, -5.18f), new(-5.67f, -5.31f)),
            [BedWarsTeam.Red] = new(new(5.08f, -14.71f), SystemTypes.Comms, new(2.72f, -14.76f), new(2.72f, -16.37f), new(5.63f, -16.71f)),
            [BedWarsTeam.Green] = new(new(14.54f, -4.61f), SystemTypes.Nav, new(16.57f, -3.21f), new(16.57f, -6.12f), new(17.57f, -4.07f))
        },
        [MapNames.MiraHQ] = new()
        {
            [BedWarsTeam.Blue] = new(new(2.81f, -1.43f), SystemTypes.Launchpad, new(-4.46f, 0.48f), new(-4.46f, 4.12f), new(-4.45f, 2.4f)),
            [BedWarsTeam.Yellow] = new(new(22.37f, 1.5f), SystemTypes.Balcony, new(20.02f, -1.81f), new(27.62f, -1.81f), new(23.77f, -1.58f)),
            [BedWarsTeam.Red] = new(new(17.8f, 23.17f), SystemTypes.Greenhouse, new(13.58f, 22.4f), new(22.08f, 22.37f), new(17.85f, 25.55f)),
            [BedWarsTeam.Green] = new(new(6.12f, 12.12f), SystemTypes.Reactor, new(1.69f, 10.63f), new(8.88f, 12.38f), new(2.47f, 11.9f))
        },
        [MapNames.Polus] = new()
        {
            [BedWarsTeam.Blue] = new(new(3.32f, -21.65f), SystemTypes.LifeSupp, new(0.96f, -23.57f), new(3.57f, -24.45f), new(0.89f, -16.03f)),
            [BedWarsTeam.Yellow] = new(new(21.21f, -23f), SystemTypes.Admin, new(21.78f, -25.26f), new(20.74f, -21.53f), new(24.94f, -20.67f)),
            [BedWarsTeam.Red] = new(new(31.43f, -7.53f), SystemTypes.Laboratory, new(33.54f, -5.84f), new(35.93f, -6.05f), new(40.41f, -6.73f)),
            [BedWarsTeam.Green] = new(new(16.72f, -3.16f), SystemTypes.Dropship, new(14.94f, -0.9f), new(14.94f, -2.67f), new(16.69f, -0.6f))
        },
        [MapNames.Airship] = new()
        {
            [BedWarsTeam.Blue] = new(new(-17.47f, -1.09f), SystemTypes.Cockpit, new(-19.86f, 0.78f), new(-22.49f, -0.1f), new(-22.1f, -1.15f)),
            [BedWarsTeam.Yellow] = new(new(6.04f, -10.45f), SystemTypes.Security, new(8.59f, -10.85f), new(8.4f, -12.47f), new(10.29f, -16.15f)),
            [BedWarsTeam.Red] = new(new(33.7f, -0.88f), SystemTypes.CargoBay, new(38.41f, 1.29f), new(38.41f, -0.69f), new(37.37f, -3.46f)),
            [BedWarsTeam.Green] = new(new(4.54f, 15.61f), SystemTypes.MeetingRoom, new(12f, 9.11f), new(12.66f, 6.29f), new(16.11f, 15.26f))
        },
        [MapNames.Fungle] = new()
        {
            [BedWarsTeam.Blue] = new(new(15.18f, -11.91f), SystemTypes.Jungle, new(11.07f, -14f), new(11.07f, -16.18f), new(15.15f, -16.06f)),
            [BedWarsTeam.Yellow] = new(new(19.83f, 11.07f), SystemTypes.Comms, new(20.19f, 13.63f), new(22.89f, 13.32f), new(24.24f, 14.41f)),
            [BedWarsTeam.Red] = new(new(-7.39f, 8.81f), SystemTypes.Dropship, new(-9.86f, 13.43f), new(-7.47f, 10.92f), new(-11.21f, 12.5f)),
            [BedWarsTeam.Green] = new(new(-15.55f, -7.04f), SystemTypes.Kitchen, new(-17.22f, -9.32f), new(-13.78f, -9.32f), new(-22.83f, -7.19f))
        },
        [(MapNames)6] = new()
        {
            [BedWarsTeam.Blue] = new(new(-12.69f, -27.88f), SystemTypes.Engine, new(-14.67f, -34.71f), new(-12f, -34.71f), new(-14.6f, -30.96f)),
            [BedWarsTeam.Yellow] = new(new(9.84f, -27.66f), SystemTypes.Electrical, new(10.22f, -31.58f), new(12.76f, -32.24f), new(12.29f, -29.15f)),
            [BedWarsTeam.Red] = new(new(-8.36f, -40.06f), (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Ballast, new(-8.25f, -43f), new(-10.7f, -40.36f), new(-9.44f, -41.55f)),
            [BedWarsTeam.Green] = new(new(5.71f, -38.68f), (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.LowerLobby, new(4.59f, -39.83f), new(6.46f, -39.83f), new(8.79f, -38.3f))
        }
    };

    private static readonly Dictionary<MapNames, Dictionary<Item, List<Vector2>>> ItemGeneratorPositions = new()
    {
        [MapNames.Skeld] = new()
        {
            [Item.Iron] = [new(3.69f, -15.7f), new(17.91f, -5.06f), new(-20f, -3.5f), new(-10.61f, -3.81f)],
            [Item.Gold] = [new(3.69f, -16f), new(17.91f, -5.48f), new(-20f, -4f), new(-10.61f, -4.33f)],
            [Item.Emerald] = [new(4.53f, -9.67f)],
            [Item.Diamond] = [new(9.55f, -9.41f), new(-15.29f, 2.88f), new(1.61f, 5.38f), new(-9.78f, -7.68f)]
        },
        [MapNames.MiraHQ] = new()
        {
            [Item.Iron] = [new(18.15f, 24.21f), new(28f, 0.62f), new(-5.51f, -2f), new(6.23f, 14.35f)],
            [Item.Gold] = [new(17.52f, 24.21f), new(28f, 0f), new(-5.51f, -1.48f), new(6.23f, 13.82f)],
            [Item.Emerald] = [new(12.53f, 6.91f)],
            [Item.Diamond] = [new(15.41f, -1.46f), new(17.8f, 11.46f)]
        },
        [MapNames.Polus] = new()
        {
            [Item.Iron] = [new(1.44f, -19.91f), new(20.09f, -25.15f), new(29.83f, -7.25f), new(18.54f, -1.68f)],
            [Item.Gold] = [new(1.44f, -20.33f), new(20.09f, -24.74f), new(29.83f, -7.77f), new(18.54f, -2.2f)],
            [Item.Emerald] = [new(11.73f, -15.87f), new(20.64f, -12.2f)],
            [Item.Diamond] = [new(35.54f, -21.57f), new(23.91f, -6.82f), new(7.27f, -12.07f), new(12.67f, -23.38f)]
        },
        [MapNames.Airship] = new()
        {
            [Item.Iron] = [new(-21.58f, -3.44f), new(5.61f, -14.36f), new(28.95f, -1.25f), new(10.72f, 14.23f)],
            [Item.Gold] = [new(-21f, -3.44f), new(6.23f, -14.36f), new(28.95f, -1.77f), new(11.34f, 14.23f)],
            [Item.Emerald] = [new(19.04f, 0.07f), new(19.04f, 0.67f)],
            [Item.Diamond] = [new(-13.6f, -14.38f), new(19.37f, -3.95f), new(19.86f, 11.76f), new(-0.8f, -2.55f)]
        },
        [MapNames.Fungle] = new()
        {
            [Item.Iron] = [new(17.63f, -12.48f), new(25.22f, 11.33f), new(-8.63f, 10.33f), new(-13.67f, -7.08f)],
            [Item.Gold] = [new(18.19f, -12.48f), new(24.6f, 11.33f), new(-8.63f, 9.81f), new(-13.67f, -7.6f)],
            [Item.Emerald] = [new(9.37f, 0.99f), new(-3.15f, -10.55f), new(2f, -1.57f)],
            [Item.Diamond] = [new(22.28f, 3.18f), new(2.86f, 1.28f), new(-17.61f, 2.65f), new(-4.56f, -14.77f)]
        },
        [(MapNames)6] = new()
        {
            [Item.Iron] = [new(-11.39f, -30.8f), new(13f, -25.27f), new(-11.41f, -38.57f), new(8.79f, -40.54f)],
            [Item.Gold] = [new(-11.39f, -31.43f), new(13f, -25.9f), new(-11.41f, -39.13f), new(8.79f, -40f)],
            [Item.Emerald] = [new(-3.09f, -40.54f), new(-1f, -29.11f)],
            [Item.Diamond] = [new(-9.34f, -33.79f), new(-7.07f, -33.79f), new(2.79f, -32f), new(2.79f, -31.37f)]
        }
    };

    static BedWars()
    {
        Bases[MapNames.Dleks] = Bases[MapNames.Skeld].Select(x => (key: x.Key, value: new Base(new(-x.Value.BedPosition.x, x.Value.BedPosition.y), x.Value.Room, new(-x.Value.ItemShopPosition.x, x.Value.ItemShopPosition.y), new(-x.Value.UpgradeShopPosition.x, x.Value.UpgradeShopPosition.y), new(-x.Value.SpawnPosition.x, x.Value.SpawnPosition.y)))).ToDictionary(x => x.key, x => x.value);
        ItemGeneratorPositions[MapNames.Dleks] = ItemGeneratorPositions[MapNames.Skeld].ToDictionary(x => x.Key, x => x.Value.ConvertAll(p => new Vector2(-p.x, p.y)));
    }

    private enum BedWarsTeam
    {
        Blue,
        Yellow,
        Green,
        Red
    }

    private static Color GetColor(this BedWarsTeam team)
    {
        return team switch
        {
            BedWarsTeam.Blue => Color.cyan,
            BedWarsTeam.Yellow => Color.yellow,
            BedWarsTeam.Green => Color.green,
            BedWarsTeam.Red => Color.red,
            _ => Color.white
        };
    }

    private static byte GetColorId(this BedWarsTeam team)
    {
        return team switch
        {
            BedWarsTeam.Red => 0,
            BedWarsTeam.Yellow => 5,
            BedWarsTeam.Blue => 10,
            BedWarsTeam.Green => 11,
            _ => 7
        };
    }

    private static string GetName(this BedWarsTeam team)
    {
        return team switch
        {
            BedWarsTeam.Blue => Translator.GetString("Bedwars.BlueTeam"),
            BedWarsTeam.Yellow => Translator.GetString("Bedwars.YellowTeam"),
            BedWarsTeam.Green => Translator.GetString("Bedwars.GreenTeam"),
            BedWarsTeam.Red => Translator.GetString("Bedwars.RedTeam"),
            _ => string.Empty
        };
    }

    private abstract class Shop
    {
        public abstract CustomNetObject NetObject { get; }
        protected Dictionary<byte, int> SelectionIndex { get; } = [];

        public abstract void NextSelection(PlayerControl pc);
        public abstract void Purchase(PlayerControl pc);

        public virtual void EnterShop(PlayerControl pc)
        {
            if (pc == null || !pc.IsAlive()) return;
            RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskUpdateSound);
            SelectionIndex.TryAdd(pc.PlayerId, 0);
        }

        public virtual void ExitShop(PlayerControl pc) { }

        public abstract string GetSuffix(PlayerControl pc);
    }

    private sealed class ItemShop(BedWarsShop netObject) : Shop
    {
        public override CustomNetObject NetObject { get; } = netObject;
        private Item[] Selections { get; } = Enum.GetValues<Item>();
        private ItemCategory[] Categories { get; } = Enum.GetValues<ItemCategory>()[1..];
        private Dictionary<byte, int> CategoryIndex { get; } = [];
        private Dictionary<byte, ItemCategory> Category { get; } = [];

        public override void NextSelection(PlayerControl pc)
        {
            if (pc == null || !pc.IsAlive()) return;

            if (!Category.TryGetValue(pc.PlayerId, out ItemCategory category))
            {
                NextCategory(pc);
                return;
            }

            SelectionIndex.TryAdd(pc.PlayerId, 0);
            SelectionIndex[pc.PlayerId]++;

            if (SelectionIndex[pc.PlayerId] >= Selections.Count(x => ItemCategories[x] == category))
                SelectionIndex[pc.PlayerId] = 0;
        }

        public override void Purchase(PlayerControl pc)
        {
            if (pc == null || !pc.IsAlive()) return;

            if (!Category.TryGetValue(pc.PlayerId, out ItemCategory category))
            {
                EnterCategory(pc);
                return;
            }

            Item[] selections = Selections.Where(x => ItemCategories[x] == category).ToArray();
            if (!SelectionIndex.TryGetValue(pc.PlayerId, out int index) || index < 0 || index >= selections.Length) return;

            Item selectedItem = selections[index];
            if (!ItemCost.TryGetValue(selectedItem, out (Item Resource, int Count) cost)) return;

            if (Data.TryGetValue(pc.PlayerId, out PlayerData data) && data.Inventory.Items.TryGetValue(cost.Resource, out int res) && res >= cost.Count)
            {
                ItemCategory itemCategory = ItemCategories[selectedItem];

                switch (itemCategory)
                {
                    case ItemCategory.Armor when !data.Armor.HasValue || data.Armor.Value < selectedItem:
                        data.Armor = selectedItem;
                        Utils.NotifyRoles(SpecifyTarget: pc);
                        break;
                    case ItemCategory.Weapon when !data.Sword.HasValue || data.Sword.Value < selectedItem:
                        data.Sword = selectedItem;
                        Utils.NotifyRoles(SpecifyTarget: pc);
                        break;
                    case ItemCategory.Utility when selectedItem == Item.TNT:
                        LateTask.New(() => Utils.NotifyRoles(SpecifyTarget: pc), 0.2f, log: false);
                        break;
                }

                if (itemCategory is ItemCategory.Armor or ItemCategory.Weapon || data.Inventory.Adjust(selectedItem))
                    data.Inventory.Adjust(cost.Resource, -cost.Count);

                RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskComplete);
                Logger.Info($"{pc.GetRealName()} purchased {selectedItem} for {cost.Count} {cost.Resource}", "BedWars");
            }
        }

        private void NextCategory(PlayerControl pc)
        {
            if (pc == null || !pc.IsAlive()) return;

            CategoryIndex.TryAdd(pc.PlayerId, 0);
            CategoryIndex[pc.PlayerId]++;

            if (CategoryIndex[pc.PlayerId] >= Categories.Length)
                CategoryIndex[pc.PlayerId] = 0;
        }

        private void EnterCategory(PlayerControl pc)
        {
            if (pc == null || !pc.IsAlive()) return;
            RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskUpdateSound);
            CategoryIndex.TryAdd(pc.PlayerId, 0);
            SelectionIndex.TryAdd(pc.PlayerId, 0);
            Category[pc.PlayerId] = Categories[CategoryIndex[pc.PlayerId]];
        }

        public override void EnterShop(PlayerControl pc)
        {
            if (pc == null || !pc.IsAlive()) return;
            RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskUpdateSound);
            SelectionIndex.TryAdd(pc.PlayerId, 0);
            CategoryIndex.TryAdd(pc.PlayerId, 0);
        }

        public override void ExitShop(PlayerControl pc)
        {
            if (pc == null || !pc.IsAlive()) return;
            CategoryIndex.Remove(pc.PlayerId);
            Category.Remove(pc.PlayerId);
        }

        public override string GetSuffix(PlayerControl pc)
        {
            if (!Category.TryGetValue(pc.PlayerId, out ItemCategory category))
            {
                if (!CategoryIndex.TryGetValue(pc.PlayerId, out int index) || index < 0 || index >= Categories.Length) return string.Empty;
                ItemCategory itemCategory = Categories[index];
                List<string> itemsInCategory = [];

                foreach (Item item in Selections)
                {
                    if (ItemCategories[item] == itemCategory && ItemDisplayData.TryGetValue(item, out ItemDisplay display))
                        itemsInCategory.Add($"{display.Name.Invoke()} {Utils.ColorString(display.Color, display.Icon.ToString())}");
                }

                return string.Format(Translator.GetString("Bedwars.Shop.ItemShopSuffix.CategorySelection"), Translator.GetString($"BedWars.ItemCategory.{itemCategory}"), string.Join(' ', itemsInCategory));
            }
            else
            {
                Item[] selections = Selections.Where(x => ItemCategories[x] == category).ToArray();
                if (!SelectionIndex.TryGetValue(pc.PlayerId, out int index) || index < 0 || index >= selections.Length) return string.Empty;

                Item selectedItem = selections[index];
                if (!ItemDisplayData.TryGetValue(selectedItem, out ItemDisplay display)) return string.Empty;
                string itemName = Utils.ColorString(display.Color, display.Name.Invoke());
                string itemIcon = Utils.ColorString(display.Color, display.Icon.ToString());
                string itemDescription = Translator.GetString($"BedWars.ItemDescription.{selectedItem}");
                if (!ItemCost.TryGetValue(selectedItem, out (Item Resource, int Count) cost) || !ItemDisplayData.TryGetValue(cost.Resource, out ItemDisplay costDisplay)) return string.Empty;
                var costString = $"{cost.Count} {Utils.ColorString(costDisplay.Color, costDisplay.Icon.ToString())}";

                return string.Format(Translator.GetString("Bedwars.Shop.ItemShopSuffix.ItemSelection"), itemName, itemIcon, itemDescription, costString);
            }
        }
    }

    private sealed class UpgradeShop(BedWarsShop netObject) : Shop
    {
        public override CustomNetObject NetObject { get; } = netObject;
        private Upgrade[] Selections { get; } = Enum.GetValues<Upgrade>();

        public override void NextSelection(PlayerControl pc)
        {
            if (pc == null || !pc.IsAlive()) return;

            SelectionIndex.TryAdd(pc.PlayerId, 0);
            SelectionIndex[pc.PlayerId]++;

            if (SelectionIndex[pc.PlayerId] >= Selections.Length)
                SelectionIndex[pc.PlayerId] = 0;
        }

        public override void Purchase(PlayerControl pc)
        {
            if (pc == null || !pc.IsAlive()) return;
            if (!SelectionIndex.TryGetValue(pc.PlayerId, out int index) || index < 0 || index >= Selections.Length) return;

            Upgrade selectedUpgrade = Selections[index];
            if (!UpgradeCost.TryGetValue(selectedUpgrade, out int cost)) return;

            if (Data.TryGetValue(pc.PlayerId, out PlayerData data) && data.Inventory.Items.TryGetValue(Item.Diamond, out int res) && res >= cost)
            {
                if (!Upgrades.TryGetValue(data.Team, out HashSet<Upgrade> upgrades)) Upgrades[data.Team] = upgrades = [];
                if (upgrades.Add(selectedUpgrade)) data.Inventory.Adjust(Item.Diamond, -cost);

                RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskComplete);
                Logger.Info($"{pc.GetRealName()} purchased {selectedUpgrade} for {cost} {Item.Diamond}", "BedWars");
            }
        }

        public override string GetSuffix(PlayerControl pc)
        {
            if (!SelectionIndex.TryGetValue(pc.PlayerId, out int index) || index < 0 || index >= Selections.Length) return string.Empty;

            Upgrade selectedUpgrade = Selections[index];
            if (!UpgradeCost.TryGetValue(selectedUpgrade, out int cost) || !ItemDisplayData.TryGetValue(Item.Diamond, out ItemDisplay display)) return string.Empty;
            var costString = $"{cost} {Utils.ColorString(display.Color, display.Icon.ToString())}";
            if (!Data.TryGetValue(pc.PlayerId, out PlayerData data)) return string.Empty;
            string alreadyBought = Upgrades.TryGetValue(data.Team, out HashSet<Upgrade> upgrades) && upgrades.Contains(selectedUpgrade)
                ? "\n" + Translator.GetString("BedWars.Upgrade.AlreadyBought")
                : string.Empty;

            return string.Format(Translator.GetString("Bedwars.Shop.UpgradeShopSuffix"), Translator.GetString($"BedWars.Upgrade.{selectedUpgrade}"), Translator.GetString($"BedWars.UpgradeDescription.{selectedUpgrade}"), alreadyBought, costString);
        }
    }

    private enum Upgrade
    {
        ReinforcedArmor,
        Sharpness,
        Haste,
        Forge,
        Trap,
        HealPool
    }

    private static readonly Dictionary<Item, (Item Resource, int Count)> ItemCost = new()
    {
        [Item.Wool] = (Item.Iron, 1),
        [Item.EndStone] = (Item.Iron, 4),
        [Item.Wood] = (Item.Gold, 2),
        [Item.Glass] = (Item.Iron, 12),
        [Item.Obsidian] = (Item.Emerald, 2),

        [Item.WoodenSword] = (Item.Iron, 1),
        [Item.IronSword] = (Item.Gold, 3),
        [Item.DiamondSword] = (Item.Emerald, 2),

        [Item.IronArmor] = (Item.Gold, 6),
        [Item.DiamondArmor] = (Item.Emerald, 4),

        [Item.Pickaxe] = (Item.Gold, 12),
        [Item.Axe] = (Item.Gold, 1),
        [Item.Shears] = (Item.Iron, 6),

        [Item.GoldenApple] = (Item.Gold, 4),
        [Item.TNT] = (Item.Gold, 8),
        [Item.EnderPearl] = (Item.Emerald, 1),
        [Item.InvisibilityPotion] = (Item.Emerald, 1),
        [Item.SpeedPotion] = (Item.Emerald, 1)
    };

    private static readonly Dictionary<Upgrade, int> UpgradeCost = new()
    {
        [Upgrade.ReinforcedArmor] = 2,
        [Upgrade.Sharpness] = 4,
        [Upgrade.Haste] = 3,
        [Upgrade.Forge] = 1,
        [Upgrade.Trap] = 5,
        [Upgrade.HealPool] = 1
    };

    private abstract class ItemGenerator
    {
        protected abstract BedWarsItemGenerator NetObject { get; }
        protected abstract int GenerationInterval { get; }
        protected abstract Item GeneratedItem { get; }

        private long LastGenerationTime = Utils.TimeStamp;
        private int Generated;

        public void Update()
        {
            int oldNum = Generated;
            long now = Utils.TimeStamp;

            bool onBase = GeneratedItem is Item.Iron or Item.Gold;

            if ((GracePeriodEnd <= now || onBase) && LastGenerationTime + GenerationInterval <= now)
            {
                LastGenerationTime = now;
                Generated++;

                if (onBase)
                {
                    BedWarsTeam team = Enum.GetValues<BedWarsTeam>().MinBy(x => Vector2.Distance(Bases[Main.CurrentMap][x].BedPosition, NetObject.Position));
                    if (Upgrades.TryGetValue(team, out HashSet<Upgrade> upgrades) && upgrades.Contains(Upgrade.Forge)) Generated++;
                }
            }

            PlayerControl[] playersInRadius = Utils.GetPlayersInRadius(ShopAndItemGeneratorRange, NetObject.Position).ToArray();

            foreach (PlayerControl pc in playersInRadius)
            {
                if (Data.TryGetValue(pc.PlayerId, out PlayerData data))
                    data.Inventory.Adjust(GeneratedItem, Generated);
            }

            if (playersInRadius.Length > 0)
                Generated = 0;

            if (oldNum != Generated) NetObject.SetCount(Generated);
        }
    }

    private sealed class IronGenerator(BedWarsItemGenerator netObject) : ItemGenerator
    {
        protected override BedWarsItemGenerator NetObject { get; } = netObject;
        protected override int GenerationInterval { get; } = IronGenerationInterval;
        protected override Item GeneratedItem => Item.Iron;
    }

    private sealed class GoldGenerator(BedWarsItemGenerator netObject) : ItemGenerator
    {
        protected override BedWarsItemGenerator NetObject { get; } = netObject;
        protected override int GenerationInterval { get; } = GoldGenerationInterval;
        protected override Item GeneratedItem => Item.Gold;
    }

    private sealed class EmeraldGenerator(BedWarsItemGenerator netObject) : ItemGenerator
    {
        protected override BedWarsItemGenerator NetObject { get; } = netObject;
        protected override int GenerationInterval { get; } = EmeraldGenerationInterval;
        protected override Item GeneratedItem => Item.Emerald;
    }

    private sealed class DiamondGenerator(BedWarsItemGenerator netObject) : ItemGenerator
    {
        protected override BedWarsItemGenerator NetObject { get; } = netObject;
        protected override int GenerationInterval { get; } = DiamondGenerationInterval;
        protected override Item GeneratedItem => Item.Diamond;
    }

    public enum Item // Item count: 4+5+3+2+3+5 = 22
    {
        // Resources (4)
        Iron,
        Gold,
        Emerald,
        Diamond,

        // Blocks (5)
        Wool,
        EndStone,
        Wood,
        Glass,
        Obsidian,

        // Weapons (3)
        WoodenSword,
        IronSword,
        DiamondSword,

        // Armor (2)
        IronArmor,
        DiamondArmor,

        // Tools (3)
        Pickaxe,
        Axe,
        Shears,

        // Utility (5)
        GoldenApple,
        TNT,
        EnderPearl,
        InvisibilityPotion,
        SpeedPotion
    }

    private enum ItemCategory
    {
        Resource,
        Block,
        Weapon,
        Armor,
        Tool,
        Utility
    }

    private static readonly Dictionary<Item, ItemCategory> ItemCategories = new()
    {
        [Item.Iron] = ItemCategory.Resource,
        [Item.Gold] = ItemCategory.Resource,
        [Item.Emerald] = ItemCategory.Resource,
        [Item.Diamond] = ItemCategory.Resource,

        [Item.Wool] = ItemCategory.Block,
        [Item.EndStone] = ItemCategory.Block,
        [Item.Wood] = ItemCategory.Block,
        [Item.Glass] = ItemCategory.Block,
        [Item.Obsidian] = ItemCategory.Block,

        [Item.WoodenSword] = ItemCategory.Weapon,
        [Item.IronSword] = ItemCategory.Weapon,
        [Item.DiamondSword] = ItemCategory.Weapon,

        [Item.IronArmor] = ItemCategory.Armor,
        [Item.DiamondArmor] = ItemCategory.Armor,

        [Item.Pickaxe] = ItemCategory.Tool,
        [Item.Axe] = ItemCategory.Tool,
        [Item.Shears] = ItemCategory.Tool,

        [Item.GoldenApple] = ItemCategory.Utility,
        [Item.TNT] = ItemCategory.Utility,
        [Item.EnderPearl] = ItemCategory.Utility,
        [Item.InvisibilityPotion] = ItemCategory.Utility,
        [Item.SpeedPotion] = ItemCategory.Utility
    };

    private readonly record struct ItemDisplay(Color Color, char Icon, Func<string> Name);

    private static readonly Dictionary<Item, ItemDisplay> ItemDisplayData = new()
    {
        [Item.Iron] = new(Color.white, '▲', () => Translator.GetString("BedWars.Item.Iron")),
        [Item.Gold] = new(Color.yellow, '●', () => Translator.GetString("BedWars.Item.Gold")),
        [Item.Emerald] = new(new Color32(23, 133, 48, 255), '◈', () => Translator.GetString("BedWars.Item.Emerald")),
        [Item.Diamond] = new(Color.cyan, '◆', () => Translator.GetString("BedWars.Item.Diamond")),

        [Item.Wool] = new(Palette.White_75Alpha, '░', () => Translator.GetString("BedWars.Item.Wool")),
        [Item.EndStone] = new(new Color32(224, 230, 181, 255), '▩', () => Translator.GetString("BedWars.Item.EndStone")),
        [Item.Wood] = new(new Color32(179, 132, 86, 255), '▤', () => Translator.GetString("BedWars.Item.Wood")),
        [Item.Glass] = new(Palette.HalfWhite, '▢', () => Translator.GetString("BedWars.Item.Glass")),
        [Item.Obsidian] = new(new Color32(15, 0, 48, 255), '▇', () => Translator.GetString("BedWars.Item.Obsidian")),

        [Item.WoodenSword] = new(Palette.Brown, '†', () => Translator.GetString("BedWars.Item.WoodenSword")),
        [Item.IronSword] = new(Color.white, '✚', () => Translator.GetString("BedWars.Item.IronSword")),
        [Item.DiamondSword] = new(Color.cyan, '✽', () => Translator.GetString("BedWars.Item.DiamondSword")),

        [Item.IronArmor] = new(Color.white, '◎', () => Translator.GetString("BedWars.Item.IronArmor")),
        [Item.DiamondArmor] = new(Color.cyan, '⊗', () => Translator.GetString("BedWars.Item.DiamondArmor")),

        [Item.Pickaxe] = new(new Color32(85, 92, 133, 255), '┯', () => Translator.GetString("BedWars.Item.Pickaxe")),
        [Item.Axe] = new(new Color32(108, 112, 77, 255), '┭', () => Translator.GetString("BedWars.Item.Axe")),
        [Item.Shears] = new(Color.gray, '✂', () => Translator.GetString("BedWars.Item.Shears")),

        [Item.GoldenApple] = new(Color.yellow, '☀', () => Translator.GetString("BedWars.Item.GoldenApple")),
        [Item.TNT] = new(Color.red, '♨', () => Translator.GetString("BedWars.Item.TNT")),
        [Item.EnderPearl] = new(new Color32(37, 74, 57, 255), '⦿', () => Translator.GetString("BedWars.Item.EnderPearl")),
        [Item.InvisibilityPotion] = new(Color.magenta, '◌', () => Translator.GetString("BedWars.Item.InvisibilityPotion")),
        [Item.SpeedPotion] = new(Color.blue, '»', () => Translator.GetString("BedWars.Item.SpeedPotion"))
    };

    private readonly record struct BreakDifficulty(int Default, int WithPickaxe, int WithAxe, int WithShears);

    private static readonly Dictionary<Item, BreakDifficulty> BreakDifficulties = new()
    {
        [Item.Wool] = new(2, 2, 2, 1),
        [Item.EndStone] = new(6, 2, 6, 6),
        [Item.Wood] = new(4, 4, 1, 4),
        [Item.Glass] = new(1, 1, 1, 1),
        [Item.Obsidian] = new(60, 8, 60, 60)
    };

    private static int GetBreakDifficulty(Item item, Item? tool)
    {
        if (BreakDifficulties.TryGetValue(item, out BreakDifficulty difficulty))
        {
            return tool.HasValue
                ? tool.Value switch
                {
                    Item.Pickaxe => difficulty.WithPickaxe,
                    Item.Axe => difficulty.WithAxe,
                    Item.Shears => difficulty.WithShears,
                    _ => difficulty.Default
                }
                : difficulty.Default;
        }

        return 0;
    }

    private class Inventory
    {
        public Dictionary<Item, int> Items = [];
        public int SelectedSlot;

        public void Clear()
        {
            Items = Items.Where(x => ItemCategories[x.Key] == ItemCategory.Tool).ToDictionary(x => x.Key, x => x.Value);
        }

        public void NextSlot()
        {
            SelectedSlot++;
            if (SelectedSlot >= InventorySlots) SelectedSlot = 0;
        }

        public bool Adjust(Item item, int count = 1)
        {
            if (Items.ContainsKey(item))
                Items[item] += count;
            else
            {
                if (Items.Count >= InventorySlots) return false;
                Items[item] = count;
            }

            if (Items[item] <= 0) Items.Remove(item);
            return true;
        }

        public Item? GetSelectedItem()
        {
            if (SelectedSlot < 0 || SelectedSlot >= Items.Count) return null;
            return Items.ElementAt(SelectedSlot).Key;
        }

        public void UseSelectedItem(PlayerControl pc)
        {
            if (pc == null || !pc.IsAlive() || !Data.TryGetValue(pc.PlayerId, out PlayerData data)) return;

            Vector2 pos = pc.Pos();
            bool nextToBed = AllNetObjects.FindFirst(x => !x.Value.Bed.IsBroken && Vector2.Distance(x.Value.Bed.Position, pos) <= BedBreakAndProtectRange, out KeyValuePair<BedWarsTeam, NetObjectCollection> bed);

            if (SelectedSlot < 0 || SelectedSlot >= Items.Count)
            {
                if (nextToBed && (bed.Key != data.Team || bed.Value.Bed.Layers.Count > 0))
                    bed.Value.Bed.TryBreak(pc);
                
                return;
            }
            
            KeyValuePair<Item, int> selected = Items.ElementAt(SelectedSlot);

            Logger.Info($"Use selected item {selected.Key} with count {selected.Value} at position {pos} by player {pc.GetRealName()} (next to bed: {nextToBed})", "BedWars");

            switch (ItemCategories[selected.Key])
            {
                case ItemCategory.Block when nextToBed && bed.Key == data.Team:
                    int req = bed.Value.Bed.GetNextProtectReq();

                    if (selected.Value >= req)
                    {
                        RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskUpdateSound);
                        data.Inventory.Adjust(selected.Key, -req);
                        bed.Value.Bed.Layers.Add(selected.Key);
                        bed.Value.Bed.UpdateStatus();
                    }

                    break;
                case ItemCategory.Utility:
                    switch (selected.Key)
                    {
                        case Item.GoldenApple:
                            pc.RPCPlayCustomSound("Bet");
                            data.Health = MaxHealth;
                            break;
                        case Item.TNT:
                            pc.RPCPlayCustomSound("Line");
                            _ = new TNT(pos);
                            break;
                        case Item.InvisibilityPotion:
                            pc.RpcMakeInvisible();
                            LateTask.New(() =>
                            {
                                if (GameStates.IsEnded || !GameStates.InGame || GameStates.IsLobby) return;
                                pc.RpcMakeVisible();
                            }, InvisPotionDuration);
                            break;
                        case Item.SpeedPotion:
                            Main.AllPlayerSpeed[pc.PlayerId] += SpeedPotionSpeedIncrease;
                            pc.MarkDirtySettings();
                            LateTask.New(() =>
                            {
                                if (GameStates.IsEnded || !GameStates.InGame || GameStates.IsLobby) return;
                                Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                                pc.MarkDirtySettings();
                            }, SpeedPotionDuration);
                            break;
                    }

                    data.Inventory.Adjust(selected.Key, -1);
                    break;
                default:
                    if (nextToBed && (bed.Key != data.Team || bed.Value.Bed.Layers.Count > 0))
                        bed.Value.Bed.TryBreak(pc);
                    
                    break;
            }
        }

        public override string ToString()
        {
            var i = 0;
            List<string> itemsDisplays = [];
            string bottomText = Utils.EmptyMessage;

            foreach ((Item item, int count) in Items)
            {
                if (!ItemDisplayData.TryGetValue(item, out ItemDisplay display)) continue;

                var sb = new StringBuilder();
                sb.Append(Utils.ColorString(display.Color, display.Icon.ToString()));

                if (count > 1)
                {
                    sb.Append("<sub>");
                    sb.Append(count);
                    sb.Append("</sub>");
                }
                
                itemsDisplays.Add(sb.ToString());

                if (i++ == SelectedSlot) bottomText = display.Name.Invoke();
            }

            while (itemsDisplays.Count < InventorySlots)
                itemsDisplays.Add(Utils.ColorString(Color.clear, "---"));

            const string baseColor = "<#000000>";
            
            var finalSb = new StringBuilder();
            finalSb.Append(baseColor);
            finalSb.AppendLine("▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁");
            finalSb.Append("</color>");
            i = 0;

            foreach (string itemsDisplay in itemsDisplays)
            {
                bool selected = i == SelectedSlot || i - 1 == SelectedSlot;
                if (!selected) finalSb.Append(baseColor);
                finalSb.Append(selected ? '┃' : '│');
                if (!selected) finalSb.Append("</color>");
                finalSb.Append(' ');
                finalSb.Append(itemsDisplay);
                finalSb.Append(' ');
                i++;
            }

            bool lastSelected = i - 1 == SelectedSlot;
            if (!lastSelected) finalSb.Append(baseColor);
            finalSb.Append(lastSelected ? '┃' : '│');
            if (!lastSelected) finalSb.Append("</color>");
            finalSb.AppendLine(baseColor);
            finalSb.Append("▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔");
            finalSb.AppendLine("</color>");

            finalSb.AppendLine();
            finalSb.Append(Utils.ColorString(Color.white, bottomText));

            return finalSb.ToString();
        }
    }

    internal class Bed : CustomNetObject
    {
        protected string BaseSprite;
        private string StatusText;
        public readonly List<Item> Layers = [];
        public bool IsBroken;
        public readonly HashSet<byte> Breaking = [];

        protected string GetSprite()
        {
            return StatusText + new string('\n', Math.Max(0, 2 - StatusText.Count(x => x == '\n'))) + BaseSprite;
        }

        public void UpdateStatus(bool changeSprite = true)
        {
            List<string> layerStrings = [];

            foreach (Item layer in Layers)
            {
                if (!ItemDisplayData.TryGetValue(layer, out ItemDisplay display)) continue;
                layerStrings.Add(Utils.ColorString(display.Color, display.Icon.ToString()));
            }

            string layers = Layers.Count > 0 ? string.Join(' ', layerStrings) : Translator.GetString("Bedwars.BedStatus.Exposed");
            var nextProtectReq = $"<size=70%>{GetNextProtectReq()} {Translator.GetString("Bedwars.BedStatus.ProtectReq")}</size>";

            Dictionary<Item, int> breakTimes = new[] { Item.Pickaxe, Item.Axe, Item.Shears }.ToDictionary(x => x, x => GetBreakTime(x));
            List<string> bt = [$"{GetBreakTime(null)}"];

            foreach ((Item tool, int time) in breakTimes)
            {
                if (!ItemDisplayData.TryGetValue(tool, out ItemDisplay display)) continue;
                bt.Add($"{Utils.ColorString(display.Color, display.Icon.ToString())} {time}");
            }

            var breakTime = $"<size=70%>{Translator.GetString("Bedwars.BedStatus.BreakTime")}:</size> {string.Join(" | ", bt)}";

            StatusText = $"{layers}\n{nextProtectReq}\n{breakTime}\n{Utils.EmptyMessage}";
            if (changeSprite) RpcChangeSprite(GetSprite());

            Logger.Info($"{GetType().Name} status updated: {StatusText.Replace("\n", " - ")}", "BedWars");
        }

        public int GetNextProtectReq()
        {
            return (int)Math.Pow(2, Layers.Count);
        }

        private int GetBreakTime(Item? tool)
        {
            return 1 + Layers.Sum(x => GetBreakDifficulty(x, tool));
        }

        public void TryBreak(PlayerControl pc)
        {
            if (pc == null || !pc.IsAlive() || !Data.TryGetValue(pc.PlayerId, out PlayerData data)) return;

            if (Layers.Count == 0)
            {
                Broken();
                return;
            }

            if (Breaking.Add(pc.PlayerId)) Main.Instance.StartCoroutine(CoTryBreak());
            return;

            IEnumerator CoTryBreak()
            {
                Item? tool = data.Inventory.GetSelectedItem();
                Item topLayer = Layers[^1];

                int totalTime;
                int breakDifficulty = GetBreakDifficulty(topLayer, tool);

                if (Upgrades.TryGetValue(data.Team, out HashSet<Upgrade> upgrades) && upgrades.Contains(Upgrade.Haste))
                    breakDifficulty = (int)Math.Ceiling(breakDifficulty * 0.75f);

                float timer = totalTime = breakDifficulty;

                long lastNotify = 0;
                ItemDisplay layerDisplay = ItemDisplayData[topLayer];
                string layerName = Utils.ColorString(layerDisplay.Color, layerDisplay.Icon.ToString());
                var str = string.Empty;
                const int progressDisplayParts = 10;

                RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskUpdateSound);

                while (timer > 0f)
                {
                    timer -= Time.deltaTime;
                    yield return new WaitForEndOfFrame();

                    if (lastNotify != Utils.TimeStamp)
                    {
                        lastNotify = Utils.TimeStamp;
                        var left = (int)Math.Ceiling(timer / totalTime * progressDisplayParts);
                        string progress = Utils.ColorString(layerDisplay.Color, $"{new('\u25a0', progressDisplayParts - left)}{new('\u25a1', left)}");
                        string newStr = string.Format(Translator.GetString("Bedwars.BedStatus.Breaking"), layerName, progress);
                        if (newStr != str) pc.Notify(newStr, 100f, true);
                    }

                    if (!pc.IsAlive() || Vector2.Distance(pc.Pos(), Position) > BedBreakAndProtectRange)
                    {
                        Breaking.Remove(pc.PlayerId);
                        NameNotifyManager.Notifies.Remove(pc.PlayerId);
                        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                        yield break;
                    }
                }

                Breaking.Remove(pc.PlayerId);
                NameNotifyManager.Notifies.Remove(pc.PlayerId);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                Layers.RemoveAt(Layers.Count - 1);
                data.Inventory.Adjust(topLayer, GetNextProtectReq());
                UpdateStatus();
                if (Layers.Count == 0 && data.Team != Team) Broken();
            }
        }

        public void Broken()
        {
            BedWarsTeam team = Team;

            foreach ((byte id, PlayerData data) in Data)
            {
                PlayerControl pc = id.GetPlayer();
                if (pc == null || !pc.IsAlive()) continue;

                pc.Notify(data.Team == team ? Translator.GetString("Bedwars.BedStatus.Broken") : string.Format(Translator.GetString("Bedwars.BedStatus.EnemyBroken"), team.GetName()));
            }

            CustomSoundsManager.RPCPlayCustomSoundAll("Gunfire");
            Logger.Info($"Bed of team {team.GetName()} at position {Position} is broken", "BedWars");
            Despawn();
            IsBroken = true;
        }

        private BedWarsTeam Team => this switch
        {
            BlueBed => BedWarsTeam.Blue,
            YellowBed => BedWarsTeam.Yellow,
            GreenBed => BedWarsTeam.Green,
            RedBed => BedWarsTeam.Red,
            _ => throw new InvalidOperationException("Unknown bed type")
        };
    }

    public static void OnTNTExplode(Vector2 position)
    {
        CustomSoundsManager.RPCPlayCustomSoundAll("Boom");
        Logger.Info($"TNT exploded at position {position}", "BedWars");

        foreach ((byte id, PlayerData data) in Data)
        {
            PlayerControl pc = id.GetPlayer();
            if (pc == null || !pc.IsAlive()) continue;

            float distance = Vector2.Distance(pc.Pos(), position);
            if (distance <= TNTRange) data.Damage(pc, distance <= 1f ? TNTDamage : TNTDamage / distance);
        }

        foreach ((Bed bed, _, _) in AllNetObjects.Values)
        {
            if (bed.IsBroken || bed.Layers.Count == 0) continue;
            float distance = Vector2.Distance(bed.Position, position);

            if (distance <= TNTRange)
            {
                float damage = distance <= 1f ? TNTBedDamage : TNTBedDamage / distance;
                var didDamage = false;

                Logger.Info($"Bed at position {bed.Position} ({bed.GetType().Name}) about to take {damage} damage from TNT explosion", "BedWars");

                while (damage > 0f)
                {
                    if (bed.Layers.Count == 0) break;
                    Item topLayer = bed.Layers[^1];
                    if (topLayer is Item.Glass or Item.Obsidian) break;
                    int breakDifficulty = GetBreakDifficulty(topLayer, null);

                    if (breakDifficulty <= damage)
                    {
                        Logger.Info($"Bed at position {bed.Position} ({bed.GetType().Name}) took damage from TNT explosion: {topLayer} (damage: {breakDifficulty})", "BedWars");
                        bed.Layers.RemoveAt(bed.Layers.Count - 1);
                        didDamage = true;
                        damage -= breakDifficulty;
                    }
                    else
                        break;
                }

                if (didDamage)
                {
                    foreach (byte id in bed.Breaking)
                    {
                        var pc = id.GetPlayer();
                        if (pc == null) continue;
                        
                        NameNotifyManager.Notifies.Remove(pc.PlayerId);
                        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                    }
                    
                    bed.Breaking.Clear();
                    bed.UpdateStatus();
                }
            }
        }
    }

    public static void OnChat(PlayerControl player, string message)
    {
        if (!Data.TryGetValue(player.PlayerId, out PlayerData data)) return;
        
        message = message.Trim().Replace(" ", string.Empty).Trim();

        if (int.TryParse(message, out int slot) && slot > 0 && slot <= InventorySlots)
            data.Inventory.SelectedSlot = slot - 1;

        if (message == "cls")
        {
            Item? selected = data.Inventory.GetSelectedItem();
            if (!selected.HasValue) return;
            data.Inventory.Adjust(selected.Value, -data.Inventory.Items[selected.Value]);
        }
    }

    public static void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.PhantomCooldown = 0.1f;

        try { AURoleOptions.GuardianAngelCooldown = 900f; }
        catch (Exception e) { Utils.ThrowException(e); }

        if (Trapped.Contains(playerId))
        {
            opt.SetVision(false);
            opt.SetFloat(FloatOptionNames.CrewLightMod, TrappedVision);
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, TrappedVision);
        }
    }

    public static bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return Data.TryGetValue(pc.PlayerId, out PlayerData data) && data.Inventory.Items.ContainsKey(Item.EnderPearl);
    }

    public static bool OnVanish(PlayerControl pc)
    {
        if (InShop.TryGetValue(pc.PlayerId, out Shop shop)) shop.NextSelection(pc);
        else if (Data.TryGetValue(pc.PlayerId, out PlayerData data)) data.Inventory.NextSlot();
        return false;
    }

    public static void OnPet(PlayerControl pc)
    {
        if (InShop.TryGetValue(pc.PlayerId, out Shop shop)) shop.Purchase(pc);
        else if (Data.TryGetValue(pc.PlayerId, out PlayerData data)) data.Inventory.UseSelectedItem(pc);
    }

    public static void OnExitVent(PlayerControl pc, Vent vent)
    {
        if (Data.TryGetValue(pc.PlayerId, out PlayerData data))
            data.Inventory.Adjust(Item.EnderPearl, -1);
    }
}

public class BedWarsPlayer : RoleBase
{
    private static bool On;

    public override bool IsEnable => On;

    public override void SetupCustomOption() { }

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
        BedWars.ApplyGameOptions(opt, playerId);
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return BedWars.CanUseImpostorVentButton(pc);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        return BedWars.OnVanish(pc);
    }

    public override void OnPet(PlayerControl pc)
    {
        BedWars.OnPet(pc);
    }

    public override void OnExitVent(PlayerControl pc, Vent vent)
    {
        BedWars.OnExitVent(pc, vent);
    }
}
