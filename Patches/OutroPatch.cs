using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using EHR.Gamemodes;
using EHR.Modules;
using EHR.Roles;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
internal static class EndGamePatch
{
    public static Dictionary<byte, string> SummaryText = [];
    public static string KillLog = string.Empty;

    public static void Postfix()
    {
        GameStates.InGame = false;

        Logger.Info("-----------Game over-----------", "Phase");

        ChatCommands.DraftResult = [];
        ChatCommands.DraftRoles = [];
        Main.SetRoles = [];
        Main.SetAddOns = [];
        SummaryText = [];
        Main.LastAddOns = [];

        foreach ((byte id, PlayerState state) in Main.PlayerStates)
        {
            if (Doppelganger.PlayerIdList.Count > 0 && Doppelganger.DoppelVictim.ContainsKey(id))
            {
                PlayerControl dpc = Utils.GetPlayerById(id);

                if (dpc != null)
                {
                    dpc.RpcSetName(Doppelganger.DoppelVictim[id]);
                    Main.AllPlayerNames[id] = Doppelganger.DoppelVictim[id];
                }
            }

            SummaryText[id] = Utils.SummaryTexts(id, false);
            if (state.SubRoles.Count == 0) continue;

            Main.LastAddOns[id] = $"<size=70%>{id.ColoredPlayerName()}: {state.SubRoles.Join(x => x.ToColoredString())}</size>";
        }

        if (Options.DumpLogAfterGameEnd.GetBool()) Utils.DumpLog(false);

        StringBuilder sb = new(GetString("KillLog"));
        sb.Append(':');
        sb.Append("<size=70%>");

        foreach ((byte key, PlayerState value) in Main.PlayerStates.OrderBy(x => x.Value.RealKiller.TimeStamp.Ticks))
        {
            DateTime date = value.RealKiller.TimeStamp;
            if (date == DateTime.MinValue) continue;

            long secondsIn = new DateTimeOffset(date.ToUniversalTime()).ToUnixTimeSeconds() - IntroCutsceneDestroyPatch.IntroDestroyTS;
            byte killerId = value.GetRealKiller();
            bool gmIsFm = Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.StopAndGo;
            bool gmIsFmhh = gmIsFm || Options.CurrentGameMode is CustomGameMode.HotPotato or CustomGameMode.HideAndSeek or CustomGameMode.Speedrun or CustomGameMode.CaptureTheFlag or CustomGameMode.NaturalDisasters or CustomGameMode.RoomRush or CustomGameMode.KingOfTheZones or CustomGameMode.Quiz or CustomGameMode.TheMindGame or CustomGameMode.BedWars or CustomGameMode.Deathrace or CustomGameMode.Mingle or CustomGameMode.Snowdown;
            sb.Append($"\n{secondsIn / 60:00}:{secondsIn % 60:00} {Main.AllPlayerNames[key]} ({(gmIsFmhh ? string.Empty : Utils.GetDisplayRoleName(key, true))}{(gmIsFm ? string.Empty : Utils.GetSubRolesText(key, summary: true))}) [{Utils.GetVitalText(key)}]");
            if (killerId != byte.MaxValue && killerId != key) sb.Append($"\n\t⇐ {Main.AllPlayerNames[killerId]} ({(gmIsFmhh ? string.Empty : Utils.GetDisplayRoleName(killerId, true))}{(gmIsFm ? string.Empty : Utils.GetSubRolesText(killerId, summary: true))})");
        }

        KillLog = sb.Append("</size>").ToString();
        if (!KillLog.Contains('\n')) KillLog = string.Empty;

        Main.NormalOptions.KillCooldown = Options.DefaultKillCooldown;

        EndGameResult.CachedWinners = new();

        HashSet<PlayerControl> winner = Main.EnumeratePlayerControls().Where(pc => CustomWinnerHolder.WinnerIds.Contains(pc.PlayerId)).ToHashSet();

        foreach (CustomRoles team in CustomWinnerHolder.WinnerRoles)
            winner.UnionWith(Main.EnumeratePlayerControls().Where(p => p.Is(team) && !winner.Contains(p)));

        Main.WinnerNameList = [];
        Main.WinnerList = [];

        foreach (PlayerControl pc in winner)
        {
            if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw && pc.Is(CustomRoles.GM)) continue;

            EndGameResult.CachedWinners.Add(new(pc.Data));
            Main.WinnerList.Add(pc.PlayerId);
            Main.WinnerNameList.Add(pc.GetRealName());
        }

