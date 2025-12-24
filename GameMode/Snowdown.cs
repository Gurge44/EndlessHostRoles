using System;
using System.Collections.Generic;
using System.Linq;
using Hazel;
using UnityEngine;

namespace EHR;

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

        PowerUpPriceOptions = Enum.GetValues<PowerUp>().ToDictionary(x => x, x => new IntegerOptionItem(id++, "Snowdown.PowerUpPriceOption", new(1, 20, 1), PowerUpPrices[x], tab)
            .SetColor(color)
            .SetGameMode(gameMode)
            .SetValueFormat(OptionFormat.Pieces)
            .AddReplacement(("{powerup}", Translator.GetString($"Snowdown.PowerUp.{x}"))));
    }

    public static void ApplyGameOptions()
    {
        AURoleOptions.PhantomCooldown = 2f;
        AURoleOptions.PhantomDuration = 0.1f;
    }

    public static string GetSuffix(PlayerControl seer, PlayerControl target)
    {
        if (!Data.TryGetValue(seer.PlayerId, out PlayerData seerData) || !Data.TryGetValue(target.PlayerId, out PlayerData targetData)) return string.Empty;
        
        if (seer.PlayerId == target.PlayerId)
            return seerData.GetSelfSuffix();
        
        StringBuilder sb = new("<#ffffff>");
        AddStats(sb, targetData);
        sb.Append("</color>");
        return sb.ToString();
    }

    private static void AddStats(StringBuilder sb, PlayerData targetData)
    {
        sb.Append("<#e4fdff>");
        sb.Append(Nums1[targetData.SnowballsReady]);
        sb.Append("</color>");

        sb.Append(' ');
        
        sb.Append("<#dfc57b>");
        sb.Append(Nums2[targetData.Coins]);
        sb.Append("</color>");

        if (targetData.Points > 0)
        {
            sb.Append(' ');
            
            sb.Append("<#e5acff>");
            sb.Append(Nums3[targetData.Points - 1]);
            sb.Append("</color>");
        }
    }

    public static string GetStatistics(byte id)
    {
        if (!Data.TryGetValue(id, out PlayerData data)) return string.Empty;
        StringBuilder sb = new("<#ffffff>");
        AddStats(sb, data);
        sb.Append("</color>");
        return sb.ToString();
    }

    public static string GetHudText()
    {
        if (!GameEndsAfterTime) return string.Empty;
        long timeLeft = GameEndTime - (Utils.TimeStamp - GameStartTS);
        if (timeLeft == 60)
        {
            SoundManager.Instance.PlaySound(HudManager.Instance.LobbyTimerExtensionUI.lobbyTimerPopUpSound, false);
            Utils.FlashColor(new(1f, 1f, 0f, 0.4f), 1.4f);
        }
        return $"{timeLeft / 60:00}:{timeLeft % 60:00}";
    }

    public static bool CheckGameEnd(out GameOverReason reason)
    {
        reason = GameOverReason.ImpostorsByKill;
        if (GameStates.IsEnded || !Main.IntroDestroyed) return false;
        PlayerControl[] aapc = Main.AllAlivePlayerControls;

        switch (aapc.Length)
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
                    CustomWinnerHolder.WinnerIds = Data.Where(x => x.Value.Points == max && x.Key.GetPlayer() != null).Select(x => x.Key).ToHashSet();
                    Logger.Info($"Winners: {(string.Join(", ", CustomWinnerHolder.WinnerIds.Select(x => Main.AllPlayerNames.GetValueOrDefault(x, "[Unknown player]"))))}", "Snowdown");
                    Main.DoBlockNameChange = true;
                    return true;
                }

                if (GameEndsWhenPointsReached && Data.IntersectBy(Main.AllAlivePlayerControls.Select(p => p.PlayerId), x => x.Key).FindFirst(x => x.Value.Points >= PointsToReach, out var winnerData))
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
        
        Data = Main.AllPlayerControls.ToDictionary(x => x.PlayerId, _ => new PlayerData());
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
        killer.SetKillCooldown(5f);
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
            Snowball touchingSnowball = Snowballs.Find(x => x.Active && x.Thrower != __instance && Vector2.Distance(x.Position, pos) < 1.5f);

            if (touchingSnowball != null)
            {
                touchingSnowball.SetInactive();
                
                if (touchingSnowball.Thrower != null && Data.TryGetValue(touchingSnowball.Thrower.PlayerId, out PlayerData throwerData) && throwerData.Coins < throwerData.MaxCoins)
                    throwerData.Coins++;
            }

            if (data.SnowballsReady < data.MaxSnowballsReady && now - data.LastSnowballGainTS >= data.SnowballGainInterval)
            {
                data.SnowballsReady++;
                data.LastSnowballGainTS = now;
            }

            if (Vector2.Distance(pos, data.LastPosition) > 0.01f)
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

            PowerUp powerUp = Enum.GetValues<PowerUp>()[ShopSelectedIndex];
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
                
                if (ShopSelectedIndex >= Enum.GetValues<PowerUp>().Length)
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
            StringBuilder sb = new("<#ffffff><size=140%>");
            
            AddStats(sb, this);

            sb.Append("</size>");

            if (InShop)
            {
                PowerUp powerUp = Enum.GetValues<PowerUp>()[ShopSelectedIndex];
                sb.Append('\n');
                sb.Append('\n');
                sb.Append(Translator.GetString($"Snowdown.PowerUp.{powerUp}"));
                sb.Append(' ');
                sb.Append("<#dfc57b>");
                sb.Append(Nums2[PowerUpPrices[powerUp]]);
                sb.Append("</color>");
                sb.Append('\n');
                sb.Append("<size=90%>");
                sb.Append(Translator.GetString($"Snowdown.PowerUp.{powerUp}.Description"));
                sb.Append("</size>");

                if (ShowHelp)
                {
                    sb.Append('\n');
                    sb.Append('\n');
                    sb.Append("<size=80%>");
                    sb.Append(Translator.GetString("Snowdown.ShopHelp"));
                    sb.Append("</size>");
                }
            }

            sb.Append("</color>");
            return sb.ToString();
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
