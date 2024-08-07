using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace EHR
{
    public static class CTFManager
    {
        private static OptionItem AlertTeamMembersOfFlagTaken;
        private static OptionItem TaggedPlayersGet;
        private static OptionItem WhenFlagCarrierGetsTagged;

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
        private static bool ValidTag;

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

            TaggedPlayersGet = new StringOptionItem(id + 1, "CTF_TaggedPlayersGet", TaggedPlayersGetOptions, 0, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.CaptureTheFlag)
                .SetColor(color);

            WhenFlagCarrierGetsTagged = new StringOptionItem(id + 2, "CTF_WhenFlagCarrierGetsTagged", WhenFlagCarrierGetsTaggedOptions, 0, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.CaptureTheFlag)
                .SetColor(color);
        }

        public static bool KnowTargetRoleColor(PlayerControl seer, PlayerControl target, ref string color)
        {
            Color32 teamColor = PlayerTeams[target.PlayerId].GetTeamColor();
            color = $"#{teamColor.r:x2}{teamColor.g:x2}{teamColor.b:x2}{teamColor.a:x2}";
            return true;
        }

        public static string GetSuffixText(PlayerControl seer, PlayerControl target)
        {
            if (seer.PlayerId != target.PlayerId) return string.Empty;
            return $"<size=1.4>{GetStatistics(target.PlayerId).Replace(" - ", "\n")}</size>";
        }

        public static string GetStatistics(byte id)
        {
            if (!PlayerData.TryGetValue(id, out var stats)) return string.Empty;
            return string.Format(Translator.GetString("CTF_PlayerStats"), Math.Round(stats.FlagTime, 1), stats.TagCount);
        }

        public static int GetFlagTime(byte id)
        {
            return (int)Math.Round(PlayerData[id].FlagTime);
        }

        public static int GetTagCount(byte id)
        {
            return PlayerData[id].TagCount;
        }

        public static bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;
            var aapc = Main.AllAlivePlayerControls;

            switch (aapc.Length)
            {
                case 0:
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Draw);
                    CustomWinnerHolder.WinnerIds = Main.PlayerStates.Keys.ToHashSet();
                    reason = GameOverReason.HumansDisconnect;
                    return true;
                case 1:
                    TeamData[PlayerTeams[aapc[0].PlayerId]].SetAsWinner();
                    return true;
                default:
                    // If WinnerData is already set, end the game
                    if (WinnerData.Team != "No one wins") return true;

                    // If all players are on the same team, end the game
                    if (aapc.All(x => PlayerTeams[x.PlayerId] == CTFTeam.Blue) || aapc.All(x => PlayerTeams[x.PlayerId] == CTFTeam.Yellow))
                    {
                        TeamData[PlayerTeams[aapc[0].PlayerId]].SetAsWinner();
                        return true;
                    }

                    return false;
            }
        }

        public static void OnGameStart()
        {
            // Reset all data
            PlayerTeams = [];
            TeamData = [];
            WinnerData = (Color.white, "No one wins");
            PlayerData = Main.PlayerStates.Keys.ToDictionary(x => x, _ => new CTFPlayerData());
            ValidTag = false;

            // Check if the current game mode is Capture The Flag
            if (Options.CurrentGameMode != CustomGameMode.CaptureTheFlag) return;

            // Assign players to teams
            List<PlayerControl> players = Main.AllAlivePlayerControls.ToList();
            int blueCount = players.Count / 2;
            int yellowCount = players.Count - blueCount;
            HashSet<byte> bluePlayers = [];
            HashSet<byte> yellowPlayers = [];

            for (int i = 0; i < blueCount; i++)
            {
                PlayerControl player = players.RandomElement();
                players.Remove(player);
                PlayerTeams[player.PlayerId] = CTFTeam.Blue;
                bluePlayers.Add(player.PlayerId);
                Utils.RpcChangeSkin(player, BlueOutfit);
            }

            foreach (PlayerControl player in players)
            {
                PlayerTeams[player.PlayerId] = CTFTeam.Yellow;
                yellowPlayers.Add(player.PlayerId);
                Utils.RpcChangeSkin(player, YellowOutfit);
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
            LateTask.New(() =>
            {
                foreach (var pc in Main.AllAlivePlayerControls)
                {
                    if (PlayerTeams.TryGetValue(pc.PlayerId, out var team))
                    {
                        switch (team)
                        {
                            case CTFTeam.Blue:
                                pc.TP(blueFlagBase.Position);
                                pc.Notify(string.Format(Translator.GetString("CTF_Notify_EnemyTeamRoom"), yellowFlagBase.RoomName));
                                break;
                            case CTFTeam.Yellow:
                                pc.TP(yellowFlagBase.Position);
                                pc.Notify(string.Format(Translator.GetString("CTF_Notify_EnemyTeamRoom"), blueFlagBase.RoomName));
                                break;
                        }
                    }
                }

                ValidTag = true;
            }, 10f, "CTFManager.OnGameStart");
        }

        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!ValidTag) return;

            if (TeamData.FindFirst(x => x.Value.FlagCarrier == target.PlayerId, out var kvp))
            {
                kvp.Value.DropFlag();
                if (WhenFlagCarrierGetsTagged.GetValue() == 1)
                {
                    kvp.Value.Flag.TP(kvp.Key.GetOppositeTeam().GetFlagBase().Position);
                }
            }

            switch (TaggedPlayersGet.GetValue())
            {
                case 0:
                    target.TP(PlayerTeams[target.PlayerId].GetFlagBase().Position);
                    break;
                case 1:
                    target.Suicide();
                    break;
            }

            PlayerData[killer.PlayerId].TagCount++;
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: killer);
        }

        public static void OnPet(PlayerControl pc)
        {
            if (!ValidTag) return;
            Logger.Info($"{pc.GetRealName()} petted their pet", "CTF.OnPet");
            // If the player is near the enemy's flag, pick it up
            var pos = pc.Pos();
            var enemy = TeamData[PlayerTeams[pc.PlayerId].GetOppositeTeam()];
            if (enemy.IsNearFlag(pos)) enemy.PickUpFlag(pc.PlayerId);
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
            private HashSet<byte> Players { get; } = players;
            public byte FlagCarrier { get; private set; } = flagCarrier;

            public void SetAsWinner()
            {
                WinnerData = (team.GetTeamColor(), team.GetTeamName());
                CustomWinnerHolder.WinnerIds = Players;
            }

            public void Update()
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

            public void PickUpFlag(byte id)
            {
                if (FlagCarrier == id) return;

                FlagCarrier = id;
                Update();

                if (AlertTeamMembersOfFlagTaken.GetBool())
                {
                    TeamData[team].Players
                        .Select(x => x.GetPlayer())
                        .DoIf(x => x != null, x => x.Notify(Translator.GetString("CTF_FlagTaken")));
                }
            }

            public void DropFlag() => FlagCarrier = byte.MaxValue;

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