        Arsonist.IsDoused = [];
        Revolutionist.IsDraw = [];
        Investigator.IsRevealed = [];

        Main.VisibleTasksCount = false;

        CustomNetObject.Reset();
        Main.LoversPlayers.Clear();
        Bloodmoon.OnMeetingStart();
        AFKDetector.ExemptedPlayers.Clear();

        foreach (PlayerState state in Main.PlayerStates.Values)
            state.Role.Init();

        if (AmongUsClient.Instance.AmHost)
        {
            Main.RealOptionsData.Restore(GameOptionsManager.Instance.CurrentGameOptions);
            GameOptionsSender.AllSenders.Clear();
            GameOptionsSender.AllSenders.Add(new NormalGameOptionsSender());

            ChatCommands.Spectators.Clear();

            Utils.NumSnapToCallsThisRound = 0;
            Main.GameTimer.Reset();

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.Standard:
                    if (GameStates.CurrentServerType == GameStates.ServerType.Vanilla) Main.GamesPlayed.AddRange(Main.EnumeratePlayerControls().ToDictionary(x => x.FriendCode, _ => 0), false);
                    Main.GamesPlayed.AdjustAllValues(x => ++x);
                    Main.GotShieldAnimationInfoThisGame.Clear();
                    if (Main.GM.Value) Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].IsDead = false;
                    break;
                case CustomGameMode.StopAndGo:
                    Main.EnumeratePlayerControls().Do(x => StopAndGo.HasPlayed.Add(x.FriendCode));
                    break;
                case CustomGameMode.RoomRush:
                    Main.EnumeratePlayerControls().Do(x => RoomRush.HasPlayedFriendCodes.Add(x.FriendCode));
                    break;
                case CustomGameMode.KingOfTheZones:
                    Main.EnumeratePlayerControls().Do(x => KingOfTheZones.PlayedFCs.Add(x.FriendCode));
                    break;
                case CustomGameMode.Quiz:
                    Main.EnumeratePlayerControls().Do(x => Quiz.HasPlayedFriendCodes.Add(x.FriendCode));
                    break;
                case CustomGameMode.Deathrace:
                    MapNames map = Main.CurrentMap;
                    
                    foreach (PlayerControl pc in Main.EnumeratePlayerControls())
                    {
                        if (!Deathrace.PlayedMaps.TryGetValue(pc.FriendCode, out var maps))
                            Deathrace.PlayedMaps[pc.FriendCode] = [map];
                        else
                            maps.Add(map);
                    }

                    break;
                case CustomGameMode.Mingle:
                    Main.EnumeratePlayerControls().Do(x => Mingle.HasPlayedFCs.Add(x.FriendCode));
                    break;
                default:
                    if (Main.HasPlayedGM.TryGetValue(Options.CurrentGameMode, out HashSet<string> playedFCs))
                        playedFCs.UnionWith(Main.EnumeratePlayerControls().Select(x => x.FriendCode));

                    break;
            }

            if (Options.AutoGMRotationEnabled)
            {
                Options.AutoGMRotationIndex++;

                if (Options.AutoGMRotationIndex >= Options.AutoGMRotationCompiled.Count)
                {
                    bool includesRandomChoice = Options.AutoGMRotationSlots.Exists(x => x.Slot.GetValue() == 2);

                    if (includesRandomChoice) Options.CompileAutoGMRotationSettings();
                    else Options.AutoGMRotationIndex = 0;
                }
            }
            
            Main.Instance.StartCoroutine(BanManager.LoadEACList(reload: true));
        }
    }
}

