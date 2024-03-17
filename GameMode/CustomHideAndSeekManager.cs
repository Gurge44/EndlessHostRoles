using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AmongUs.GameOptions;
using HarmonyLib;
using TOHE.GameMode.HideAndSeekRoles;
using TOHE.Modules;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace TOHE
{
    internal static class CustomHideAndSeekManager
    {
        public static int TimeLeft;
        private static long LastUpdate;

        private static OptionItem MaxGameLength;

        public static void SetupCustomOption()
        {
            const int id = 69_211_001;
            Color color = new(52, 94, 235, byte.MaxValue);

            MaxGameLength = IntegerOptionItem.Create(id, "FFA_GameTime", new(0, 1200, 10), 600, TabGroup.GameSettings, false)
                .SetGameMode(CustomGameMode.HideAndSeek)
                .SetValueFormat(OptionFormat.Seconds)
                .SetColor(color);
        }

        public static void Init()
        {
            TimeLeft = MaxGameLength.GetInt() + 8;
            LastUpdate = Utils.TimeStamp;
        }

        public static void AssignRoles(ref Dictionary<PlayerControl, CustomRoles> result)
        {
            List<PlayerControl> allPlayers = [.. Main.AllAlivePlayerControls];
            allPlayers.Shuffle(IRandom.Instance);

            int seekerNum = Main.RealOptionsData.GetInt(Int32OptionNames.NumImpostors);

            Dictionary<CustomRoles, int> roles = Assembly
                .GetExecutingAssembly()
                .GetTypes()
                .Where(t => (typeof(IHideAndSeekRole)).IsAssignableFrom(t) && !t.IsInterface)
                .Select(x => ((CustomRoles)Enum.Parse(typeof(CustomRoles), ignoreCase: true, value: x.Name)))
                .Where(role => role.GetMode() != 0)
                .ToDictionary(x => x, x => x.GetCount());

            while (seekerNum > 0 && allPlayers.Count > 0)
            {
                var pc = allPlayers[0];
                result[pc] = CustomRoles.Seeker;
                allPlayers.Remove(pc);
                seekerNum--;
            }

            if (allPlayers.Count == 0) return;

            bool stop = false;
            foreach (var role in roles)
            {
                for (int i = 0; i < role.Value; i++)
                {
                    var pc = allPlayers[0];
                    result[pc] = role.Key;
                    allPlayers.Remove(pc);

                    if (allPlayers.Count == 0)
                    {
                        stop = true;
                        break;
                    }

                    if (IRandom.Instance.Next(2) == 0) break;
                }

                if (stop) break;
            }

            if (allPlayers.Count == 0) return;

            foreach (var pc in allPlayers)
            {
                result[pc] = CustomRoles.Hider;
            }
        }

        public static void ApplyGameOptions(IGameOptions opt, PlayerControl pc)
        {
        }

        public static bool KnowTargetRoleColor(PlayerControl seer, PlayerControl target, ref string color)
        {
            if (target.Is(CustomRoles.Seeker))
            {
                color = Main.roleColors[CustomRoles.Seeker];
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
            if (seer.PlayerId == target.PlayerId)
            {
                if (!isHUD && seer.IsModClient()) return string.Empty;
                if (isHUD || TimeLeft <= 60)
                {
                    return $"<color={Main.roleColors[CustomRoles.Hider]}>{Translator.GetString("TimeLeft")}:</color> {TimeLeft}s";
                }
            }

            var remainingMinutes = TimeLeft / 60;
            return $"{string.Format(Translator.GetString("MinutesLeft"), $"{remainingMinutes}-{remainingMinutes + 1}")}";
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
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Hider);
                CustomWinnerHolder.WinnerIds.UnionWith(Main.PlayerStates.Where(x => x.Value.MainRole == CustomRoles.Hider).Select(x => x.Key));
                AddFoxesToWinners();
                return true;
            }

            // If there are no hiders left, the game is over and only seekers win
            if (alivePlayers.All(x => x.GetCustomRole() != CustomRoles.Hider))
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Seeker);
                CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Seeker);
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
            CustomWinnerHolder.WinnerRoles.Add(CustomRoles.Fox);
            CustomWinnerHolder.WinnerIds.UnionWith(foxes);
        }

        public static string GetTaskBarText()
        {
            var text = string.Empty;
            foreach (var state in Main.PlayerStates)
            {
                string name = Main.AllPlayerNames.GetValueOrDefault(state.Key, string.Empty);
                if (name == string.Empty) continue;
                name = Utils.ColorString(Main.PlayerColors.GetValueOrDefault(state.Key, Color.white), name);
                bool isSeeker = state.Value.MainRole == CustomRoles.Seeker;
                bool alive = !state.Value.IsDead;
                string taskCount = Utils.GetTaskCount(state.Key, false);
                string stateText = isSeeker ? $"({Translator.GetString("Seeker")})" : $"{taskCount}";
                text += $"{name} {stateText}";
            }

            return text;
        }

        public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null || target == null) return;

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
