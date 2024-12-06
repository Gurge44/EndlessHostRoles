using System;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using UnityEngine;
using static EHR.Translator;

namespace EHR;

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.ShowRole))]
internal static class SetUpRoleTextPatch
{
    public static bool IsInIntro;

    public static void Postfix(IntroCutscene __instance)
    {
        // After showing team for non-modded clients, update player names.
        IsInIntro = false;
        Utils.DoNotifyRoles(NoCache: true);

        PlayerControl lp = PlayerControl.LocalPlayer;

        LateTask.New(() =>
        {
            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.SoloKombat:
                {
                    Color color = ColorUtility.TryParseHtmlString("#f55252", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.color = color;
                    __instance.RoleText.text = GetString("SoloKombat");
                    __instance.RoleText.color = Utils.GetRoleColor(lp.GetCustomRole());
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = lp.GetRoleInfo();
                    break;
                }
                case CustomGameMode.FFA:
                {
                    Color color = ColorUtility.TryParseHtmlString("#00ffff", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("Killer");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("KillerInfo");
                    break;
                }
                case CustomGameMode.MoveAndStop:
                {
                    Color color = ColorUtility.TryParseHtmlString("#00ffa5", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("MoveAndStop");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("TaskerInfo");
                    break;
                }
                case CustomGameMode.HotPotato:
                {
                    Color color = ColorUtility.TryParseHtmlString("#e8cd46", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("HotPotato");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("PotatoInfo");
                    break;
                }
                case CustomGameMode.Speedrun:
                {
                    Color color = Utils.GetRoleColor(CustomRoles.Speedrunner);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("Runner");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("RunnerInfo");
                    break;
                }
                case CustomGameMode.CaptureTheFlag:
                {
                    Color color = ColorUtility.TryParseHtmlString("#1313c2", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.transform.gameObject.SetActive(false);
                    __instance.RoleText.text = GetString("CTFPlayer");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("CTFPlayerInfo");
                    break;
                }
                case CustomGameMode.NaturalDisasters:
                {
                    Color color = ColorUtility.TryParseHtmlString("#03fc4a", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.color = color;
                    __instance.RoleText.text = GetString("NDPlayer");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("NDPlayerInfo");
                    break;
                }
                case CustomGameMode.RoomRush:
                {
                    Color color = ColorUtility.TryParseHtmlString("#ffab1b", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.color = color;
                    __instance.RoleText.text = GetString("RRPlayer");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("RRPlayerInfo");
                    break;
                }
                case CustomGameMode.AllInOne:
                {
                    Color color = ColorUtility.TryParseHtmlString("#f542ad", out Color c) ? c : new(255, 255, 255, 255);
                    __instance.YouAreText.color = color;
                    __instance.RoleText.text = GetString("AllInOne");
                    __instance.RoleText.color = color;
                    __instance.RoleBlurbText.color = color;
                    __instance.RoleBlurbText.text = GetString("AllInOneInfo");
                    break;
                }
                default:
                {
                    CustomRoles role = lp.GetCustomRole();

                    if (!role.IsVanilla())
                    {
                        __instance.YouAreText.color = Utils.GetRoleColor(role);
                        __instance.RoleText.text = Utils.GetRoleName(role);
                        __instance.RoleText.color = Utils.GetRoleColor(role);
                        __instance.RoleBlurbText.color = Utils.GetRoleColor(role);
                        __instance.RoleBlurbText.text = "<size=50%>" + lp.GetRoleInfo() + "</size>";
                    }

                    foreach (CustomRoles subRole in Main.PlayerStates[lp.PlayerId].SubRoles)
                    {
                        if (role is CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor && subRole == CustomRoles.Lovers) continue;

                        __instance.RoleBlurbText.text += "\n<size=30%>" + Utils.ColorString(Utils.GetRoleColor(subRole), GetString($"{subRole}Info"));
                    }

                    __instance.RoleText.text += Utils.GetSubRolesText(lp.PlayerId, false, true);
                    break;
                }
            }
        }, 0.0001f, "Override Role Text");

        if (!AmongUsClient.Instance.AmHost)
        {
            LateTask.New(() =>
            {
                if (AmongUsClient.Instance.IsGameOver || GameStates.IsLobby || lp == null) return;

                lp.SetName(Main.AllPlayerNames[lp.PlayerId]);
            }, 1f, "Reset Name For Modded Client");
        }
    }
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
internal static class CoBeginPatch
{
    public static void Prefix()
    {
        StringBuilder sb = new();
        sb.Append("------------Display Names------------\n");

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            sb.Append($"{(pc.AmOwner ? "[*]" : string.Empty),-3}{pc.PlayerId,-2}:{pc.name.PadRightV2(20)}:{pc.cosmetics.nameText.text} ({Palette.ColorNames[pc.Data.DefaultOutfit.ColorId].ToString().Replace("Color", string.Empty)})\n");
            pc.cosmetics.nameText.text = pc.name;
        }

        sb.Append("------------Roles------------\n");
        foreach (PlayerControl pc in Main.AllPlayerControls) sb.Append($"{(pc.AmOwner ? "[*]" : string.Empty),-3}{pc.PlayerId,-2}:{pc.Data?.PlayerName?.PadRightV2(20)}:{pc.GetAllRoleName().RemoveHtmlTags().Replace("\n", " + ")}\n");

        sb.Append("------------Platforms------------\n");

        foreach (PlayerControl pc in Main.AllPlayerControls)
        {
            try
            {
                string text = pc.AmOwner ? "[*]" : "   ";
                text += $"{pc.PlayerId,-2}:{pc.Data?.PlayerName?.PadRightV2(20)}:{pc.GetClient()?.PlatformData?.Platform.ToString().Replace("Standalone", string.Empty),-11}";

                if (Main.PlayerVersion.TryGetValue(pc.PlayerId, out PlayerVersion pv))
                    text += $":Mod({pv.forkId}/{pv.version}:{pv.tag})";
                else
                    text += ":Vanilla";

                sb.Append(text + "\n");
            }
            catch (Exception ex) { Logger.Exception(ex, "Platform"); }
        }

        sb.Append("------------Vanilla Settings------------\n");
        System.Collections.Generic.IEnumerable<string> tmp = GameOptionsManager.Instance.CurrentGameOptions.ToHudString(GameData.Instance ? GameData.Instance.PlayerCount : 10).Split("\r\n").Skip(1);
        foreach (string t in tmp) sb.Append(t + "\n");

        sb.Append("------------Modded Settings------------\n");

        foreach (OptionItem o in OptionItem.AllOptions)
            if (!o.IsCurrentlyHidden() && (o.Parent?.GetBool() ?? !o.GetString().Equals("0%")))
                sb.Append($"{(o.Parent == null ? o.GetName(true, true).RemoveHtmlTags().PadRightV2(40) : $"┗ {o.GetName(true, true).RemoveHtmlTags()}".PadRightV2(41))}:{o.GetString().RemoveHtmlTags()}\n");

        sb.Append("-------------Other Information-------------\n");
        sb.Append($"Number of players: {Main.AllPlayerControls.Length}\n");
        sb.Append($"Game mode: {GetString(Options.CurrentGameMode.ToString())}\n");
        sb.Append($"Map: {Main.CurrentMap}");

        Logger.Info("\n" + sb, "GameInfo", multiLine: true);

        RPC.RpcVersionCheck();

        SetupLongRoleDescriptions();

        Utils.NotifyRoles(NoCache: true);

        GameStates.InGame = true;
    }

    private static void SetupLongRoleDescriptions()
    {
        try
        {
            Utils.LongRoleDescriptions.Clear();

            int charsInOneLine = GetUserTrueLang() is SupportedLangs.Russian or SupportedLangs.SChinese or SupportedLangs.TChinese or SupportedLangs.Japanese or SupportedLangs.Korean ? 35 : 50;

            foreach (PlayerControl seer in Main.AllPlayerControls)
            {
                string longInfo = seer.GetRoleInfo(true).Split("\n\n")[0];
                if (longInfo.Contains("):\n")) longInfo = longInfo.Split("):\n")[1];

                var tooLong = false;
                bool showLongInfo = Options.ShowLongInfo.GetBool();

                if (showLongInfo)
                {
                    if (longInfo.Length > 296)
                    {
                        longInfo = longInfo[..296];
                        longInfo += "...";
                        tooLong = true;
                    }

                    for (int i = charsInOneLine; i < longInfo.Length; i += charsInOneLine)
                    {
                        if (tooLong && i > 296) break;

                        int index = longInfo.LastIndexOf(' ', i);
                        if (index != -1) longInfo = longInfo.Insert(index + 1, "\n");
                    }
                }

                longInfo = $"<#ffffff>{longInfo}</color>";

                int lines = longInfo.Count(x => x == '\n');
                int readTime = 20 + (lines * 5);

                Utils.LongRoleDescriptions[seer.PlayerId] = (longInfo, readTime, tooLong);
            }
        }
        catch (Exception e) { Utils.ThrowException(e); }
    }
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate))]
internal static class BeginCrewmatePatch
{
    public static bool Prefix(IntroCutscene __instance, ref List<PlayerControl> teamToDisplay)
    {
        CustomRoles role = PlayerControl.LocalPlayer.GetCustomRole();

        if (PlayerControl.LocalPlayer.Is(CustomRoleTypes.Neutral) && !role.IsMadmate())
        {
            teamToDisplay = new();
            teamToDisplay.Add(PlayerControl.LocalPlayer);

            byte id = PlayerControl.LocalPlayer.PlayerId;

            switch (Main.PlayerStates[id].Role)
            {
                case Lawyer:
                    teamToDisplay.Add(Utils.GetPlayerById(Lawyer.Target[id]));
                    break;
                case Executioner:
                    teamToDisplay.Add(Utils.GetPlayerById(Executioner.Target[id]));
                    break;
            }
        }
        else if (PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) || role.IsMadmate())
        {
            teamToDisplay = new();
            teamToDisplay.Add(PlayerControl.LocalPlayer);
            __instance.BeginImpostor(teamToDisplay);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return false;
        }

        if (role == CustomRoles.LovingCrewmate || PlayerControl.LocalPlayer.Is(CustomRoles.Lovers))
        {
            teamToDisplay = new();
            teamToDisplay.Add(PlayerControl.LocalPlayer);
            teamToDisplay.Add(Main.LoversPlayers.FirstOrDefault(x => x.PlayerId != PlayerControl.LocalPlayer.PlayerId));
        }
        else if (role == CustomRoles.LovingImpostor) teamToDisplay.Add(Main.LoversPlayers.FirstOrDefault(x => x.PlayerId != PlayerControl.LocalPlayer.PlayerId));

        if (CustomTeamManager.EnabledCustomTeams.Count > 0)
        {
            CustomTeamManager.CustomTeam team = CustomTeamManager.GetCustomTeam(PlayerControl.LocalPlayer.PlayerId);

            if (team != null)
            {
                teamToDisplay = new();

                foreach (PlayerControl pc in Main.AllPlayerControls)
                    if (CustomTeamManager.AreInSameCustomTeam(pc.PlayerId, PlayerControl.LocalPlayer.PlayerId))
                        teamToDisplay.Add(pc);
            }
        }

        if (CustomGameMode.FFA.IsActiveOrIntegrated() && FFAManager.FFATeamMode.GetBool() && FFAManager.PlayerTeams.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out int ffaTeam))
        {
            teamToDisplay = new();

            foreach (PlayerControl pc in Main.AllPlayerControls)
                if (FFAManager.PlayerTeams.TryGetValue(pc.PlayerId, out int team) && team == ffaTeam)
                    teamToDisplay.Add(pc);
        }

        return true;
    }

    public static void Postfix(IntroCutscene __instance, ref List<PlayerControl> teamToDisplay)
    {
        CustomRoles role = PlayerControl.LocalPlayer.GetCustomRole();

        __instance.ImpostorText.gameObject.SetActive(false);

        if (PlayerControl.LocalPlayer.Is(CustomRoles.Bloodlust))
        {
            __instance.TeamTitle.text = GetString("TeamCrewmate");
            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Bloodlust);
            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = GetString("SubText.Bloodlust");
        }
        else if (role is CustomRoles.LovingCrewmate or CustomRoles.LovingImpostor)
        {
            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = Utils.GetRoleColor(role);
            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(role.GetRoleTypes());
            byte otherLoverId = Main.LoversPlayers.First(x => x.PlayerId != PlayerControl.LocalPlayer.PlayerId).PlayerId;
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = string.Format(GetString($"SubText.{role}"), otherLoverId.ColoredPlayerName());
        }
        else
        {
            switch (role.GetCustomRoleTypes())
            {
                case CustomRoleTypes.Impostor:
                    __instance.TeamTitle.text = GetString("TeamImpostor");
                    __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 25, 25, byte.MaxValue);
                    PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
                    __instance.ImpostorText.gameObject.SetActive(true);
                    __instance.ImpostorText.text = GetString("SubText.Impostor");
                    break;
                case CustomRoleTypes.Crewmate:
                    __instance.TeamTitle.text = GetString("TeamCrewmate");
                    __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(140, 255, 255, byte.MaxValue);
                    PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Crewmate);
                    __instance.ImpostorText.gameObject.SetActive(true);
                    __instance.ImpostorText.text = GetString("SubText.Crewmate");
                    break;
                case CustomRoleTypes.Neutral:

                    if (Options.UniqueNeutralRevealScreen.GetBool())
                    {
                        __instance.TeamTitle.text = GetString($"{role}");
                        __instance.TeamTitle.color = __instance.BackgroundBar.material.color = Utils.GetRoleColor(role);
                        PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                        __instance.ImpostorText.gameObject.SetActive(true);
                        __instance.ImpostorText.text = GetString($"{role}Info");
                    }
                    else
                    {
                        __instance.TeamTitle.text = GetString("TeamNeutral");
                        __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 171, 27, byte.MaxValue);
                        PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                        __instance.ImpostorText.gameObject.SetActive(true);
                        __instance.ImpostorText.text = GetString("SubText.Neutral");
                    }

                    break;
            }