[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
internal static class SetEverythingUpPatch
{
    public static string LastWinsText = string.Empty;
    public static string LastWinsReason = string.Empty;
    private static SimpleButton ResultsToggleButton;

    public static void Postfix(EndGameManager __instance)
    {
        //#######################################
        //      ==Victory Faction Display==
        //#######################################

        Main.Instance.StartCoroutine(SetupPoolablePlayers());

        __instance.WinText.alignment = TextAlignmentOptions.Center;
        GameObject winnerTextObject = Object.Instantiate(__instance.WinText.gameObject);
        winnerTextObject.transform.position = new(__instance.WinText.transform.position.x, __instance.WinText.transform.position.y - 0.5f, __instance.WinText.transform.position.z);
        winnerTextObject.transform.localScale = new(0.6f, 0.6f, 0.6f);
        var winnerText = winnerTextObject.GetComponent<TextMeshPro>();
        winnerText.fontSizeMin = 3f;
        winnerText.text = string.Empty;

        var customWinnerText = string.Empty;
        var additionalWinnerText = string.Empty;
        string customWinnerColor = Utils.GetRoleColorCode(CustomRoles.Crewmate);

        if (CustomWinnerHolder.WinnerTeam is not (CustomWinner.None or CustomWinner.Draw or CustomWinner.Error))
        {
            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.SoloPVP:
                {
                    __instance.BackgroundBar.material.color = new Color32(245, 82, 82, 255);
                    customWinnerText = CustomWinnerHolder.WinnerIds.Select(x => x.ColoredPlayerName()).Join() + GetString("Win");
                    customWinnerColor = "#f55252";
                    additionalWinnerText = "\n" + string.Format(GetString("SoloPVP.WinnersKillCount"), SoloPVP.PlayerScore[CustomWinnerHolder.WinnerIds.First()]);
                    goto Skip;
                }
                case CustomGameMode.FFA:
                {
                    byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = new Color32(0, 255, 255, 255);
                    winnerText.text = FreeForAll.FFATeamMode.GetBool() ? string.Empty : Main.AllPlayerNames[winnerId] + GetString("Win");
                    winnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
                case CustomGameMode.StopAndGo:
                {
                    byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = new Color32(0, 255, 165, 255);
                    winnerText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                    winnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
                case CustomGameMode.HotPotato:
                {
                    byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = new Color32(232, 205, 70, 255);
                    winnerText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                    winnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
                case CustomGameMode.Speedrun:
                {
                    byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Speedrunner);
                    winnerText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                    winnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
                case CustomGameMode.CaptureTheFlag:
                {
                    (Color Color, string Team) winnerData = CaptureTheFlag.WinnerData;
                    __instance.BackgroundBar.material.color = winnerData.Color;
                    winnerText.text = winnerData.Team;
                    winnerText.color = winnerData.Color;
                    goto EndOfText;
                }
                case CustomGameMode.NaturalDisasters:
                {
                    var ndColor = new Color32(3, 252, 74, 255);
                    __instance.BackgroundBar.material.color = ndColor;

                    if (CustomWinnerHolder.WinnerIds.Count <= 1)
                    {
                        byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                        winnerText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                        winnerText.color = Main.PlayerColors[winnerId];
                    }
                    else
                    {
                        winnerText.text = CustomWinnerHolder.WinnerIds.Select(x => x.ColoredPlayerName()).Join() + GetString("Win");
                        winnerText.color = ndColor;
                    }
                
                    goto EndOfText;
                }
                case CustomGameMode.RoomRush:
                {
                    byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = new Color32(255, 171, 27, 255);
                    winnerText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                    winnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
                case CustomGameMode.KingOfTheZones:
                {
                    (Color Color, string Team) winnerData = KingOfTheZones.WinnerData;
                    __instance.BackgroundBar.material.color = winnerData.Color;
                    winnerText.text = winnerData.Team;
                    winnerText.color = winnerData.Color;
                    goto EndOfText;
                }
                case CustomGameMode.Quiz:
                {
                    byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.QuizMaster);
                    winnerText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                    winnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
                case CustomGameMode.TheMindGame:
                {
                    __instance.BackgroundBar.material.color = Color.yellow;
                    winnerText.text = CustomWinnerHolder.WinnerIds.Select(x => x.ColoredPlayerName()).Join() + GetString("Win");
                    winnerText.color = Color.yellow;
                    goto EndOfText;
                }
                case CustomGameMode.BedWars:
                {
                    (Color Color, string Team) winnerData = BedWars.WinnerData;
                    __instance.BackgroundBar.material.color = winnerData.Color;
                    winnerText.text = winnerData.Team;
                    winnerText.color = winnerData.Color;
                    goto EndOfText;
                }
                case CustomGameMode.Deathrace:
                {
                    byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                    __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Racer);
                    winnerText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                    winnerText.color = Main.PlayerColors[winnerId];
                    goto EndOfText;
                }
                case CustomGameMode.Mingle:
                {
                    if (CustomWinnerHolder.WinnerIds.Count <= 1)
                    {
                        byte winnerId = CustomWinnerHolder.WinnerIds.FirstOrDefault();
                        __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.MinglePlayer);
                        winnerText.text = Main.AllPlayerNames[winnerId] + GetString("Win");
                        winnerText.color = Main.PlayerColors[winnerId];
                    }
                    else
                    {
                        Color color = Utils.GetRoleColor(CustomRoles.MinglePlayer);
                        __instance.BackgroundBar.material.color = color;
                        winnerText.text = CustomWinnerHolder.WinnerIds.Select(x => x.ColoredPlayerName()).Join() + GetString("Win");
                        winnerText.color = color;
                    }
                    
                    goto EndOfText;
                }
                case CustomGameMode.Snowdown:
                {
                    Color color = Utils.GetRoleColor(CustomRoles.SnowdownPlayer);
                    __instance.BackgroundBar.material.color = color;
                    winnerText.text = (CustomWinnerHolder.WinnerIds.Count <= 1 ? CustomWinnerHolder.WinnerIds.FirstOrDefault().ColoredPlayerName() : CustomWinnerHolder.WinnerIds.Select(x => x.ColoredPlayerName()).Join()) + GetString("Win");
                    winnerText.color = color;
                    goto EndOfText;
                }
            }
        }

        if (CustomWinnerHolder.WinnerTeam == CustomWinner.CustomTeam)
        {
            CustomTeamManager.CustomTeam team = CustomTeamManager.WinnerTeam;
            customWinnerText = string.Format(GetString("CustomWinnerText"), team.TeamName);
            customWinnerColor = team.RoleRevealScreenBackgroundColor == "*" ? Main.NeutralColor : team.RoleRevealScreenBackgroundColor;
            __instance.BackgroundBar.material.color = ColorUtility.TryParseHtmlString(team.RoleRevealScreenBackgroundColor, out Color color) ? color : Utils.GetRoleColor(CustomRoles.Sprayer);
            additionalWinnerText = $"\n{team.TeamMembers.Where(r => Main.PlayerStates.Any(x => x.Value.MainRole == r && CustomWinnerHolder.WinnerIds.Contains(x.Key))).Join(x => x.ToColoredString())}{GetString("Win")}";
            goto Skip;
        }

        var winnerRole = (CustomRoles)CustomWinnerHolder.WinnerTeam;

        if (winnerRole >= 0)
        {
            customWinnerText = GetWinnerRoleName(winnerRole);
            customWinnerColor = Utils.GetRoleColorCode(winnerRole);
            __instance.BackgroundBar.material.color = Utils.GetRoleColor(winnerRole);
        }

        if (Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].MainRole == CustomRoles.GM)
        {
            __instance.WinText.text = GetString("GameOver");
            __instance.WinText.color = Utils.GetRoleColor(CustomRoles.GM);
            __instance.BackgroundBar.material.color = Utils.GetRoleColor(winnerRole);
        }

        switch (CustomWinnerHolder.WinnerTeam)
        {
            case CustomWinner.Crewmate:
                customWinnerColor = Utils.GetRoleColorCode(CustomRoles.Crewmate);
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Crewmate);
                break;
            case CustomWinner.Impostor:
                customWinnerColor = Utils.GetRoleColorCode(CustomRoles.Impostor);
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Impostor);
                break;
            case CustomWinner.Egoist:
                customWinnerColor = Utils.GetRoleColorCode(CustomRoles.Egoist);
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Egoist);
                break;
            case CustomWinner.Terrorist:
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Terrorist);
                break;
            case CustomWinner.Lovers:
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Lovers);
                break;
            case CustomWinner.Phantasm:
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Phantasm);
                break;
            case CustomWinner.Draw:
                __instance.WinText.text = GetString("ForceEnd");
                __instance.WinText.color = Color.white;
                __instance.BackgroundBar.material.color = Color.gray;
                winnerText.text = GetString("ForceEndText");
                winnerText.color = Color.gray;
                break;
            case CustomWinner.Neutrals:
                __instance.WinText.text = GetString("DefeatText");
                __instance.WinText.color = Utils.GetRoleColor(CustomRoles.Impostor);
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Executioner);
                winnerText.text = GetString("NeutralsLeftText");
                winnerText.color = Utils.GetRoleColor(CustomRoles.Executioner);
                break;
            case CustomWinner.None:
                __instance.WinText.text = string.Empty;
                __instance.WinText.color = Color.black;
                __instance.BackgroundBar.material.color = Color.gray;
                winnerText.text = GetString(Main.GameEndDueToTimer ? "GameTimerEnded" : "EveryoneDied");
                winnerText.color = Color.gray;
                break;
            case CustomWinner.Error:
                __instance.WinText.text = GetString("ErrorEndText");
                __instance.WinText.color = Color.red;
                __instance.BackgroundBar.material.color = Color.red;
                winnerText.text = GetString("ErrorEndTextDescription");
                winnerText.color = Color.white;
                break;
        }

        foreach (AdditionalWinners additionalWinners in CustomWinnerHolder.AdditionalWinnerTeams)
        {
            var addWinnerRole = (CustomRoles)additionalWinners;
            Logger.Warn(additionalWinners.ToString(), "AdditionalWinner");
            if (addWinnerRole == CustomRoles.Sidekick) continue;

            Color color = additionalWinners == AdditionalWinners.AliveNeutrals ? Team.Neutral.GetColor() : Utils.GetRoleColor(addWinnerRole);
            additionalWinnerText += "\n" + Utils.ColorString(color, GetAdditionalWinnerRoleName(additionalWinners == AdditionalWinners.AliveNeutrals ? additionalWinners.ToString() : addWinnerRole.ToString()));
        }

        Skip:

        if (CustomWinnerHolder.WinnerTeam is not CustomWinner.Draw and not CustomWinner.None and not CustomWinner.Error)
            winnerText.text = additionalWinnerText == string.Empty ? $"<size=100%><color={customWinnerColor}>{customWinnerText}</color></size>" : $"<size=100%><color={customWinnerColor}>{customWinnerText}</color></size><size=50%>{additionalWinnerText}</size>";

        EndOfText:

        LastWinsText = winnerText.text /*.RemoveHtmlTags()*/;
        return;

        IEnumerator SetupPoolablePlayers()
        {
            if (Camera.main == null) yield break;

            yield return null;

            Vector3 pos = Camera.main.ViewportToWorldPoint(new(0f, 1f, Camera.main.nearClipPlane));
            GameObject roleSummaryObject = Object.Instantiate(__instance.WinText.gameObject);
            roleSummaryObject.transform.position = new(__instance.Navigation.ExitButton.transform.position.x + 0.1f, pos.y - 0.1f, -15f);
            roleSummaryObject.transform.localScale = new(1f, 1f, 1f);
            roleSummaryObject.SetActive(false);
            
            yield return null;

            StringBuilder sb = new($"<font=\"DIN_Pro_Bold_700 SDF\">{GetString("RoleSummaryText")}\n<b>");
            List<byte> cloneRoles = [.. Main.PlayerStates.Keys];

            foreach (byte id in Main.WinnerList)
            {
                if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>")) continue;

                sb.Append('\n').Append(EndGamePatch.SummaryText[id]);
                cloneRoles.Remove(id);
            }

            sb.Append("</b>\n");
            
            yield return null;

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.SoloPVP:
                {
                    List<(int, byte)> list = [];
                    list.AddRange(cloneRoles.Select(id => (SoloPVP.GetRankFromScore(id), id)));

                    list.Sort();
                    foreach ((int, byte) id in list.Where(x => EndGamePatch.SummaryText.ContainsKey(x.Item2)))
                        sb.Append('\n').Append(EndGamePatch.SummaryText[id.Item2]);

                    break;
                }
                case CustomGameMode.FFA:
                {
                    List<(int, byte)> list = [];
                    list.AddRange(cloneRoles.Select(id => (FreeForAll.GetRankFromScore(id), id)));

                    list.Sort();
                    foreach ((int, byte) id in list.Where(x => EndGamePatch.SummaryText.ContainsKey(x.Item2)))
                        sb.Append('\n').Append(EndGamePatch.SummaryText[id.Item2]);

                    break;
                }
                case CustomGameMode.StopAndGo:
                {
                    List<(int, byte)> list = [];
                    list.AddRange(cloneRoles.Select(id => (StopAndGo.GetRankFromScore(id), id)));

                    list.Sort();
                    foreach ((int, byte) id in list.Where(x => EndGamePatch.SummaryText.ContainsKey(x.Item2)))
                        sb.Append('\n').Append(EndGamePatch.SummaryText[id.Item2]);

                    break;
                }
                case CustomGameMode.HotPotato:
                {
                    IOrderedEnumerable<byte> list = cloneRoles.OrderByDescending(HotPotato.GetSurvivalTime);
                    foreach (byte id in list.Where(EndGamePatch.SummaryText.ContainsKey)) sb.Append('\n').Append(EndGamePatch.SummaryText[id]);

                    break;
                }
                case CustomGameMode.Speedrun:
                {
                    IOrderedEnumerable<byte> list = cloneRoles.OrderByDescending(id => Main.PlayerStates[id].TaskState.CompletedTasksCount);
                    foreach (byte id in list.Where(EndGamePatch.SummaryText.ContainsKey)) sb.Append('\n').Append(EndGamePatch.SummaryText[id]);

                    break;
                }
                case CustomGameMode.CaptureTheFlag:
                {
                    IOrderedEnumerable<byte> list = cloneRoles.OrderByDescending(CaptureTheFlag.GetFlagTime);
                    foreach (byte id in list.Where(EndGamePatch.SummaryText.ContainsKey)) sb.Append('\n').Append(EndGamePatch.SummaryText[id]);

                    break;
                }
                case CustomGameMode.NaturalDisasters:
                {
                    IOrderedEnumerable<byte> list = cloneRoles.OrderByDescending(NaturalDisasters.SurvivalTime);
                    foreach (byte id in list.Where(EndGamePatch.SummaryText.ContainsKey)) sb.Append('\n').Append(EndGamePatch.SummaryText[id]);

                    break;
                }
                case CustomGameMode.RoomRush:
                {
                    IOrderedEnumerable<byte> list = RoomRush.PointsSystem ? cloneRoles.OrderByDescending(x => int.TryParse(RoomRush.GetPoints(x).Split('/')[0], out int i) ? i : 0) : cloneRoles.OrderByDescending(RoomRush.GetSurvivalTime);
                    foreach (byte id in list.Where(EndGamePatch.SummaryText.ContainsKey)) sb.Append('\n').Append(EndGamePatch.SummaryText[id]);

                    break;
                }
                case CustomGameMode.KingOfTheZones:
                {
                    IOrderedEnumerable<byte> list = cloneRoles.OrderByDescending(KingOfTheZones.GetZoneTime);
                    foreach (byte id in list.Where(EndGamePatch.SummaryText.ContainsKey)) sb.Append('\n').Append(EndGamePatch.SummaryText[id]);

                    break;
                }
                case CustomGameMode.Snowdown:
                case CustomGameMode.Mingle:
                case CustomGameMode.Deathrace:
                case CustomGameMode.BedWars:
                case CustomGameMode.Quiz:
                {
                    foreach (byte id in cloneRoles.Where(EndGamePatch.SummaryText.ContainsKey))
                        sb.Append('\n').Append(EndGamePatch.SummaryText[id]);

                    break;
                }
                case CustomGameMode.TheMindGame:
                {
                    IOrderedEnumerable<byte> list = cloneRoles.OrderByDescending(TheMindGame.GetPoints);
                    foreach (byte id in list.Where(EndGamePatch.SummaryText.ContainsKey)) sb.Append('\n').Append(EndGamePatch.SummaryText[id]);
                    break;
                }
                default:
                {
                    foreach (byte id in cloneRoles)
                    {
                        try
                        {
                            if (EndGamePatch.SummaryText[id].Contains("<INVALID:NotAssigned>")) continue;

                            sb.Append('\n').Append(EndGamePatch.SummaryText[id]);
                        }
                        catch { }
                    }

                    break;
                }
            }

            yield return null;

            if (Options.CurrentGameMode != CustomGameMode.Standard)
            {
                if (Statistics.WinCountsForOutro != string.Empty)
                {
                    sb.Append("\n\n\n");
                    sb.Append(Statistics.WinCountsForOutro);
                }
            }

            sb.Append("</font>");

            yield return null;

            var roleSummary = roleSummaryObject.GetComponent<TextMeshPro>();
            roleSummary.alignment = TextAlignmentOptions.TopLeft;
            roleSummary.color = Color.white;
            roleSummary.outlineWidth *= 1.2f;
            roleSummary.fontSizeMin = roleSummary.fontSizeMax = roleSummary.fontSize = 1.25f;

            var roleSummaryRectTransform = roleSummary.GetComponent<RectTransform>();
            roleSummaryRectTransform.anchoredPosition = new(pos.x + 3.5f, pos.y - 0.7f);
            roleSummary.text = sb.ToString();

            string[] lines = sb.ToString().Split('\n');
            List<TextMeshPro> roleSummaryObjects = [];

            for (var i = 0; i < lines.Length; i++)
            {
                GameObject lineObj = Object.Instantiate(roleSummaryObject, roleSummaryObject.transform.parent);
                lineObj.SetActive(true);
                var lineText = lineObj.GetComponent<TextMeshPro>();
                lineText.text = "<font=\"DIN_Pro_Bold_700 SDF\">" + lines[i];
                lineText.alignment = TextAlignmentOptions.TopLeft;

                var lineRect = lineObj.GetComponent<RectTransform>();
                lineRect.anchoredPosition = new(pos.x + 3.5f - 5f, pos.y - 0.7f - (i * 0.15f)); // slide from the left
                lineText.alpha = 0f;

                roleSummaryObjects.Add(lineText);
                yield return null;

                __instance.StartCoroutine(SlideAndFadeIn(lineRect, lineText, i * 0.15f).WrapToIl2Cpp()); // stagger animation
                continue;

                static IEnumerator SlideAndFadeIn(RectTransform rect, TextMeshPro text, float delay)
                {
                    yield return new WaitForSecondsRealtime(delay);

                    Vector2 start = rect.anchoredPosition;
                    Vector2 end = start + new Vector2(5f, 0); // target pos
                    const float duration = 0.5f;
                    var elapsed = 0f;

                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float t = elapsed / duration;
                        rect.anchoredPosition = Vector2.Lerp(start, end, Mathf.SmoothStep(0, 1, t));
                        text.alpha = t;
                        yield return null;
                    }

                    rect.anchoredPosition = end;
                    text.alpha = 1f;
                }
            }

            yield return null;

            Object.Destroy(roleSummaryObject);

            yield return null;

            bool showInitially = Main.ShowResult;

            ResultsToggleButton = new SimpleButton(
                __instance.transform,
                "ShowHideResultsButton",
                new(-4.5f, 2.6f, -14f),
                new(0, 165, 255, 255),
                new(0, 255, 255, 255),
                () =>
                {
                    bool setToActive = !roleSummaryObjects[0].gameObject.activeSelf;
                    roleSummaryObjects.ForEach(x => x.gameObject.SetActive(setToActive));
                    Main.ShowResult = setToActive;
                    ResultsToggleButton.Label.text = GetString(setToActive ? "HideResults" : "ShowResults");
                },
                GetString(showInitially ? "HideResults" : "ShowResults"))
            {
                Scale = new(1.5f, 0.5f),
                FontSize = 2f
            };

            if (Options.CurrentGameMode == CustomGameMode.Standard)
            {
                yield return null;

                int num = Mathf.CeilToInt(7.5f);

                try
                {
                    Il2CppArrayBase<PoolablePlayer> pbs = __instance.transform.GetComponentsInChildren<PoolablePlayer>();

                    if (pbs != null)
                    {
                        foreach (PoolablePlayer pb in pbs)
                        {
                            if (pb != null)
                                pb.ToggleName(false);
                        }
                    }
                }
                catch (Exception e) { Utils.ThrowException(e); }

                yield return null;

                List<CachedPlayerData> cachedWinners = EndGameResult.CachedWinners.ToArray().ToList();

                for (var i = 0; i < cachedWinners.Count; i++)
                {
                    CachedPlayerData data = cachedWinners[i];
                    int num2 = i % 2 == 0 ? -1 : 1;
                    int num3 = (i + 1) / 2;
                    float num4 = num3 / (float)num;
                    float num5 = Mathf.Lerp(1f, 0.75f, num4);
                    float num6 = i == 0 ? -8 : -1;

                    PoolablePlayer poolablePlayer = Object.Instantiate(__instance.PlayerPrefab, __instance.transform);
                    poolablePlayer.transform.localPosition = new Vector3(1f * num2 * num3 * num5, FloatRange.SpreadToEdges(-1.125f, 0f, num3, num), num6 + (num3 * 0.01f)) * 0.9f;
                    float num7 = Mathf.Lerp(1f, 0.65f, num4) * 0.9f;
                    Vector3 vector = new(num7, num7, 1f);
                    poolablePlayer.transform.localScale = vector;
                    poolablePlayer.UpdateFromPlayerOutfit(data.Outfit, PlayerMaterial.MaskType.ComplexUI, data.IsDead, true);

                    if (data.IsDead)
                    {
                        poolablePlayer.cosmetics.currentBodySprite.BodySprite.sprite = poolablePlayer.cosmetics.currentBodySprite.GhostSprite;
                        poolablePlayer.SetDeadFlipX(i % 2 == 0);
                    }
                    else
                        poolablePlayer.SetFlipX(i % 2 == 0);

                    bool lowered = i is 1 or 2 or 5 or 6 or 9 or 10 or 13 or 14;

                    poolablePlayer.cosmetics.nameText.color = Color.white;
                    poolablePlayer.cosmetics.nameText.transform.localScale = new(1f / vector.x, 1f / vector.y, 1f / vector.z);
                    poolablePlayer.cosmetics.nameText.text = data.PlayerName;

                    Vector3 defaultPos = poolablePlayer.cosmetics.nameText.transform.localPosition;

                    yield return null;

                    for (var j = 0; j < Main.WinnerList.Count; j++)
                    {
                        byte id = Main.WinnerList[j];
                        if (Main.WinnerNameList[j].RemoveHtmlTags() != data.PlayerName.RemoveHtmlTags() || data.PlayerName == GetString("Dead")) continue;

                        CustomRoles role = Main.PlayerStates[id].MainRole;

                        string color = Main.RoleColors[role];
                        string rolename = Utils.GetRoleName(role);

                        poolablePlayer.cosmetics.nameText.text += $"\n<color={color}>{rolename}</color>";
                        poolablePlayer.cosmetics.nameText.transform.localPosition = new(defaultPos.x, !lowered ? defaultPos.y - 0.6f : defaultPos.y - 1.4f, -15f);
                    }

                    yield return null;
                }
            }
        }

        static string GetAdditionalWinnerRoleName(string role)
        {
            string name = GetString($"AdditionalWinnerRoleText.{role}");
            if (name == string.Empty || name.StartsWith("*") || name.StartsWith("<INVALID"))
                name = string.Format(GetString("AdditionalWinnerRoleText.Default"), GetString(role));

            return name;
        }

        static string GetWinnerRoleName(CustomRoles role)
        {
            string name = GetString($"WinnerRoleText.{role}");
            if (name == string.Empty || name.StartsWith("*") || name.StartsWith("<INVALID"))
                name = string.Format(GetString("WinnerRoleText.Default"), GetString($"{role}"));

            return name;
        }
    }

}
