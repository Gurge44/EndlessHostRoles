using EHR.Roles;
using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EHR.Gamemodes;

public static class Snowdown
{
    // Kill: Give the target a snowball
    // Vanish: Throw snowball in movement direction / Cycle to next powerup while in shop
    // Sabotage: Enter/Exit shop
    // Pet: Buy the selected powerup in the shop

    public static float SnowballThrowSpeed = 4f;
    private static int SnowballGainFrequency = 5;
    private static int MaxSnowballsInHand = 5;
    private static int MaxCoinsInHand = 10;
    private static bool GameEndsAfterTime = true;
    private static int GameEndTime = 300;
    private static bool GameEndsWhenPointsReached = true;
    private static int PointsToReach = 5;
    private static Dictionary<PowerUp, int> PowerUpPrices = new()
    {
        [PowerUp.FillUp] = 3,
        [PowerUp.IncreaseCapacity] = 1,
        [PowerUp.FasterPickup] = 2
    };

    private static OptionItem SnowballThrowSpeedOption;
    private static OptionItem SnowballGainFrequencyOption;
    private static OptionItem MaxSnowballsInHandOption;
    private static OptionItem MaxCoinsInHandOption;
    private static OptionItem GameEndsAfterTimeOption;
    private static OptionItem GameEndTimeOption;
    private static OptionItem GameEndsWhenPointsReachedOption;
    private static OptionItem PointsToReachOption;
    private static Dictionary<PowerUp, OptionItem> PowerUpPriceOptions = [];
    
    private static readonly PowerUp[] AllPowerUp = Enum.GetValues<PowerUp>();
    private static readonly StringBuilder SelfSuffix = new();
    private static readonly StringBuilder Suffix = new();
    private static readonly StringBuilder Statistics = new();

    public static Dictionary<byte, PlayerData> Data = [];
    private static List<Snowball> Snowballs = [];
    public static ((float Left, float Right) X, (float Bottom, float Top) Y) MapBounds;
    private static long GameStartTS;
    
    private static readonly char[] Nums1 = "⓿❶❷❸❹❺❻❼❽❾❿".ToCharArray();
    private static readonly char[] Nums2 = "⓪①②③④⑤⑥⑦⑧⑨⑩⑪⑫⑬⑭⑮⑯⑰⑱⑲⑳".ToCharArray();
    private static readonly char[] Nums3 = "⑴⑵⑶⑷⑸⑹⑺⑻⑼⑽⑾⑿⒀⒁⒂⒃⒄⒅⒆⒇".ToCharArray();
    
    public static void SetupCustomOption()
    {
        var id = 69_225_001;
        Color color = Utils.GetRoleColor(CustomRoles.SnowdownPlayer);
        const CustomGameMode gameMode = CustomGameMode.Snowdown;
        const TabGroup tab = TabGroup.GameSettings;
        
        SnowballThrowSpeedOption = new FloatOptionItem(id++, "Snowdown.SnowballThrowSpeedOption", new(0.25f, 10f, 0.25f), 4f, tab)
            .SetHeader(true)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Multiplier);
        
