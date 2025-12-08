using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AmongUs.GameOptions;
using EHR.GameMode.HideAndSeekRoles;
using EHR.Modules;
using HarmonyLib;
using UnityEngine;

namespace EHR;

internal static class CustomHnS
{
    public static int TimeLeft;
    private static long LastUpdate;
    public static bool IsBlindTime;

    private static OptionItem MaxGameLength;
    private static OptionItem MinNeutrals;
    private static OptionItem MaxNeutrals;
    private static OptionItem DangerMeter;
    private static OptionItem PlayersSeeRoles;

    public static Dictionary<Team, Dictionary<CustomRoles, int>> HideAndSeekRoles = [];
    public static Dictionary<byte, (IHideAndSeekRole Interface, CustomRoles Role)> PlayerRoles = [];
    public static Dictionary<byte, byte> ClosestImpostor = [];
    public static Dictionary<byte, int> Danger = [];

    public static List<CustomRoles> AllHnSRoles = [];

    public static int SeekerNum => Math.Max(Main.RealOptionsData.GetInt(Int32OptionNames.NumImpostors), 1);
    public static int MaximumGameLength => MaxGameLength.GetInt();

    public static void SetupCustomOption()
    {
        const int id = 69_211_001;
        Color color = new(52, 94, 235, byte.MaxValue);

        MaxGameLength = new IntegerOptionItem(id, "FFA_GameTime", new(0, 1200, 10), 300, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(color);

        MinNeutrals = new IntegerOptionItem(id + 1, "HNS.MinNeutrals", new(0, 13, 1), 0, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetColor(color);

        MaxNeutrals = new IntegerOptionItem(id + 2, "HNS.MaxNeutrals", new(0, 13, 1), 2, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetColor(color);

        DangerMeter = new BooleanOptionItem(id + 3, "HNS.DangerMeter", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetColor(color);

        PlayersSeeRoles = new BooleanOptionItem(id + 4, "HNS.PlayersSeeRoles", true, TabGroup.GameSettings)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetColor(color);
    }

    public static void Init()
    {
        TimeLeft = MaxGameLength.GetInt();
        LastUpdate = Utils.TimeStamp;

        Type[] types = GetAllHnsRoleTypes();

        AllHnSRoles = GetAllHnsRoles(types);

        HideAndSeekRoles = types
            .Select(x => (IHideAndSeekRole)Activator.CreateInstance(x))
            .Where(x => x != null)
            .Join(AllHnSRoles, x => x.GetType().Name.ToLower(), x => x.ToString().ToLower(), (Interface, Enum) => (Enum, Interface))
            .Where(x => (!x.Enum.OnlySpawnsWithPets() || Options.UsePets.GetBool()) && (x.Enum != CustomRoles.Agent || SeekerNum >= 2) && x.Interface.Count > 0 && (x.Interface.Team == Team.Neutral || x.Interface.Chance > IRandom.Instance.Next(100)))
            .OrderBy(x => x.Enum is CustomRoles.Seeker or CustomRoles.Hider ? 100 : IRandom.Instance.Next(100))
            .GroupBy(x => x.Interface.Team)
            .ToDictionary(x => x.Key, x => x.ToDictionary(y => y.Enum, y => y.Interface.Count));

        PlayerRoles = [];
        ClosestImpostor = [];
    }

    public static void StartSeekerBlindTime()
    {
        Main.AllPlayerKillCooldown.SetAllValues(Seeker.KillCooldown.GetFloat());
        IsBlindTime = true;
        Utils.MarkEveryoneDirtySettingsV4();

        LateTask.New(() =>
        {
            IsBlindTime = false;
            Utils.MarkEveryoneDirtySettingsV4();

            Main.AllAlivePlayerControls
                .Join(PlayerRoles, x => x.PlayerId, x => x.Key, (pc, role) => (pc, role.Value.Interface))
                .Where(x => x.Interface.Team == Team.Impostor)
                .Do(x => x.pc.SetKillCooldown());

            LateTask.New(() => Main.Instance.StartCoroutine(Utils.NotifyEveryoneAsync(noCache: false)), 3f, log: false);
        }, Seeker.BlindTime.GetFloat() + 14f, "Blind Time Expire");
    }

    public static List<CustomRoles> GetAllHnsRoles(IEnumerable<Type> types)
    {
        return types
            .Select(x => Enum.Parse<CustomRoles>(ignoreCase: true, value: x.Name))
            .Where(role => role is CustomRoles.Seeker or CustomRoles.Hider || role.GetMode() != 0)
            .ToList();
    }

    public static Type[] GetAllHnsRoleTypes()
    {
        return Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(IHideAndSeekRole).IsAssignableFrom(t) && !t.IsInterface)
            .ToArray();
    }

    public static void AssignRoles()
    {
        Dictionary<PlayerControl, CustomRoles> result = [];
        List<PlayerControl> allPlayers = [.. Main.AllPlayerControls];

        if (Main.GM.Value) allPlayers.RemoveAll(x => x.AmOwner);
        allPlayers.RemoveAll(x => ChatCommands.Spectators.Contains(x.PlayerId));

        allPlayers.Shuffle();

        Dictionary<Team, int> memberNum = new()
        {
            [Team.Neutral] = IRandom.Instance.Next(MinNeutrals.GetInt(), MaxNeutrals.GetInt() + 1),
            [Team.Impostor] = SeekerNum
        };

        memberNum[Team.Crewmate] = allPlayers.Count - memberNum.Values.Sum();

        Logger.Warn($"Number of impostors: {memberNum[Team.Impostor]}", "HnsRoleAssigner");

        foreach (KeyValuePair<byte, CustomRoles> item in Main.SetRoles)
        {
            try
            {
                PlayerControl pc = allPlayers.FirstOrDefault(x => x.PlayerId == item.Key);
                if (pc == null) continue;

                result[pc] = item.Value;
                allPlayers.RemoveAll(x => x.PlayerId == item.Key);

                KeyValuePair<Team, Dictionary<CustomRoles, int>> role = HideAndSeekRoles.FirstOrDefault(x => x.Value.ContainsKey(item.Value));
                role.Value[item.Value]--;
                memberNum[role.Key]--;

                Logger.Warn($"Pre-Set Role Assigned: {pc.GetRealName()} => {item.Value}", "HnsRoleAssigner");
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }
        }

        Dictionary<Team, PlayerControl[]> playerTeams = Enum.GetValues<Team>()[1..4]
            .SelectMany(x => Enumerable.Repeat(x, Math.Max(memberNum[x], 0)))
            .Shuffle()
            .Zip(allPlayers)
            .GroupBy(x => x.First, x => x.Second)
            .ToDictionary(x => x.Key, x => x.ToArray());

        if (memberNum[Team.Neutral] > 0 && HideAndSeekRoles.TryGetValue(Team.Neutral, out Dictionary<CustomRoles, int> neutrals))
            HideAndSeekRoles[Team.Neutral] = neutrals.Shuffle().ToDictionary(x => x.Key, x => x.Value);

        foreach ((Team team, Dictionary<CustomRoles, int> roleCounts) in HideAndSeekRoles)
        {
            try
            {
                if (playerTeams[team].Length == 0 || memberNum[team] <= 0) continue;
            }
            catch (KeyNotFoundException) { continue; }

            foreach ((CustomRoles role, int count) in roleCounts)
            {
                for (var i = 0; i < count; i++)
                {
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
            Logger.Warn($"Unassigned, force Hider: {pc.GetRealName()} => {CustomRoles.Hider}", "HnsRoleAssigner");
            result[pc] = CustomRoles.Hider;
            memberNum[Team.Crewmate]--;
            allPlayers.Remove(pc);
        }

        if (allPlayers.Count > 0) Logger.Error($"Some players were not assigned a role: {allPlayers.Join(x => x.GetRealName())}", "HnsRoleAssigner");

        Logger.Msg($"Roles: {result.Join(x => $"{x.Key.GetRealName()} => {x.Value}")}", "HnsRoleAssigner");

        Dictionary<string, IHideAndSeekRole> roleInterfaces = Assembly.GetExecutingAssembly().GetTypes()
            .Where(x => typeof(IHideAndSeekRole).IsAssignableFrom(x) && !x.IsInterface)
            .Select(x => (IHideAndSeekRole)Activator.CreateInstance(x))
            .Where(x => x != null)
            .ToDictionary(x => x.GetType().Name, x => x);

        PlayerRoles = result.ToDictionary(x => x.Key.PlayerId, x => (roleInterfaces[x.Value.ToString()], x.Value));

        if (Main.GM.Value)
        {
            PlayerControl lp = PlayerControl.LocalPlayer;
            result[lp] = CustomRoles.GM;
            PlayerRoles[lp.PlayerId] = (new Hider(), CustomRoles.GM);
        }

        foreach (byte spectator in ChatCommands.Spectators)
            PlayerRoles[spectator] = (new Hider(), CustomRoles.GM);

        LateTask.New(() => result.IntersectBy(Main.PlayerStates.Keys, x => x.Key.PlayerId).Do(x => x.Key.RpcSetCustomRole(x.Value)), 5f, log: false);

        // ==================================================================================================================

        if (result.ContainsValue(CustomRoles.Agent))
        {
            byte agent = result.GetKeyByValue(CustomRoles.Agent).PlayerId;
            PlayerRoles.DoIf(x => x.Value.Role != CustomRoles.Agent && x.Value.Interface.Team == Team.Impostor, x => TargetArrow.Add(x.Key, agent));
        }
    }

    public static void ApplyGameOptions(IGameOptions opt, PlayerControl pc)
    {
        (IHideAndSeekRole Interface, CustomRoles Role) role = PlayerRoles.GetValueOrDefault(pc.PlayerId);
        bool blind = role.Interface.Team == Team.Impostor && IsBlindTime;
        Main.AllPlayerSpeed[pc.PlayerId] = blind ? Main.MinSpeed : role.Interface.RoleSpeed;
        opt.SetFloat(FloatOptionNames.CrewLightMod, blind ? 0f : role.Interface.RoleVision);
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, blind ? 0f : role.Interface.RoleVision);
        opt.SetFloat(FloatOptionNames.PlayerSpeedMod, Main.AllPlayerSpeed[pc.PlayerId]);
    }

    public static bool KnowTargetRoleColor(PlayerControl seer, PlayerControl target, ref string color)
    {
        if (seer.PlayerId == target.PlayerId) return true;

        if (!PlayerRoles.TryGetValue(target.PlayerId, out (IHideAndSeekRole Interface, CustomRoles Role) targetRole)) return false;
        if (!PlayerRoles.TryGetValue(seer.PlayerId, out (IHideAndSeekRole Interface, CustomRoles Role) seerRole)) return false;

        if (PlayersSeeRoles.GetBool())
        {
            color = Main.RoleColors[targetRole.Role];
            if (targetRole.Role == CustomRoles.Agent) color = Main.RoleColors[CustomRoles.Hider];

            return true;
        }

        if (targetRole.Interface.Team == Team.Impostor && (targetRole.Role != CustomRoles.Agent || seerRole.Interface.Team == Team.Impostor))
        {
            color = Main.RoleColors[CustomRoles.Seeker];
            return true;
        }

        return false;
    }

    public static bool HasTasks(NetworkedPlayerInfo playerInfo)
    {
        if (!AmongUsClient.Instance.AmHost && playerInfo.Object.AmOwner) return PlayerControl.LocalPlayer.GetCustomRole() is CustomRoles.Taskinator or CustomRoles.Hider or CustomRoles.Jet or CustomRoles.Detector or CustomRoles.Jumper;

        if (!PlayerRoles.TryGetValue(playerInfo.PlayerId, out (IHideAndSeekRole Interface, CustomRoles Role) role)) return false;

        return role.Interface.Team == Team.Crewmate || role.Role == CustomRoles.Taskinator;
    }

    public static bool IsRoleTextEnabled(PlayerControl seer, PlayerControl target)
    {
        if (seer.PlayerId == target.PlayerId || PlayersSeeRoles.GetBool()) return true;

        if (!PlayerRoles.TryGetValue(target.PlayerId, out (IHideAndSeekRole Interface, CustomRoles Role) targetRole)) return false;
        if (!PlayerRoles.TryGetValue(seer.PlayerId, out (IHideAndSeekRole Interface, CustomRoles Role) seerRole)) return false;

        return targetRole.Interface.Team == Team.Impostor && (targetRole.Role != CustomRoles.Agent || seerRole.Interface.Team == Team.Impostor);
    }

    public static string GetSuffixText(PlayerControl seer, PlayerControl target, bool hud = false)
    {
        if (GameStates.IsLobby || Options.CurrentGameMode != CustomGameMode.HideAndSeek || Main.HasJustStarted || seer.PlayerId != target.PlayerId || (seer.IsHost() && !hud) || TimeLeft < 0) return string.Empty;

        string dangerMeter = GetDangerMeter(seer);

        if (PlayerRoles.TryGetValue(seer.PlayerId, out (IHideAndSeekRole Interface, CustomRoles Role) seerRole) && seerRole.Interface.Team == Team.Impostor && PlayerRoles.FindFirst(x => x.Value.Role == CustomRoles.Agent, out KeyValuePair<byte, (IHideAndSeekRole Interface, CustomRoles Role)> kvp))
        {
            byte agent = kvp.Key;
            dangerMeter += TargetArrow.GetArrows(seer, agent);
        }

        if (TimeLeft == 60)
        {
            SoundManager.Instance.PlaySound(HudManager.Instance.LobbyTimerExtensionUI.lobbyTimerPopUpSound, false);
            Utils.FlashColor(new(1f, 1f, 0f, 0.4f), 1.4f);
        }

        if (TimeLeft <= 60) return $"{dangerMeter}\n<color={Main.RoleColors[CustomRoles.Hider]}>{Translator.GetString("TimeLeft")}:</color> {TimeLeft}s";

        int minutes = TimeLeft / 60;
        int seconds = TimeLeft % 60;
        return dangerMeter + "\n" + (hud ? $"{minutes:00}:{seconds:00}" : $"{string.Format(Translator.GetString("MinutesLeft"), $"{minutes}-{minutes + 1}")}");
    }

    private static string GetDangerMeter(PlayerControl seer)
    {
        return Danger.TryGetValue(seer.PlayerId, out int danger)
            ? danger <= 5
                ? $"\n<color={GetColorFromDanger()}>{new('\u25a0', 5 - danger)}{new('\u25a1', danger)}</color>"
                : $"\n<color=#ffffff>{new('\u25a1', 5)}</color>"
            : string.Empty;

        string GetColorFromDanger() // 0: Highest, 4: Lowest
            =>
                danger switch
                {
                    0 => "#ff1313",
                    1 => "#ff6a00",
                    2 => "#ffaa00",
                    3 => "#ffea00",
                    4 => "#ffff00",
                    _ => "#ffffff"
                };
    }

    public static string GetRoleInfoText(PlayerControl seer)
    {
        return $"<size=90%>{Utils.ColorString(Utils.GetRoleColor(seer.GetCustomRole()), seer.GetRoleInfo())}</size>";
    }

    public static bool CheckForGameEnd(out GameOverReason reason)
    {
        reason = GameOverReason.ImpostorsByKill;

        PlayerControl[] alivePlayers = Main.AllAlivePlayerControls;

        // If there are 0 players alive, the game is over and only foxes win
        if (alivePlayers.Length == 0)
        {
            reason = GameOverReason.CrewmateDisconnect;
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
            AddFoxesToWinners();
            return true;
        }

        // If there are no crew roles left, the game is over and only impostors win
        if (alivePlayers.All(x => PlayerRoles.TryGetValue(x.PlayerId, out var role) && role.Interface.Team != Team.Crewmate))
        {
            reason = GameOverReason.HideAndSeek_ImpostorsByKills;
            SetWinners(CustomWinner.Seeker, Team.Impostor);
            return true;
        }

        // If time is up or there are no impostors in the game, the game is over and crewmates win
        if (TimeLeft <= 0 || PlayerRoles.Values.All(x => x.Interface.Team != Team.Impostor))
        {
            reason = TimeLeft <= 0 ? GameOverReason.HideAndSeek_CrewmatesByTimer : GameOverReason.ImpostorDisconnect;
            SetWinners(CustomWinner.Hider, Team.Crewmate);
            return true;
        }

        return false;

        static void SetWinners(CustomWinner winner, Team team)
        {
            CustomWinnerHolder.ResetAndSetWinner(winner);
            CustomWinnerHolder.WinnerIds.UnionWith(PlayerRoles.Where(x => x.Value.Interface.Team == team).Select(x => x.Key));
            AddFoxesToWinners();
        }
    }

    public static void AddFoxesToWinners()
    {
        List<byte> foxes = Main.PlayerStates.Values.Where(x => x.MainRole == CustomRoles.Fox && x.Player != null && x.Player.IsAlive()).Select(x => x.Player.PlayerId).ToList();
        if (foxes.Count == 0) return;

        CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Fox);
        CustomWinnerHolder.WinnerIds.UnionWith(foxes);
    }

    public static string GetTaskBarText()
    {
        string text = Main.PlayerStates.IntersectBy(PlayerRoles.Keys, x => x.Key).Aggregate("<size=80%>", (current, state) => $"{current}{GetStateText(state)}\n");
        return $"{text}</size>\r\n\r\n<#00ffa5>{Translator.GetString("HNS.TaskCount")}</color> {GameData.Instance.CompletedTasks}/{GameData.Instance.TotalTasks}";

        static string GetStateText(KeyValuePair<byte, PlayerState> state)
        {
            string name = Main.AllPlayerNames.GetValueOrDefault(state.Key, $"ID {state.Key}");
            name = Utils.ColorString(Main.PlayerColors.GetValueOrDefault(state.Key, Color.white), name);
            bool isSeeker = PlayerRoles[state.Key].Interface.Team == Team.Impostor;
            bool alive = !state.Value.IsDead;

            TaskState ts = state.Value.TaskState;
            var stateText = string.Empty;

            if (PlayersSeeRoles.GetBool())
                stateText = $" ({GetRole().ToColoredString()}){GetTaskCount()}";
            else if (isSeeker) stateText = $" ({CustomRoles.Seeker.ToColoredString()})";

            if (!alive) stateText += $"  <color=#ff0000>{Translator.GetString("Dead")}</color>";

            stateText = $"{name}{stateText}";
            return stateText;

            CustomRoles GetRole() => state.Value.MainRole == CustomRoles.Agent ? CustomRoles.Hider : state.Value.MainRole;

            string GetTaskCount() => CustomRoles.Agent.IsEnable() || !ts.HasTasks ? string.Empty : $" ({ts.CompletedTasksCount}/{ts.AllTasksCount})";
        }
    }

    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || PlayerRoles[killer.PlayerId].Interface.Team != Team.Impostor || PlayerRoles[target.PlayerId].Interface.Team == Team.Impostor || IsBlindTime) return;

        killer.Kill(target);

        if (Main.GM.Value && AmongUsClient.Instance.AmHost) PlayerControl.LocalPlayer.KillFlash();
        ChatCommands.Spectators.ToValidPlayers().Do(x => x.KillFlash());

        // If the Troll is killed, they win
        if (target.Is(CustomRoles.Troll))
        {
            CustomSoundsManager.RPCPlayCustomSoundAll("Congrats");
            CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Troll);
            CustomWinnerHolder.WinnerIds.Add(target.PlayerId);
            AddFoxesToWinners();
        }
    }