            if (Main.LoversPlayers.Count == 2 && Main.LoversPlayers.Exists(x => x.IsLocalPlayer()))
            {
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Lovers);
                byte otherLoverId = Main.LoversPlayers.First(x => x.PlayerId != PlayerControl.LocalPlayer.PlayerId).PlayerId;
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.DestroyTranslator();
                __instance.ImpostorText.text = string.Format(GetString("SubText.LovingCrewmate"), otherLoverId.ColoredPlayerName());
            }
        }

        // ReSharper disable Unity.IncorrectMonoBehaviourInstantiation
        try
        {
            PlayerControl.LocalPlayer.Data.Role.IntroSound = role switch
            {
                CustomRoles.Terrorist or
                    CustomRoles.Sapper or
                    CustomRoles.Bomber or
                    CustomRoles.Nuker
                    => ShipStatus.Instance.CommonTasks.FirstOrDefault(task => task.TaskType == TaskTypes.FixWiring)?.MinigamePrefab.OpenSound,

                CustomRoles.Sheriff or
                    CustomRoles.Veteran
                    => new AirshipStatus().ShortTasks.FirstOrDefault(task => task.TaskType == TaskTypes.PutAwayPistols)?.MinigamePrefab.OpenSound,

                CustomRoles.Cleaner or
                    CustomRoles.Cleanser
                    => new AirshipStatus().ShortTasks.FirstOrDefault(task => task.TaskType == TaskTypes.PolishRuby)?.MinigamePrefab.OpenSound,

                CustomRoles.Dictator or
                    CustomRoles.Lawyer or
                    CustomRoles.Judge or
                    CustomRoles.Mayor
                    => new AirshipStatus().ShortTasks.FirstOrDefault(task => task.TaskType == TaskTypes.FixShower)?.MinigamePrefab.OpenSound,

                CustomRoles.Monitor or
                    CustomRoles.AntiAdminer
                    => new AirshipStatus().LongTasks.FirstOrDefault(task => task.TaskType == TaskTypes.ResetBreakers)?.MinigamePrefab.OpenSound,

                CustomRoles.EvilTracker or
                    CustomRoles.Tracefinder or
                    CustomRoles.Scout or
                    CustomRoles.Bloodhound or
                    CustomRoles.Mortician or
                    CustomRoles.Lighter
                    => new AirshipStatus().ShortTasks.FirstOrDefault(task => task.TaskType == TaskTypes.DivertPower)?.MinigamePrefab.OpenSound,

                CustomRoles.Oracle or
                    CustomRoles.Divinator or
                    CustomRoles.Mediumshiper or
                    CustomRoles.DovesOfNeace or
                    CustomRoles.Spiritualist or
                    CustomRoles.Spiritcaller or
                    CustomRoles.Beacon or
                    CustomRoles.Farseer
                    => GetIntroSound(RoleTypes.GuardianAngel),

                CustomRoles.Alchemist
                    => new AirshipStatus().LongTasks.FirstOrDefault(task => task.TaskType == TaskTypes.DevelopPhotos)?.MinigamePrefab.OpenSound,

                CustomRoles.Deputy or
                    CustomRoles.Jailor
                    => new AirshipStatus().LongTasks.FirstOrDefault(task => task.TaskType == TaskTypes.UnlockSafe)?.MinigamePrefab.OpenSound,

                CustomRoles.Workaholic or
                    CustomRoles.Speedrunner or
                    CustomRoles.Snitch
                    => DestroyableSingleton<HudManager>.Instance.TaskCompleteSound,

                CustomRoles.TaskManager
                    => DestroyableSingleton<HudManager>.Instance.TaskUpdateSound,

                CustomRoles.Opportunist or
                    CustomRoles.FFF or
                    CustomRoles.Revolutionist
                    => GetIntroSound(RoleTypes.Crewmate),

                CustomRoles.Nightmare
                    => GetIntroSound(RoleTypes.Impostor),

                CustomRoles.SabotageMaster or
                    CustomRoles.Engineer or
                    CustomRoles.EngineerEHR or
                    CustomRoles.Inhibitor or
                    CustomRoles.Saboteur or
                    CustomRoles.SecurityGuard or
                    CustomRoles.Provocateur
                    => ShipStatus.Instance.SabotageSound,

                CustomRoles.Mastermind or
                    CustomRoles.Shiftguard or
                    CustomRoles.Randomizer or
                    CustomRoles.Gambler
                    => GetIntroSound(RoleTypes.Shapeshifter),

                CustomRoles.Doctor or
                    CustomRoles.Medic
                    => GetIntroSound(RoleTypes.Scientist),

                CustomRoles.GM
                    => DestroyableSingleton<HudManager>.Instance.TaskCompleteSound,

                CustomRoles.SwordsMan or
                    CustomRoles.Minimalism or
                    CustomRoles.NiceGuesser
                    => PlayerControl.LocalPlayer.KillSfx,

                CustomRoles.Drainer
                    => PlayerControl.LocalPlayer.MyPhysics.ImpostorDiscoveredSound,

                CustomRoles.Addict or
                    CustomRoles.Ventguard
                    => ShipStatus.Instance.VentEnterSound,

                CustomRoles.ParityCop or
                    CustomRoles.NiceEraser or
                    CustomRoles.TimeManager
                    => MeetingHud.Instance.VoteLockinSound,

                CustomRoles.Demolitionist or
                    CustomRoles.TimeMaster or
                    CustomRoles.Grenadier or
                    CustomRoles.Miner or
                    CustomRoles.Disperser
                    => ShipStatus.Instance.VentMoveSounds.FirstOrDefault(),

                CustomRoles.Tracker
                    or CustomRoles.EvilTracker
                    => GetIntroSound(RoleTypes.Tracker),

                CustomRoles.Noisemaker
                    or CustomRoles.NoisemakerEHR
                    or CustomRoles.DarkHide
                    or CustomRoles.Specter
                    => GetIntroSound(RoleTypes.Noisemaker),

                CustomRoles.Phantom
                    or CustomRoles.PhantomEHR
                    or CustomRoles.Swooper
                    or CustomRoles.Wraith
                    or CustomRoles.Chameleon
                    => GetIntroSound(RoleTypes.Phantom),

                CustomRoles.Shapeshifter
                    or CustomRoles.ShapeshifterEHR
                    => GetIntroSound(RoleTypes.Shapeshifter),

                _ => GetAudioClipFromCustomRoleType()
            };
        }
        catch (Exception ex)
        {
            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetAudioClipFromCustomRoleType();
            Logger.Warn($"Could not set intro sound\n{ex}", "IntroSound");
        }
        // ReSharper restore Unity.IncorrectMonoBehaviourInstantiation

        if (PlayerControl.LocalPlayer.Is(CustomRoles.GM))
        {
            __instance.TeamTitle.text = Utils.GetRoleName(role);
            __instance.TeamTitle.color = Utils.GetRoleColor(role);
            __instance.BackgroundBar.material.color = Utils.GetRoleColor(role);
            __instance.ImpostorText.gameObject.SetActive(false);
        }

        if (PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) || PlayerControl.LocalPlayer.Is(CustomRoles.Parasite) || PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor))
        {
            __instance.TeamTitle.text = GetString("TeamMadmate");
            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 25, 25, byte.MaxValue);
            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = GetString("SubText.Madmate");
        }

        if (CustomTeamManager.EnabledCustomTeams.Count > 0)
        {
            CustomTeamManager.CustomTeam team = CustomTeamManager.GetCustomTeam(PlayerControl.LocalPlayer.PlayerId);

            if (team != null)
            {
                if (team.RoleRevealScreenTitle != "*") __instance.TeamTitle.text = team.RoleRevealScreenTitle;

                if (team.RoleRevealScreenBackgroundColor != "*" && ColorUtility.TryParseHtmlString(team.RoleRevealScreenBackgroundColor, out Color bgColor)) __instance.TeamTitle.color = __instance.BackgroundBar.material.color = bgColor;

                __instance.ImpostorText.gameObject.SetActive(team.RoleRevealScreenSubtitle != "*");
                __instance.ImpostorText.text = team.RoleRevealScreenSubtitle;

                foreach (PlayerControl pc in Main.AllPlayerControls)
                    if (CustomTeamManager.AreInSameCustomTeam(pc.PlayerId, PlayerControl.LocalPlayer.PlayerId))
                        teamToDisplay.Add(pc);
            }
        }

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.SoloKombat:
            {
                Color color = ColorUtility.TryParseHtmlString("#f55252", out Color c) ? c : new(255, 255, 255, 255);
                __instance.TeamTitle.text = Utils.GetRoleName(role);
                __instance.TeamTitle.color = Utils.GetRoleColor(role);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("ModeSoloKombat");
                __instance.BackgroundBar.material.color = color;
                PlayerControl.LocalPlayer.Data.Role.IntroSound = DestroyableSingleton<HnSImpostorScreamSfx>.Instance.HnSOtherImpostorTransformSfx;
                break;
            }
            case CustomGameMode.FFA:
            {
                __instance.TeamTitle.text = GetString("Killer");
                Color color = FFAManager.PlayerTeams.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out int team) && ColorUtility.TryParseHtmlString(FFAManager.TeamColors[team], out Color teamColor) ? teamColor : new(0, 255, 255, byte.MaxValue);
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = color;
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("KillerInfo");
                break;
            }
            case CustomGameMode.MoveAndStop:
            {
                __instance.TeamTitle.text = GetString("MoveAndStop");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(0, 255, 165, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("TaskerInfo");
                break;
            }
            case CustomGameMode.HotPotato:
            {
                __instance.TeamTitle.text = GetString("HotPotato");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(232, 205, 70, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("PotatoInfo");
                break;
            }
            case CustomGameMode.Speedrun:
            {
                __instance.TeamTitle.text = GetString("Runner");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = Utils.GetRoleColor(CustomRoles.Speedrunner);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Crewmate);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("RunnerInfo");
                break;
            }
            case CustomGameMode.HideAndSeek:
            {
                __instance.TeamTitle.text = GetString("HideAndSeek");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(52, 94, 235, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("SubText.HideAndSeek");
                break;
            }
            case CustomGameMode.CaptureTheFlag:
            {
                __instance.TeamTitle.text = $"<size=70%>{GetString("CTFPlayer")}</size>";
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(19, 19, 194, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Engineer);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("CTFPlayerInfo");
                break;
            }
            case CustomGameMode.NaturalDisasters:
            {
                __instance.TeamTitle.text = "<size=70%>" + GetString("NDPlayer") + "</size>";
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(3, 252, 74, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.GuardianAngel);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("NDPlayerInfo");
                break;
            }
            case CustomGameMode.RoomRush:
            {
                __instance.TeamTitle.text = GetString("RRPlayer");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 171, 27, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("RRPlayerInfo");
                break;
            }
            case CustomGameMode.AllInOne:
            {
                __instance.TeamTitle.text = GetString("AllInOne");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(245, 66, 173, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("AllInOneInfo");
                break;
            }
        }

        // if (Input.GetKey(KeyCode.RightShift))
        // {
        //     __instance.TeamTitle.text = "明天就跑路啦";
        //     __instance.ImpostorText.gameObject.SetActive(true);
        //     __instance.ImpostorText.text = "嘿嘿嘿嘿嘿嘿";
        //     __instance.TeamTitle.color = Color.cyan;
        //     StartFadeIntro(__instance, Color.cyan, Color.yellow);
        // }
        //
        // if (Input.GetKey(KeyCode.RightControl))
        // {
        //     __instance.TeamTitle.text = "警告";
        //     __instance.ImpostorText.gameObject.SetActive(true);
        //     __instance.ImpostorText.text = "请远离无知的玩家";
        //     __instance.TeamTitle.color = Color.magenta;
        //     StartFadeIntro(__instance, Color.magenta, Color.magenta);
        // }

        return;

        AudioClip GetAudioClipFromCustomRoleType()
        {
            return PlayerControl.LocalPlayer.GetCustomRoleTypes() switch
            {
                CustomRoleTypes.Impostor => GetIntroSound(RoleTypes.Impostor),
                CustomRoleTypes.Crewmate => GetIntroSound(RoleTypes.Crewmate),
                CustomRoleTypes.Neutral => GetIntroSound(RoleTypes.Shapeshifter),
                _ => GetIntroSound(RoleTypes.Crewmate)
            };
        }
    }

    private static AudioClip GetIntroSound(RoleTypes roleType)
    {
        return RoleManager.Instance.AllRoles.FirstOrDefault(role => role.Role == roleType)?.IntroSound;
    }

    /*
        private static async void StartFadeIntro(IntroCutscene __instance, Color start, Color end)
        {
            await Task.Delay(1000);
            int milliseconds = 0;
            while (true)
            {
                await Task.Delay(20);
                milliseconds += 20;
                float time = milliseconds / (float)500;
                Color LerpingColor = Color.Lerp(start, end, time);
                if (__instance == null || milliseconds > 500)
                {
                    Logger.Info("ループを終了します", "StartFadeIntro");
                    break;
                }

                __instance.BackgroundBar.material.color = LerpingColor;
            }
        }
    */
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor))]
internal static class BeginImpostorPatch
{
    public static bool Prefix(IntroCutscene __instance, ref List<PlayerControl> yourTeam)
    {
        CustomRoles role = PlayerControl.LocalPlayer.GetCustomRole();

        if (PlayerControl.LocalPlayer.Is(CustomRoles.Madmate) || role.IsMadmate())
        {
            yourTeam = new();
            yourTeam.Add(PlayerControl.LocalPlayer);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return true;
        }

        if (PlayerControl.LocalPlayer.IsCrewmate() && role.IsDesyncRole())
        {
            yourTeam = new();
            yourTeam.Add(PlayerControl.LocalPlayer);
            foreach (PlayerControl pc in Main.AllPlayerControls.Where(x => !x.AmOwner)) yourTeam.Add(pc);

            __instance.BeginCrewmate(yourTeam);
            __instance.overlayHandle.color = Palette.CrewmateBlue;
            return false;
        }

        if (role.IsNeutral() || PlayerControl.LocalPlayer.Is(CustomRoles.Bloodlust))
        {
            yourTeam = new();
            yourTeam.Add(PlayerControl.LocalPlayer);
            foreach (PlayerControl pc in Main.AllPlayerControls.Where(x => !x.AmOwner)) yourTeam.Add(pc);

            __instance.BeginCrewmate(yourTeam);
            __instance.overlayHandle.color = new Color32(255, 171, 27, byte.MaxValue);
            return false;
        }

        BeginCrewmatePatch.Prefix(__instance, ref yourTeam);
        return true;
    }

