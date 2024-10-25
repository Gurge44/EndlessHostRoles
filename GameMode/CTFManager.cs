using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using HarmonyLib;
using UnityEngine;

namespace EHR
{
    public static class CTFManager
    {
        private static OptionItem AlertTeamMembersOfFlagTaken;
        private static OptionItem ArrowToEnemyFlagCarrier;
        private static OptionItem AlertTeamMembersOfEnemyFlagTaken;
        private static OptionItem ArrowToOwnFlagCarrier;
        private static OptionItem TaggedPlayersGet;
        private static OptionItem WhenFlagCarrierGetsTagged;
        private static OptionItem SpeedReductionForFlagCarrier;

        private static readonly string[] TaggedPlayersGetOptions =
        [
            "CTF_TaggedPlayersGet.SentBackToBase",
            "CTF_TaggedPlayersGet.Killed"
        ];

        private static readonly string[] WhenFlagCarrierGetsTaggedOptions =
        [
            "CTF_WhenFlagCarrierGetsTagged.FlagIsDropped",
            "CTF_WhenFlagCarrierGetsTagged.FlagIsReturned"
        ];

        public static (Color Color, string Team) WinnerData = (Color.white, "No one wins");

        private static Dictionary<byte, CTFTeam> PlayerTeams = [];
        private static Dictionary<CTFTeam, CTFTeamData> TeamData = [];
        private static Dictionary<byte, CTFPlayerData> PlayerData = [];
        private static Dictionary<byte, NetworkedPlayerInfo.PlayerOutfit> DefaultOutfits = [];
        private static bool ValidTag;
        public static bool IsDeathPossible => TaggedPlayersGet.GetValue() == 1;

        private static NetworkedPlayerInfo.PlayerOutfit YellowOutfit => new NetworkedPlayerInfo.PlayerOutfit().Set("", 5, "", "", "", "pet_coaltonpet", "");
        private static NetworkedPlayerInfo.PlayerOutfit BlueOutfit => new NetworkedPlayerInfo.PlayerOutfit().Set("", 1, "", "", "", "pet_coaltonpet", "");

        private static (Vector2 Position, string RoomName) BlueFlagBase => Main.CurrentMap switch
        {
            MapNames.Skeld => (new(16.5f, -4.8f), Translator.GetString(SystemTypes.Nav.ToString())),
            MapNames.Mira => (new(-4.5f, 2.0f), Translator.GetString(SystemTypes.Launchpad.ToString())),
            MapNames.Dleks => (new(-16.5f, -4.8f), Translator.GetString(SystemTypes.Nav.ToString())),
            MapNames.Polus => (new(9.5f, -12.5f), Translator.GetString(SystemTypes.Electrical.ToString())),
            MapNames.Airship => (new(-23.5f, -1.6f), Translator.GetString(SystemTypes.Cockpit.ToString())),
            MapNames.Fungle => (new(-15.5f, -7.5f), Translator.GetString(SystemTypes.Kitchen.ToString())),
            _ => (Vector2.zero, string.Empty)
        };

        private static (Vector2 Position, string RoomName) YellowFlagBase => Main.CurrentMap switch
        {
            MapNames.Skeld => (new(-20.5f, -5.5f), Translator.GetString(SystemTypes.Reactor.ToString())),
            MapNames.Mira => (new(17.8f, 23.0f), Translator.GetString(SystemTypes.Greenhouse.ToString())),
            MapNames.Dleks => (new(20.5f, -5.5f), Translator.GetString(SystemTypes.Reactor.ToString())),
            MapNames.Polus => (new(36.5f, -7.5f), Translator.GetString(SystemTypes.Laboratory.ToString())),
            MapNames.Airship => (new(33.5f, -1.5f), Translator.GetString(SystemTypes.CargoBay.ToString())),
            MapNames.Fungle => (new(22.2f, 13.7f), Translator.GetString(SystemTypes.Comms.ToString())),
            _ => (Vector2.zero, string.Empty)
        };