        SnowballGainFrequencyOption = new IntegerOptionItem(id++, "Snowdown.SnowballGainFrequencyOption", new(1, 30, 1), 5, tab)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Seconds);
        
        MaxSnowballsInHandOption = new IntegerOptionItem(id++, "Snowdown.MaxSnowballsInHandOption", new(1, 10, 1), 5, tab)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Pieces);
        
        MaxCoinsInHandOption = new IntegerOptionItem(id++, "Snowdown.MaxCoinsInHandOption", new(1, 20, 1), 10, tab)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Pieces);

        GameEndsAfterTimeOption = new BooleanOptionItem(id++, "Snowdown.GameEndsAfterTimeOption", true, tab)
            .SetColor(color)
            .SetGameMode(gameMode);
        
        GameEndTimeOption = new IntegerOptionItem(id++, "Snowdown.GameEndTimeOption", new(15, 1800, 15), 300, tab)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetParent(GameEndsAfterTimeOption)
            .SetValueFormat(OptionFormat.Seconds);
        
        GameEndsWhenPointsReachedOption = new BooleanOptionItem(id++, "Snowdown.GameEndsWhenPointsReachedOption", true, tab)
            .SetColor(color)
            .SetGameMode(gameMode);
        
        PointsToReachOption = new IntegerOptionItem(id++, "Snowdown.PointsToReachOption", new(1, 100, 1), 5, tab)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetParent(GameEndsWhenPointsReachedOption)
            .SetValueFormat(OptionFormat.Pieces);

        PowerUpPriceOptions = AllPowerUp.ToDictionary(x => x, x => new IntegerOptionItem(id++, "Snowdown.PowerUpPriceOption", new(1, 20, 1), PowerUpPrices[x], tab)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Pieces)
            .AddReplacement(("{powerup}", Translator.GetString($"Snowdown.PowerUp.{x}"))));
    }

    public static void ApplyGameOptions()
    {
        AURoleOptions.PhantomCooldown = 1f;
        AURoleOptions.PhantomDuration = 0.1f;
    }

    public static string GetSuffix(PlayerControl seer, PlayerControl target)
    {
        if (!Data.TryGetValue(seer.PlayerId, out PlayerData seerData) || !Data.TryGetValue(target.PlayerId, out PlayerData targetData)) return string.Empty;
        
        if (seer.PlayerId == target.PlayerId)
            return seerData.GetSelfSuffix();

        Suffix.Clear().Append("<#ffffff>");
        AddStats(Suffix, targetData);
        Suffix.Append("</color>");
        return Suffix.ToString();
    }

    private static void AddStats(StringBuilder sb, PlayerData targetData)
    {
        sb.Append("<#e4fdff>")
            .Append(Nums1[targetData.SnowballsReady])
            .Append("</color>")
            .Append(' ')
            .Append("<#dfc57b>")
            .Append(Nums2[targetData.Coins])
            .Append("</color>");

        if (targetData.Points > 0)
        {
            sb.Append(' ')
                .Append("<#e5acff>")
                .Append(Nums3[targetData.Points - 1])
                .Append("</color>");
        }
    }

    public static string GetStatistics(byte id)
    {
        if (!Data.TryGetValue(id, out PlayerData data)) return string.Empty;
        Statistics.Clear().Append("<#ffffff>");
        AddStats(Statistics, data);
        Statistics.Append("</color>");
        return Statistics.ToString();
    }

    public static string GetHudText()
    {
        if (!GameEndsAfterTime) return string.Empty;
        long timeLeft = GameEndTime - (Utils.TimeStamp - GameStartTS);
        return $"{timeLeft / 60:00}:{timeLeft % 60:00}";
    }

    public static bool CheckGameEnd(out GameOverReason reason)
    {
        reason = GameOverReason.ImpostorsByKill;
        if (GameStates.IsEnded || !Main.IntroDestroyed) return false;
        var aapc = Main.CachedAlivePlayerControls();

        switch (aapc.Count)
        {
            case 1:
            {
                PlayerControl winner = aapc[0];
                Logger.Info($"Winner: {winner.GetRealName().RemoveHtmlTags()}", "Snowdown");
                CustomWinnerHolder.WinnerIds = [winner.PlayerId];
                Main.DoBlockNameChange = true;
                return true;
            }
            case 0:
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
                Logger.Warn("No players alive. Force ending the game", "Snowdown");
                return true;
            }
            default:
            {
                if (GameEndsAfterTime && Utils.TimeStamp - GameStartTS >= GameEndTime)
                {
                    int max = Data.Values.Max(x => x.Points);
                    CustomWinnerHolder.WinnerIds = Data.Where(x => x.Value.Points == max && x.Key.GetPlayer()).Select(x => x.Key).ToHashSet();
                    Logger.Info($"Winners: {string.Join(", ", CustomWinnerHolder.WinnerIds.Select(x => Main.AllPlayerNames.GetValueOrDefault(x, "[Unknown player]")))}", "Snowdown");
                    Main.DoBlockNameChange = true;
                    return true;
                }

                if (GameEndsWhenPointsReached && Data.IntersectBy(Main.EnumerateAlivePlayerControls().Select(p => p.PlayerId), x => x.Key).FindFirst(x => x.Value.Points >= PointsToReach, out var winnerData))
                {
                    CustomWinnerHolder.WinnerIds = [winnerData.Key];
                    Logger.Info($"Winners: {Main.AllPlayerNames.GetValueOrDefault(winnerData.Key, "[Unknown player]")}", "Snowdown");
                    Main.DoBlockNameChange = true;
                    return true;
                }

                return false;
            }
        }
    }

    public static void Init()
    {
        SnowballThrowSpeed = SnowballThrowSpeedOption.GetFloat();
        SnowballGainFrequency = SnowballGainFrequencyOption.GetInt();
        MaxSnowballsInHand = MaxSnowballsInHandOption.GetInt();
        MaxCoinsInHand = MaxCoinsInHandOption.GetInt();
        GameEndsAfterTime = GameEndsAfterTimeOption.GetBool();
        GameEndTime = GameEndTimeOption.GetInt();
        GameEndsWhenPointsReached = GameEndsWhenPointsReachedOption.GetBool();
        PointsToReach = PointsToReachOption.GetInt();
        PowerUpPrices = PowerUpPriceOptions.ToDictionary(x => x.Key, x => x.Value.GetInt());
        
        Data = Main.EnumeratePlayerControls().ToDictionary(x => x.PlayerId, _ => new PlayerData());
        Snowballs = [];
        
        Dictionary<SystemTypes, Vector2>.ValueCollection rooms = RandomSpawn.SpawnMap.GetSpawnMap().Positions?.Values;
        if (rooms == null) return;

        float[] x = rooms.Select(r => r.x).ToArray();
        float[] y = rooms.Select(r => r.y).ToArray();

        const float extend = 5f;
        MapBounds = ((x.Min() - extend, x.Max() + extend), (y.Min() - extend, y.Max() + extend));
    }

    public static void GameStart() // Called as non-host client too!
    {
        GameStartTS = Utils.TimeStamp;
        if (!AmongUsClient.Instance.AmHost) GameEndTime = GameEndTimeOption.GetInt();
    }

    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        killer.SetKillCooldownNonSync(5f);
        if (!Data.TryGetValue(killer.PlayerId, out PlayerData killerData) || !Data.TryGetValue(target.PlayerId, out PlayerData targetData) || killerData.SnowballsReady < 1 || targetData.SnowballsReady >= targetData.MaxSnowballsReady) return;
        killerData.SnowballsReady--;
        targetData.SnowballsReady++;
    }

    //[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class FixedUpdatePatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || Options.CurrentGameMode != CustomGameMode.Snowdown || !Main.IntroDestroyed) return;
            if (!__instance.IsAlive() || !Data.TryGetValue(__instance.PlayerId, out PlayerData data)) return;

            long now = Utils.TimeStamp;
            Vector2 pos = __instance.Pos();
            Snowball touchingSnowball = Snowballs.Find(x => x.Active && x.Thrower != __instance && FastVector2.DistanceWithinRange(x.Position, pos, 1.5f));

            if (touchingSnowball != null)
            {
                touchingSnowball.SetInactive();
                
                if (touchingSnowball.Thrower && Data.TryGetValue(touchingSnowball.Thrower.PlayerId, out PlayerData throwerData) && throwerData.Coins < throwerData.MaxCoins)
                    throwerData.Coins++;
            }

            if (data.SnowballsReady < data.MaxSnowballsReady && now - data.LastSnowballGainTS >= data.SnowballGainInterval)
            {
                data.SnowballsReady++;
                data.LastSnowballGainTS = now;
            }

            if (!FastVector2.DistanceWithinRange(pos, data.LastPosition, 0.01f))
            {
                data.LastLastPosition = data.LastPosition;
                data.LastPosition = pos;
            }

            Utils.NotifyRoles(SpecifySeer: __instance, SendOption: SendOption.None);
        }
    }

    public class PlayerData
    {
        public Vector2 LastPosition = new(0f, 0f);
        public Vector2 LastLastPosition = new(0f, 0f);
        public int SnowballsReady;
        public int MaxSnowballsReady = MaxSnowballsInHand;
        public long LastSnowballGainTS;
        public int SnowballGainInterval = SnowballGainFrequency;
        public int Coins;
        public int MaxCoins = MaxCoinsInHand;
        public bool InShop;
        private int ShopSelectedIndex;
        public int Points;
        private bool ShowHelp;

        public void OnSabotage(PlayerControl pc)
        {
            InShop = (SnowballsReady != MaxSnowballsReady || MaxSnowballsReady < 20 || MaxCoins < 10 || SnowballGainInterval > 1) && !InShop;
            ShowHelp = !Main.HasPlayedGM[CustomGameMode.Snowdown].Contains(pc.FriendCode);
            if (pc.AmOwner) HudSpritePatch.ForceUpdate = true;
        }
        
        public void OnPet(PlayerControl pc)
        {
            if (!InShop)
            {
                if (Coins >= 20)
                {
                    Points++;
                    Coins -= 20;
                }
                
                return;
            }

            PowerUp powerUp = AllPowerUp[ShopSelectedIndex];
            int cost = PowerUpPrices[powerUp];
            if (Coins < cost) return;

            switch (powerUp)
            {
                case PowerUp.FillUp:
                {
                    if (SnowballsReady == MaxSnowballsReady) return;
                    SnowballsReady = MaxSnowballsReady;
                    break;
                }
                case PowerUp.IncreaseCapacity:
                {
                    switch (MaxSnowballsReady, MaxCoins)
                    {
                        case (>= 10, >= 20):
                            return;
                        case (>= 10, < 20):
                            MaxCoins++;
                            break;
                        case (< 10, >= 20):
                            MaxSnowballsReady++;
                            break;
                        case (< 10, < 20):
                            MaxSnowballsReady++;
                            MaxCoins++;
                            break;
                    }
                    
                    break;
                }
                case PowerUp.FasterPickup:
                {
                    if (SnowballGainInterval <= 1) return;
                    SnowballGainInterval--;
                    break;
                }
            }

            Coins -= cost;

            if (SnowballsReady == MaxSnowballsReady && MaxSnowballsReady >= 20 && MaxCoins >= 10 && SnowballGainInterval <= 1)
                InShop = false;
        }
        
        public void OnVanish(PlayerControl pc)
        {
            if (InShop)
            {
                ShopSelectedIndex++;
                
                if (ShopSelectedIndex >= AllPowerUp.Length)
                    ShopSelectedIndex = 0;
            }
            else
            {
                if (SnowballsReady <= 0) return;
                SnowballsReady--;
                
                Vector2 from = pc.Pos();
                Vector2 direction = (LastPosition - LastLastPosition).normalized;

                Snowball inactive = Snowballs.Find(x => !x.Active);
                
                if (inactive != null) inactive.Reuse(from, direction, pc);
                else Snowballs.Add(new Snowball(from, direction, pc));
            }
        }

        public string GetSelfSuffix()
        {
            SelfSuffix.Clear().Append("<#ffffff><size=140%>");
            
            AddStats(SelfSuffix, this);

            SelfSuffix.Append("</size>");

            if (InShop)
            {
                PowerUp powerUp = AllPowerUp[ShopSelectedIndex];
                SelfSuffix.Append('\n')
                    .Append('\n')
                    .Append(Translator.GetString($"Snowdown.PowerUp.{powerUp}"))
                    .Append(' ')
                    .Append("<#dfc57b>")
                    .Append(Nums2[PowerUpPrices[powerUp]])
                    .Append("</color>")
                    .Append('\n')
                    .Append("<size=90%>")
                    .Append(Translator.GetString($"Snowdown.PowerUp.{powerUp}.Description"))
                    .Append("</size>");

                if (ShowHelp)
                {
                    SelfSuffix.Append('\n')
                        .Append('\n')
                        .Append("<size=80%>")
                        .Append(Translator.GetString("Snowdown.ShopHelp"))
                        .Append("</size>");
                }
            }

            SelfSuffix.Append("</color>");
            return SelfSuffix.ToString();
        }
    }

    public enum PowerUp
    {
        FillUp,
        IncreaseCapacity,
        FasterPickup
    }
}

internal class SnowdownPlayer : RoleBase
{
    public static bool On;

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

    public override bool CanUseSabotage(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override bool OnSabotage(PlayerControl pc)
    {
        if (Snowdown.Data.TryGetValue(pc.PlayerId, out Snowdown.PlayerData data))
            data.OnSabotage(pc);
        
        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (Snowdown.Data.TryGetValue(pc.PlayerId, out Snowdown.PlayerData data))
            data.OnVanish(pc);
        
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        if (Snowdown.Data.TryGetValue(pc.PlayerId, out Snowdown.PlayerData data))
            data.OnPet(pc);
    }
}
