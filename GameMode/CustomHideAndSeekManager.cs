using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using EHR.GameMode.HideAndSeekRoles;
using EHR.Modules;
using HarmonyLib;
using UnityEngine;

namespace EHR
{
    internal static class CustomHideAndSeekManager
    {
        public static int TimeLeft;
        private static long LastUpdate;

        private static OptionItem MaxGameLength;
        private static OptionItem MinNeutrals;
        private static OptionItem MaxNeutrals;

        public static Dictionary<Team, Dictionary<CustomRoles, int>> HideAndSeekRoles = [];

        public static void SetupCustomOption()
        {
            const int id = 69_211_001;
            Color color = new(52, 94, 235, byte.MaxValue);

            MaxGameLength = IntegerOptionItem.Create(id, "FFA_GameTime", new(0, 1200, 10), 600, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(color);

            MinNeutrals = IntegerOptionItem.Create(id + 1, "HNS.MinNeutrals", new(0, 13, 1), 1, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetColor(color);
            MaxNeutrals = IntegerOptionItem.Create(id + 2, "HNS.MaxNeutrals", new(0, 13, 1), 3, TabGroup.GameSettings)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetColor(color);
        }

        public static void Init()
        {
            TimeLeft = MaxGameLength.GetInt() + 8;
            LastUpdate = Utils.TimeStamp;

            Type[] types = Assembly
                .GetExecutingAssembly()
                .GetTypes()
                .Where(t => (typeof(IHideAndSeekRole)).IsAssignableFrom(t) && !t.IsInterface)
                .ToArray();

            var roleEnums = types
                .Select(x => ((CustomRoles)Enum.Parse(typeof(CustomRoles), ignoreCase: true, value: x.Name)))
                .Where(role => role.GetMode() != 0)
                .ToArray();

            HideAndSeekRoles = types
                .Select(x => (IHideAndSeekRole)Activator.CreateInstance(x))
                .Where(x => x != null)
                .Join(roleEnums, x => x.GetType().Name, x => x.ToString(), (Role, Count) => (Count, (Role, Role.Count)))
                .Where(x => x.Item2.Count > 0)
                .OrderBy(x => x.Item1 is CustomRoles.Seeker or CustomRoles.Hider ? 100 : IRandom.Instance.Next(100))
                .GroupBy(x => x.Item2.Role.Team)
                .ToDictionary(x => x.Key, x => x.ToDictionary(y => y.Item1, y => y.Item2.Count));
        }

        public static void AssignRoles(ref Dictionary<PlayerControl, CustomRoles> result)
        {
            List<PlayerControl> allPlayers = [.. Main.AllAlivePlayerControls];
            allPlayers = allPlayers.Shuffle(IRandom.Instance).ToList();

            Dictionary<Team, int> memberNum = new()
            {
                [Team.Neutral] = IRandom.Instance.Next(MinNeutrals.GetInt(), MaxNeutrals.GetInt() + 1),
                [Team.Impostor] = Math.Min(Main.RealOptionsData.GetInt(Int32OptionNames.NumImpostors), 1),
                [Team.Crewmate] = allPlayers.Count
            };

            foreach (var item in Main.SetRoles)
            {
                PlayerControl pc = allPlayers.FirstOrDefault(x => x.PlayerId == item.Key);
                if (pc == null) continue;

                result[pc] = item.Value;
                allPlayers.Remove(pc);

                var role = HideAndSeekRoles.FirstOrDefault(x => x.Value.ContainsKey(item.Value));
                role.Value[item.Value]--;
                memberNum[role.Key]--;

                Logger.Warn($"Pre-Set Role Assigned: {pc.GetRealName()} => {item.Value}", "CustomRoleSelector");
            }

            var playerTeams = EnumHelper.GetAllValues<Team>()[1..]
                .SelectMany(x => Enumerable.Repeat(x, memberNum[x]))
                .Zip(allPlayers)
                .GroupBy(x => x.First, x => x.Second)
                .ToDictionary(x => x.Key, x => x.ToArray());

            foreach ((Team team, Dictionary<CustomRoles, int> roleCounts) in HideAndSeekRoles)
            {
                if (playerTeams[team].Length == 0 || memberNum[team] <= 0) continue;

                foreach ((CustomRoles role, int count) in roleCounts)
                {
                    for (int i = 0; i < count; i++)
                    {
                        if (IRandom.Instance.Next(100) >= role.GetMode()) continue;

                        try
                        {
                            PlayerControl pc = playerTeams[team][0];
                            if (pc == null) continue;

                            result[pc] = role;
                            allPlayers.Remove(pc);
                            playerTeams[team] = playerTeams[team][1..];
                            memberNum[team]--;

                            if (memberNum[team] <= 0) break;
                        }
                        catch (Exception e)
                        {
                            if (e is IndexOutOfRangeException) break;
                            Utils.ThrowException(e);
                        }
                    }

                    if (playerTeams[team].Length == 0 || memberNum[team] <= 0) break;
                }
            }

            foreach (PlayerControl pc in allPlayers.Except(result.Keys).ToArray())
            {
                result[pc] = CustomRoles.Hider;
                memberNum[Team.Crewmate]--;
                allPlayers.Remove(pc);
            }

            if (allPlayers.Count > 0) Logger.Error($"Some players were not assigned a role: {allPlayers.Join(x => x.GetRealName())}", "CustomRoleSelector");
            Logger.Msg($"Roles: {result.Join(x => $"{x.Key.GetRealName()} => {x.Value}")}", "HideAndSeekRoleSelector");
        }

        public static void ApplyGameOptions(IGameOptions opt, PlayerControl pc)
        {
        }

        public static bool KnowTargetRoleColor(PlayerControl seer, PlayerControl target, ref string color)
        {
            if (target.Is(CustomRoles.Seeker))
            {
                color = Main.RoleColors[CustomRoles.Seeker];
                return true;
            }

            return false;
        }

        public static bool HasTasks(GameData.PlayerInfo playerInfo)
        {
            return playerInfo.Object.Is(CustomRoles.Hider);
        }

        public static bool IsRoleTextEnabled(PlayerControl seer, PlayerControl target)
        {
            return target.Is(CustomRoles.Seeker);
        }

        public static string GetSuffixText(PlayerControl seer, PlayerControl target, bool isHUD = false)
        {
            if (seer.PlayerId != target.PlayerId) return string.Empty;

            if (!isHUD && seer.IsModClient()) return string.Empty;
            if (TimeLeft <= 60)
            {
                return $"<color={Main.RoleColors[CustomRoles.Hider]}>{Translator.GetString("TimeLeft")}:</color> {TimeLeft}s";
            }

            var remainingMinutes = TimeLeft / 60;
            var remainingSeconds = $"{(TimeLeft % 60) + 1}";
            if (remainingSeconds.Length == 1) remainingSeconds = $"0{remainingSeconds}";
            return isHUD ? $"{remainingMinutes}:{remainingSeconds}" : $"{string.Format(Translator.GetString("MinutesLeft"), $"{remainingMinutes}-{remainingMinutes}")}";
        }

        public static string GetRoleInfoText(PlayerControl seer)
        {
            return $"<size=90%>{Utils.ColorString(Utils.GetRoleColor(seer.GetCustomRole()), seer.GetRoleInfo())}</size>";
        }

        public static bool CheckForGameEnd(out GameOverReason reason)
        {
            reason = GameOverReason.ImpostorByKill;

            var alivePlayers = Main.AllAlivePlayerControls;

            // If there are 0 players alive, the game is over and only foxes win
            if (alivePlayers.Length == 0)
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
                AddFoxesToWinners();
                return true;
            }

            // If time is up or all hiders have finished their tasks, the game is over and hiders win
            if (TimeLeft <= 0 || alivePlayers.Select(x => x.GetTaskState()).All(x => x.IsTaskFinished))
            {
                reason = GameOverReason.HumansByTask;
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Hider);
                CustomWinnerHolder.WinnerIds.UnionWith(Main.PlayerStates.Where(x => x.Value.MainRole == CustomRoles.Hider).Select(x => x.Key));
                AddFoxesToWinners();
                return true;
            }

            // If there are no hiders left, the game is over and only seekers win
            if (alivePlayers.All(x => x.GetCustomRole() != CustomRoles.Hider))
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Seeker);
                CustomWinnerHolder.WinnerIds.UnionWith(Main.PlayerStates.Where(x => x.Value.MainRole == CustomRoles.Seeker).Select(x => x.Key));
                AddFoxesToWinners();
                return true;
            }