    public static void Postfix(IntroCutscene __instance, ref List<PlayerControl> yourTeam)
    {
        BeginCrewmatePatch.Postfix(__instance, ref yourTeam);
    }
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
internal static class IntroCutsceneDestroyPatch
{
    public static void Postfix( /*IntroCutscene __instance*/)
    {
        if (!GameStates.IsInGame) return;

        Main.IntroDestroyed = true;

        // Set roleAssigned as false for overriding roles for modded players
        // for vanilla clients we use "Data.Disconnected"
        Main.AllPlayerControls.Do(x => x.roleAssigned = false);

        PlayerControl[] aapc = Main.AllAlivePlayerControls;

        if (AmongUsClient.Instance.AmHost)
        {
            if (Main.NormalOptions.MapId != 4)
            {
                foreach (PlayerControl pc in aapc)
                {
                    pc.SyncSettings();
                    pc.RpcResetAbilityCooldown();

                    if (pc.GetCustomRole().UsesPetInsteadOfKill())
                        pc.AddAbilityCD(10);
                    else
                        pc.AddAbilityCD(false);

                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc, NoCache: true);
                }

                if (CustomGameMode.Standard.IsActiveOrIntegrated())
                {
                    int kcd = Options.StartingKillCooldown.GetInt();

                    if (kcd is not 10 and > 0)
                    {
                        LateTask.New(() =>
                        {
                            foreach (PlayerControl pc in aapc)
                            {
                                pc.ResetKillCooldown();
                                pc.SetKillCooldown(kcd - 2);
                            }
                        }, 2f, "FixKillCooldownTask");
                    }
                    else if (Options.FixFirstKillCooldown.GetBool())
                    {
                        LateTask.New(() =>
                        {
                            aapc.Do(x => x.ResetKillCooldown());
                            aapc.Where(x => Main.AllPlayerKillCooldown[x.PlayerId] - 2f > 0f).Do(pc => pc.SetKillCooldown(Main.AllPlayerKillCooldown[pc.PlayerId] - 2f));
                        }, 2f, "FixKillCooldownTask");
                    }
                }
            }

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.FFA when FFAManager.FFAChatDuringGame.GetBool():
                    Utils.SetChatVisibleForAll();
                    break;
            }