        public static void SetupCustomOption()
        {
            const int id = 69_215_001;
            Color color = Utils.GetRoleColor(CustomRoles.CTFPlayer);

            AlertTeamMembersOfFlagTaken = new BooleanOptionItem(id, "CTF_AlertTeamMembersOfFlagTaken", true, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.CaptureTheFlag)
                .SetColor(color);

            ArrowToEnemyFlagCarrier = new BooleanOptionItem(id + 1, "CTF_ArrowToEnemyFlagCarrier", true, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.CaptureTheFlag)
                .SetParent(AlertTeamMembersOfFlagTaken)
                .SetColor(color);

            AlertTeamMembersOfEnemyFlagTaken = new BooleanOptionItem(id + 2, "CTF_AlertTeamMembersOfEnemyFlagTaken", true, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.CaptureTheFlag)
                .SetColor(color);

            ArrowToOwnFlagCarrier = new BooleanOptionItem(id + 3, "CTF_ArrowToOwnFlagCarrier", true, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.CaptureTheFlag)
                .SetParent(AlertTeamMembersOfEnemyFlagTaken)
                .SetColor(color);

            TaggedPlayersGet = new StringOptionItem(id + 4, "CTF_TaggedPlayersGet", TaggedPlayersGetOptions, 0, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.CaptureTheFlag)
                .SetColor(color);

            WhenFlagCarrierGetsTagged = new StringOptionItem(id + 5, "CTF_WhenFlagCarrierGetsTagged", WhenFlagCarrierGetsTaggedOptions, 0, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.CaptureTheFlag)
                .SetColor(color);

            SpeedReductionForFlagCarrier = new FloatOptionItem(id + 6, "CTF_SpeedReductionForFlagCarrier", new(0f, 1f, 0.05f), 0.25f, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.CaptureTheFlag)
                .SetValueFormat(OptionFormat.Multiplier)
                .SetColor(color);
        }

        public static bool KnowTargetRoleColor(PlayerControl seer, PlayerControl target, ref string color)
        {
            if (!ValidTag) return false;
            Color32 teamColor = PlayerTeams[target.PlayerId].GetTeamColor();
            color = $"#{teamColor.r:x2}{teamColor.g:x2}{teamColor.b:x2}{teamColor.a:x2}";
            return true;
        }

        public static string GetSuffixText(PlayerControl seer, PlayerControl target)
        {
            if (!ValidTag) return string.Empty;
            if (seer.PlayerId != target.PlayerId) return string.Empty;
            var arrows = TargetArrow.GetAllArrows(seer);
            arrows = arrows.Length > 0 ? $"{arrows}\n" : string.Empty;
            return $"{arrows}<size=1.4>{GetStatistics(target.PlayerId).Replace(" - ", "\n")}</size>";
        }

        public static string GetStatistics(byte id)
        {
            if (!PlayerData.TryGetValue(id, out var stats)) return string.Empty;
            return string.Format(Translator.GetString("CTF_PlayerStats"), Math.Round(stats.FlagTime, 1), stats.TagCount);
        }

        public static int GetFlagTime(byte id) => (int)Math.Round(PlayerData[id].FlagTime);

        public static bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            var aapc = Main.AllAlivePlayerControls;

            if (!ValidTag) return false;

            switch (aapc.Length)
            {
                case 0:
                    ResetSkins();
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
                    CustomWinnerHolder.WinnerIds = Main.PlayerStates.Keys.ToHashSet();
                    reason = GameOverReason.HumansDisconnect;
                    return true;
                case 1:
                    ResetSkins();
                    TeamData[PlayerTeams[aapc[0].PlayerId]].SetAsWinner();
                    return true;
                default:
                    // If WinnerData is already set, end the game
                    if (WinnerData.Team != "No one wins")
                    {
                        ResetSkins();
                        return true;
                    }

                    // If all players are on the same team, end the game
                    if (aapc.All(x => PlayerTeams[x.PlayerId] == CTFTeam.Blue) || aapc.All(x => PlayerTeams[x.PlayerId] == CTFTeam.Yellow))
                    {
                        ResetSkins();
                        TeamData[PlayerTeams[aapc[0].PlayerId]].SetAsWinner();
                        return true;
                    }

                    return false;
            }

            void ResetSkins() => DefaultOutfits.Select(x => (pc: x.Key.GetPlayer(), outfit: x.Value)).DoIf(x => x.pc != null && x.outfit != null, x => Utils.RpcChangeSkin(x.pc, x.outfit));
        }