            return false;
        }

        static void AddFoxesToWinners()
        {
            var foxes = Main.PlayerStates.Where(x => x.Value.MainRole == CustomRoles.Fox).Select(x => x.Key).ToList();
            foxes.RemoveAll(x =>
            {
                var pc = Utils.GetPlayerById(x);
                return pc == null || !pc.IsAlive();
            });
            if (foxes.Count == 0) return;
            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Fox);
            CustomWinnerHolder.WinnerIds.UnionWith(foxes);
        }

        public static string GetTaskBarText()
        {
            var text = Main.PlayerStates.Aggregate("<size=80%>", (current, state) => $"{current}{GetStateText(state)}\n");
            return $"{text}</size>\r\n\r\n<#00ffa5>Tasks:</color> {GameData.Instance.CompletedTasks}/{GameData.Instance.TotalTasks}";

            static string GetStateText(KeyValuePair<byte, PlayerState> state)
            {
                string name = Main.AllPlayerNames.GetValueOrDefault(state.Key, $"ID {state.Key}");
                name = Utils.ColorString(Main.PlayerColors.GetValueOrDefault(state.Key, Color.white), name);
                bool isSeeker = state.Value.MainRole == CustomRoles.Seeker;
                bool alive = !state.Value.IsDead;
                string taskCount = Utils.GetTaskCount(state.Key, false);
                string stateText;
                if (isSeeker) stateText = $"({Utils.ColorString(Utils.GetRoleColor(CustomRoles.Seeker), Translator.GetString("Seeker"))})";
                else stateText = alive ? string.Empty : "<#ff1313>DEAD</color>";
                stateText = $"{name} {stateText}";
                return stateText;
            }
        }

        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null || target == null || !killer.Is(CustomRoles.Seeker) || target.Is(CustomRoles.Seeker)) return;

            killer.Kill(target);

            // If the Troll is killed, they win
            if (target.Is(CustomRoles.Troll))
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Troll);
                CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
                AddFoxesToWinners();
            }
        }

        public static void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
        }

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
        class FixedUpdatePatch
        {
            public static void Postfix(PlayerControl __instance)
            {
                if (!AmongUsClient.Instance.AmHost || Options.CurrentGameMode != CustomGameMode.HideAndSeek) return;

                long now = Utils.TimeStamp;
                if (LastUpdate == now) return;
                LastUpdate = now;

                TimeLeft--;

                if ((TimeLeft + 1) % 60 == 0 || TimeLeft <= 60) Utils.NotifyRoles();
            }
        }
    }
}