    //[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class FixedUpdatePatch
    {
        public static void Postfix()
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsInTask || ExileController.Instance || Options.CurrentGameMode != CustomGameMode.HideAndSeek || Main.HasJustStarted) return;

            long now = Utils.TimeStamp;
            if (LastUpdate == now) return;
            LastUpdate = now;

            TimeLeft--;

            PlayerRoles = PlayerRoles.IntersectBy(Main.AllPlayerControls.Select(x => x.PlayerId), x => x.Key).ToDictionary(x => x.Key, x => x.Value);

            try
            {
                Dictionary<byte, Vector2> idPosPairs = PlayerRoles.Join(Main.AllPlayerControls, x => x.Key, x => x.PlayerId, (role, pc) => (role.Key, pc)).ToDictionary(x => x.Key, x => x.pc.Pos());
                Dictionary<byte, Vector2> imps = PlayerRoles.Where(x => x.Value.Interface.Team == Team.Impostor).ToDictionary(x => x.Key, x => idPosPairs[x.Key]);
                KeyValuePair<byte, (IHideAndSeekRole Interface, CustomRoles Role)>[] nonImps = PlayerRoles.Where(x => x.Value.Interface.Team is Team.Crewmate or Team.Neutral).ToArray();
                ClosestImpostor = nonImps.ToDictionary(x => x.Key, x => imps.MinBy(y => Vector2.Distance(y.Value, idPosPairs[x.Key])).Key);
                Danger = nonImps.ToDictionary(x => x.Key, x => Math.Clamp(((1 + (int)Math.Ceiling(Vector2.Distance(idPosPairs[x.Key], idPosPairs[ClosestImpostor[x.Key]]))) / 3) - 1, 0, 5));
            }
            catch { }

            if (DangerMeter.GetBool() || (TimeLeft + 1) % 60 == 0 || TimeLeft <= 60)
                Utils.NotifyRoles();
        }
    }
}