        public static void OnGameStart()
        {
            // Reset all data
            PlayerTeams = [];
            TeamData = [];
            WinnerData = (Color.white, "No one wins");
            PlayerData = Main.PlayerStates.Keys.ToDictionary(x => x, _ => new CTFPlayerData());
            DefaultOutfits = Main.AllPlayerControls.ToDictionary(x => x.PlayerId, x => x.Data.DefaultOutfit);
            ValidTag = false;

            // Check if the current game mode is Capture The Flag
            if (Options.CurrentGameMode != CustomGameMode.CaptureTheFlag) return;

            LateTask.New(() =>
            {
                // Assign players to teams
                List<PlayerControl> players = Main.AllAlivePlayerControls.Shuffle().ToList();
                if (Main.GM.Value) players.RemoveAll(x => x.IsHost());

                int blueCount = players.Count / 2;
                HashSet<byte> bluePlayers = [];
                HashSet<byte> yellowPlayers = [];
                var blueOutfit = BlueOutfit;
                var yellowOutfit = YellowOutfit;

                for (int i = 0; i < blueCount; i++)
                {
                    PlayerControl player = players.FirstOrDefault(p => p.Data.DefaultOutfit.ColorId == 1) ?? players.FirstOrDefault(x => x.Data.DefaultOutfit.ColorId != 5) ?? players.RandomElement();
                    players.Remove(player);
                    PlayerTeams[player.PlayerId] = CTFTeam.Blue;
                    bluePlayers.Add(player.PlayerId);
                    blueOutfit.PlayerName = player.GetRealName();
                    blueOutfit.PetId = player.Data.DefaultOutfit.PetId;
                    Utils.RpcChangeSkin(player, blueOutfit);
                }

                foreach (PlayerControl player in players)
                {
                    PlayerTeams[player.PlayerId] = CTFTeam.Yellow;
                    yellowPlayers.Add(player.PlayerId);
                    yellowOutfit.PlayerName = player.GetRealName();
                    yellowOutfit.PetId = player.Data.DefaultOutfit.PetId;
                    Utils.RpcChangeSkin(player, yellowOutfit);
                }

                // Create flags
                var blueFlagBase = BlueFlagBase;
                var yellowFlagBase = YellowFlagBase;

                CustomNetObject blueFlag = new BlueFlag(blueFlagBase.Position);
                CustomNetObject yellowFlag = new YellowFlag(yellowFlagBase.Position);

                // Create team data
                TeamData[CTFTeam.Blue] = new(CTFTeam.Blue, blueFlag, bluePlayers, byte.MaxValue);
                TeamData[CTFTeam.Yellow] = new(CTFTeam.Yellow, yellowFlag, yellowPlayers, byte.MaxValue);

                // Teleport players to their respective bases
                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    if (PlayerTeams.TryGetValue(pc.PlayerId, out var team))
                    {
                        switch (team)
                        {
                            case CTFTeam.Blue:
                                Loop.Times(3, _ => pc.TP(blueFlagBase.Position));
                                pc.Notify(string.Format(Translator.GetString("CTF_Notify_EnemyTeamRoom"), yellowFlagBase.RoomName));
                                break;
                            case CTFTeam.Yellow:
                                Loop.Times(3, _ => pc.TP(yellowFlagBase.Position));
                                pc.Notify(string.Format(Translator.GetString("CTF_Notify_EnemyTeamRoom"), blueFlagBase.RoomName));
                                break;
                        }
                    }

                    pc.CheckAndSetUnshiftState(force: true);
                    pc.RpcResetAbilityCooldown();
                }

                ValidTag = true;
                LateTask.New(() => Main.ProcessShapeshifts = true, 3f, log: false);
            }, 10f, "CTFManager.OnGameStart");
        }

        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            var targetTeam = PlayerTeams[target.PlayerId];
            if (!ValidTag || PlayerTeams[killer.PlayerId] == targetTeam || TeamData.Values.Any(x => x.FlagCarrier == killer.PlayerId)) return;

            new[] { killer, target }.Do(x => x.SetKillCooldown());

            if (TeamData.FindFirst(x => x.Value.FlagCarrier == target.PlayerId, out var kvp))
            {
                kvp.Value.DropFlag();
                if (WhenFlagCarrierGetsTagged.GetValue() == 1)
                {
                    kvp.Value.Flag.TP(kvp.Key.GetFlagBase().Position);
                }
            }

            switch (TaggedPlayersGet.GetValue())
            {
                case 0:
                    Loop.Times(3, _ => target.TP(targetTeam.GetFlagBase().Position));
                    Main.AllPlayerSpeed[target.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                    target.MarkDirtySettings();
                    break;
                case 1:
                    target.Suicide();
                    var notify = string.Format(Translator.GetString("CTF_TeamMemberFallen"), target.PlayerId.ColoredPlayerName());
                    TeamData[targetTeam].Players.Select(x => x.GetPlayer()).DoIf(x => x != null, x => x.Notify(notify));
                    break;
            }

            PlayerData[killer.PlayerId].TagCount++;
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: killer);
        }

        public static void TryPickUpFlag(PlayerControl pc)
        {
            if (!ValidTag) return;
            Logger.Info($"Received flag pickup request from {pc.GetRealName()}", "CTF");
            // If the player is near the enemy's flag, pick it up
            var pos = pc.Pos();
            var enemy = TeamData[PlayerTeams[pc.PlayerId].GetOppositeTeam()];
            if (enemy.IsNearFlag(pos)) enemy.PickUpFlag(pc.PlayerId);
        }

        public static void ApplyGameOptions(IGameOptions opt)
        {
            AURoleOptions.ShapeshifterCooldown = 1f;
        }

        static Color GetTeamColor(this CTFTeam team)
        {
            return team switch
            {
                CTFTeam.Blue => Color.blue,
                CTFTeam.Yellow => Color.yellow,
                _ => Color.white
            };
        }

        static string GetTeamName(this CTFTeam team)
        {
            return team switch
            {
                CTFTeam.Blue => Translator.GetString("CTF_BlueTeamWins"),
                CTFTeam.Yellow => Translator.GetString("CTF_YellowTeamWins"),
                _ => string.Empty
            };
        }

        static CTFTeam GetOppositeTeam(this CTFTeam team)
        {
            return team switch
            {
                CTFTeam.Blue => CTFTeam.Yellow,
                CTFTeam.Yellow => CTFTeam.Blue,
                _ => CTFTeam.Blue
            };
        }

        static (Vector2 Position, string RoomName) GetFlagBase(this CTFTeam team)
        {
            return team switch
            {
                CTFTeam.Blue => BlueFlagBase,
                CTFTeam.Yellow => YellowFlagBase,
                _ => (Vector2.zero, string.Empty)
            };
        }

        enum CTFTeam
        {
            Blue,
            Yellow
        }

        class CTFTeamData(CTFTeam team, CustomNetObject flag, HashSet<byte> players, byte flagCarrier)
        {
            public CustomNetObject Flag { get; } = flag;
            public HashSet<byte> Players { get; } = players;
            public byte FlagCarrier { get; private set; } = flagCarrier;

            public void SetAsWinner()
            {
                WinnerData = (team.GetTeamColor(), team.GetTeamName());
                CustomWinnerHolder.WinnerIds = Players;
                Logger.Info($"{team} team wins", "CTF");
            }

            public void Update()
            {
                try
                {
                    if (FlagCarrier == byte.MaxValue) return;
                    var flagCarrierPc = FlagCarrier.GetPlayer();
                    if (flagCarrierPc == null || !flagCarrierPc.IsAlive())
                    {
                        DropFlag();
                        return;
                    }

                    Flag.TP(flagCarrierPc.Pos());
                    PlayerData[FlagCarrier].FlagTime += Time.fixedDeltaTime;
                    Utils.NotifyRoles(SpecifySeer: flagCarrierPc, SpecifyTarget: flagCarrierPc);

                    CTFTeam enemy = team.GetOppositeTeam();
                    if (Translator.GetString(Flag.playerControl.GetPlainShipRoom().RoomId.ToString()) == enemy.GetFlagBase().RoomName)
                        TeamData[enemy].SetAsWinner();
                }
                catch
                {
                }
            }

            public void PickUpFlag(byte id)
            {
                if (FlagCarrier == id) return;

                FlagCarrier = id;
                Update();

                Logger.Info($"{id.ColoredPlayerName().RemoveHtmlTags()} picked up the {team} flag", "CTF");

                Main.AllPlayerSpeed[id] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod) - SpeedReductionForFlagCarrier.GetFloat();
                PlayerGameOptionsSender.SetDirty(id);

                if (AlertTeamMembersOfFlagTaken.GetBool())
                {
                    var arrow = ArrowToEnemyFlagCarrier.GetBool();
                    TeamData[team].Players
                        .Select(x => x.GetPlayer())
                        .DoIf(x => x != null, x =>
                        {
                            if (arrow) TargetArrow.Add(x.PlayerId, id);
                            x.Notify(Utils.ColorString(Color.yellow, Translator.GetString("CTF_FlagTaken")));
                        });
                }

                if (AlertTeamMembersOfEnemyFlagTaken.GetBool())
                {
                    var arrow = ArrowToOwnFlagCarrier.GetBool();
                    TeamData[team.GetOppositeTeam()].Players
                        .Select(x => x.GetPlayer())
                        .DoIf(x => x != null, x =>
                        {
                            if (arrow) TargetArrow.Add(x.PlayerId, id);
                            x.Notify(Translator.GetString("CTF_EnemyFlagTaken"));
                        });
                }
            }

            public void DropFlag()
            {
                Logger.Info($"{FlagCarrier.ColoredPlayerName().RemoveHtmlTags()} dropped the {team} flag", "CTF");
                if (ArrowToEnemyFlagCarrier.GetBool() || ArrowToOwnFlagCarrier.GetBool())
                    TeamData.Values.SelectMany(x => x.Players).Do(x => TargetArrow.Remove(x, FlagCarrier));
                FlagCarrier = byte.MaxValue;
                Utils.NotifyRoles();
            }

            public bool IsNearFlag(Vector2 pos) => Vector2.Distance(Flag.Position, pos) < 1f;
        }

        class CTFPlayerData
        {
            public int TagCount { get; set; }
            public float FlagTime { get; set; }
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        class FixedUpdatePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || Options.CurrentGameMode != CustomGameMode.CaptureTheFlag || Main.HasJustStarted || !__instance.IsHost()) return;

                TeamData.Values.Do(x => x.Update());
            }
        }
    }
}