            // LateTask.New(() => Main.AllPlayerControls.Do(pc ⇒ pc.RpcSetRoleDesync(RoleTypes.Shapeshifter, -3)), 2f, "SetImpostorForServer");

            PlayerControl lp = PlayerControl.LocalPlayer;

            if (lp.GetRoleTypes() == RoleTypes.Shapeshifter)
                lp.RpcChangeRoleBasis(lp.GetCustomRole());

            if (Options.UsePets.GetBool())
            {
                Main.ProcessShapeshifts = false;

                LateTask.New(() =>
                {
                    foreach (PlayerControl pc in aapc)
                    {
                        if (pc.Is(CustomRoles.GM)) continue;

                        string petId = PetsPatch.GetPetId();
                        pc.RpcSetPetDesync(petId, pc);
                        Logger.Info($"{pc.GetNameWithRole()} => {GetString(petId)} Pet", "PetAssign");
                    }

                    AntiBlackout.SendGameData();
                }, 0.3f, "Grant Pet For Everyone");

                try
                {
                    LateTask.New(() =>
                    {
                        try
                        {
                            lp.Notify(GetString("GLHF"), 2f);

                            foreach (PlayerControl pc in aapc)
                            {
                                if (pc.IsHost()) continue; // Skip the host

                                try
                                {
                                    pc.RpcShapeshift(pc, false);
                                    pc.Notify(GetString("GLHF"), 2f);
                                }
                                catch (Exception ex) { Logger.Fatal(ex.ToString(), "IntroPatch.RpcShapeshift"); }
                            }
                        }
                        catch (Exception ex) { Logger.Fatal(ex.ToString(), "IntroPatch.RpcShapeshift.foreachCycle"); }
                    }, 0.4f, "Show Pet For Everyone");
                }
                catch { }

                LateTask.New(() => Main.ProcessShapeshifts = true, 1f, "Enable SS Processing");
            }

            if (Options.UseUnshiftTrigger.GetBool() || Main.PlayerStates.Values.Any(x => x.MainRole.AlwaysUsesUnshift())) LateTask.New(() => aapc.Do(x => x.CheckAndSetUnshiftState()), 2f, "UnshiftTrigger SS");

            float specExileDelay = CustomGameMode.FFA.IsActiveOrIntegrated() && FFAManager.FFAChatDuringGame.GetBool() ? 12.5f : 1f;

            if (Main.GM.Value && lp.Is(CustomRoles.GM))
            {
                LateTask.New(() =>
                {
                    lp.RpcExile();
                    Main.PlayerStates[lp.PlayerId].SetDead();
                }, specExileDelay, "Set GM Dead");
            }

            foreach (byte spectator in ChatCommands.Spectators)
            {
                LateTask.New(() =>
                {
                    spectator.GetPlayer().RpcExileV2();
                    Main.PlayerStates[spectator].SetDead();
                }, specExileDelay, $"Set Spectator Dead ({spectator.ColoredPlayerName().RemoveHtmlTags()})");
            }

            if (Options.RandomSpawn.GetBool())
            {
                RandomSpawn.SpawnMap map = Main.NormalOptions.MapId switch
                {
                    0 => new RandomSpawn.SkeldSpawnMap(),
                    1 => new RandomSpawn.MiraHQSpawnMap(),
                    2 => new RandomSpawn.PolusSpawnMap(),
                    3 => new RandomSpawn.DleksSpawnMap(),
                    5 => new RandomSpawn.FungleSpawnMap(),
                    _ => null
                };

                if (map != null && AmongUsClient.Instance.AmHost) aapc.Do(map.RandomTeleport);
            }

            if (lp.HasDesyncRole())
            {
                lp.Data.Role.AffectedByLightAffectors = false;

                foreach (PlayerControl target in Main.AllPlayerControls)
                {
                    // Set all players as killable players
                    target.Data.Role.CanBeKilled = true;

                    // When target is impostor, set name color as white
                    target.cosmetics.SetNameColor(Color.white);
                    target.Data.Role.NameColor = Color.white;
                }
            }

            Utils.CheckAndSetVentInteractions();

            if (AFKDetector.ActivateOnStart.GetBool()) LateTask.New(() => aapc.Do(AFKDetector.RecordPosition), 1f, log: false);

            LateTask.New(() => Main.Instance.StartCoroutine(Utils.NotifyEveryoneAsync()), 3f, log: false);
        }
        else
            foreach (PlayerControl player in Main.AllPlayerControls)
                Main.PlayerStates[player.PlayerId].InitTask(player);

        Logger.Info("OnDestroy", "IntroCutscene");
    }
}