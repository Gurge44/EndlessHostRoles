using AmongUs.GameOptions;
using HarmonyLib;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE;

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.ShowRole))]
class SetUpRoleTextPatch
{
    public static void Postfix(IntroCutscene __instance)
    {
        if (!GameStates.IsModHost) return;
        _ = new LateTask(() =>
        {
            if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
            {
                var color = ColorUtility.TryParseHtmlString("#f55252", out var c) ? c : new(255, 255, 255, 255);
                CustomRoles role = PlayerControl.LocalPlayer.GetCustomRole();
                __instance.YouAreText.color = color;
                __instance.RoleText.text = Utils.GetRoleName(role);
                __instance.RoleText.color = Utils.GetRoleColor(role);
                __instance.RoleBlurbText.color = color;
                __instance.RoleBlurbText.text = PlayerControl.LocalPlayer.GetRoleInfo();
            }
            else if (Options.CurrentGameMode == CustomGameMode.FFA)
            {
                var color = ColorUtility.TryParseHtmlString("#00ffff", out var c) ? c : new(255, 255, 255, 255);
                __instance.YouAreText.transform.gameObject.SetActive(false);
                __instance.RoleText.text = "FREE FOR ALL";
                __instance.RoleText.color = color;
                __instance.RoleBlurbText.color = color;
                __instance.RoleBlurbText.text = "KILL EVERYONE TO WIN";
            }
            else
            {
                CustomRoles role = PlayerControl.LocalPlayer.GetCustomRole();
                if (!role.IsVanilla())
                {
                    __instance.YouAreText.color = Utils.GetRoleColor(role);
                    __instance.RoleText.text = Utils.GetRoleName(role);
                    __instance.RoleText.color = Utils.GetRoleColor(role);
                    __instance.RoleBlurbText.color = Utils.GetRoleColor(role);
                    __instance.RoleBlurbText.text = "<size=50%>" + PlayerControl.LocalPlayer.GetRoleInfo() + "</size>";
                }
                for (int i = 0; i < Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].SubRoles.Count; i++)
                {
                    CustomRoles subRole = Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].SubRoles[i];
                    __instance.RoleBlurbText.text += "\n<size=30%>" + Utils.ColorString(Utils.GetRoleColor(subRole), GetString($"{subRole}Info"));
                }

                if (!PlayerControl.LocalPlayer.Is(CustomRoles.Lovers) && !PlayerControl.LocalPlayer.Is(CustomRoles.Ntr) && CustomRolesHelper.RoleExist(CustomRoles.Ntr))
                    __instance.RoleBlurbText.text += "\n" + Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lovers), GetString($"{CustomRoles.Lovers}Info"));
                __instance.RoleText.text += Utils.GetSubRolesText(PlayerControl.LocalPlayer.PlayerId, false, true);
            }
        }, 0.01f, "Override Role Text");
    }
}
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
class CoBeginPatch
{
    public static void Prefix()
    {
        var logger = Logger.Handler("Info");
        logger.Info("------------显示名称------------");
        foreach (var pc in Main.AllPlayerControls)
        {
            logger.Info($"{(pc.AmOwner ? "[*]" : string.Empty),-3}{pc.PlayerId,-2}:{pc.name.PadRightV2(20)}:{pc.cosmetics.nameText.text}({Palette.ColorNames[pc.Data.DefaultOutfit.ColorId].ToString().Replace("Color", string.Empty)})");
            pc.cosmetics.nameText.text = pc.name;
        }
        logger.Info("------------职业分配------------");
        foreach (var pc in Main.AllPlayerControls)
        {
            logger.Info($"{(pc.AmOwner ? "[*]" : string.Empty),-3}{pc.PlayerId,-2}:{pc?.Data?.PlayerName?.PadRightV2(20)}:{pc.GetAllRoleName().RemoveHtmlTags()}");
        }
        logger.Info("------------运行环境------------");
        foreach (var pc in Main.AllPlayerControls)
        {
            try
            {
                var text = pc.AmOwner ? "[*]" : "   ";
                text += $"{pc.PlayerId,-2}:{pc.Data?.PlayerName?.PadRightV2(20)}:{pc.GetClient()?.PlatformData?.Platform.ToString()?.Replace("Standalone", string.Empty),-11}";
                if (Main.playerVersion.TryGetValue(pc.PlayerId, out PlayerVersion pv))
                    text += $":Mod({pv.forkId}/{pv.version}:{pv.tag})";
                else text += ":Vanilla";
                logger.Info(text);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Platform");
            }
        }
        logger.Info("------------基本设置------------");
        var tmp = GameOptionsManager.Instance.CurrentGameOptions.ToHudString(GameData.Instance ? GameData.Instance.PlayerCount : 10).Split("\r\n").Skip(1);
        foreach (var t in tmp) logger.Info(t);
        logger.Info("------------详细设置------------");
        for (int i = 0; i < OptionItem.AllOptions.Count; i++)
        {
            OptionItem o = OptionItem.AllOptions[i];
            if (!o.IsHiddenOn(Options.CurrentGameMode) && (o.Parent == null ? !o.GetString().Equals("0%") : o.Parent.GetBool()))
                logger.Info($"{(o.Parent == null ? o.GetName(true, true).RemoveHtmlTags().PadRightV2(40) : $"┗ {o.GetName(true, true).RemoveHtmlTags()}".PadRightV2(41))}:{o.GetString().RemoveHtmlTags()}");
        }

        logger.Info("-------------其它信息-------------");
        logger.Info($"玩家人数: {Main.AllPlayerControls.Count()}");
        Main.AllPlayerControls.Do(x => Main.PlayerStates[x.PlayerId].InitTask(x));
        GameData.Instance.RecomputeTaskCounts();
        TaskState.InitialTotalTasks = GameData.Instance.TotalTasks;

        Utils.NotifyRoles();

        GameStates.InGame = true;
    }
}
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate))]
class BeginCrewmatePatch
{
    public static bool Prefix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> teamToDisplay)
    {
        if (PlayerControl.LocalPlayer.Is(CustomRoleTypes.Neutral) && !PlayerControl.LocalPlayer.Is(CustomRoles.Parasite))
        {
            teamToDisplay = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            teamToDisplay.Add(PlayerControl.LocalPlayer);
        }
        if (PlayerControl.LocalPlayer.Is(CustomRoleTypes.Neutral) && !PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor))
        {
            teamToDisplay = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            teamToDisplay.Add(PlayerControl.LocalPlayer);
        }
        else if (PlayerControl.LocalPlayer.Is(CustomRoles.Madmate))
        {
            teamToDisplay = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            teamToDisplay.Add(PlayerControl.LocalPlayer);
            __instance.BeginImpostor(teamToDisplay);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return false;
        }
        else if (PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor))
        {
            teamToDisplay = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            teamToDisplay.Add(PlayerControl.LocalPlayer);
            __instance.BeginImpostor(teamToDisplay);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return false;
        }
        else if (PlayerControl.LocalPlayer.GetCustomRole().IsMadmate())
        {
            teamToDisplay = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            teamToDisplay.Add(PlayerControl.LocalPlayer);
            __instance.BeginImpostor(teamToDisplay);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return false;
        }

        return true;
    }
    public static void Postfix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> teamToDisplay)
    {
        //チーム表示変更
        CustomRoles role = PlayerControl.LocalPlayer.GetCustomRole();

        __instance.ImpostorText.gameObject.SetActive(false);
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
                __instance.TeamTitle.text = GetString("TeamNeutral");
                __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(127, 140, 141, byte.MaxValue);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
                __instance.ImpostorText.gameObject.SetActive(true);
                __instance.ImpostorText.text = GetString("SubText.Neutral");
                break;
        }
        switch (role)
        {
            case CustomRoles.Terrorist:
            case CustomRoles.Bomber:
            case CustomRoles.Nuker:
                var sound = ShipStatus.Instance.CommonTasks.Where(task => task.TaskType == TaskTypes.FixWiring).FirstOrDefault().MinigamePrefab.OpenSound;
                PlayerControl.LocalPlayer.Data.Role.IntroSound = sound;
                break;

            case CustomRoles.Sheriff:
            case CustomRoles.Veteran:
                var sound2 = ShipStatus.Instance.CommonTasks.Where(task => task.TaskType == TaskTypes.PutAwayPistols).FirstOrDefault().MinigamePrefab.OpenSound;
                PlayerControl.LocalPlayer.Data.Role.IntroSound = sound2;
                break;

            case CustomRoles.Cleaner:
            case CustomRoles.Cleanser:
                var sound3 = ShipStatus.Instance.CommonTasks.Where(task => task.TaskType == TaskTypes.PolishRuby).FirstOrDefault().MinigamePrefab.OpenSound;
                PlayerControl.LocalPlayer.Data.Role.IntroSound = sound3;
                break;

            case CustomRoles.Dictator:
            case CustomRoles.Lawyer:
            case CustomRoles.Judge:
            case CustomRoles.Mayor:
                var sound4 = ShipStatus.Instance.CommonTasks.Where(task => task.TaskType == TaskTypes.FixShower).FirstOrDefault().MinigamePrefab.OpenSound;
                PlayerControl.LocalPlayer.Data.Role.IntroSound = sound4;
                break;

            case CustomRoles.Monitor:
            case CustomRoles.AntiAdminer:
                var sound5 = ShipStatus.Instance.CommonTasks.Where(task => task.TaskType == TaskTypes.FixComms).FirstOrDefault().MinigamePrefab.OpenSound;
                PlayerControl.LocalPlayer.Data.Role.IntroSound = sound5;
                break;

            case CustomRoles.EvilTracker:
            case CustomRoles.Tracefinder:
            case CustomRoles.Tracker:
            case CustomRoles.Bloodhound:
            case CustomRoles.Mortician:
            case CustomRoles.Lighter:
                var sound8 = ShipStatus.Instance.CommonTasks.Where(task => task.TaskType == TaskTypes.DivertPower).FirstOrDefault().MinigamePrefab.OpenSound;
                PlayerControl.LocalPlayer.Data.Role.IntroSound = sound8;
                break;

            case CustomRoles.Oracle:
            case CustomRoles.Divinator:
            case CustomRoles.Mediumshiper:
            case CustomRoles.DovesOfNeace:
            case CustomRoles.Spiritualist:
            case CustomRoles.Spiritcaller:
            case CustomRoles.Farseer:
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.GuardianAngel);
                break;

            case CustomRoles.Alchemist:
                var sound7 = ShipStatus.Instance.CommonTasks.Where(task => task.TaskType == TaskTypes.DevelopPhotos).FirstOrDefault().MinigamePrefab.OpenSound;
                PlayerControl.LocalPlayer.Data.Role.IntroSound = sound7;
                break;

            case CustomRoles.Deputy:
            case CustomRoles.Jailor:
                var sound6 = ShipStatus.Instance.CommonTasks.Where(task => task.TaskType == TaskTypes.UnlockSafe).FirstOrDefault().MinigamePrefab.OpenSound;
                PlayerControl.LocalPlayer.Data.Role.IntroSound = sound6;
                break;

            case CustomRoles.Workaholic:
            case CustomRoles.Speedrunner:
            case CustomRoles.Snitch:
                PlayerControl.LocalPlayer.Data.Role.IntroSound = DestroyableSingleton<HudManager>.Instance.TaskCompleteSound;
                break;

            case CustomRoles.TaskManager:
                PlayerControl.LocalPlayer.Data.Role.IntroSound = DestroyableSingleton<HudManager>.Instance.TaskUpdateSound;
                break;

            case CustomRoles.Opportunist:
            case CustomRoles.FFF:
            case CustomRoles.Revolutionist:
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Crewmate);
                break;

            case CustomRoles.SabotageMaster:
            case CustomRoles.Engineer:
            case CustomRoles.EngineerTOHE:
            case CustomRoles.Inhibitor:
            case CustomRoles.Saboteur:
            case CustomRoles.Provocateur:
                PlayerControl.LocalPlayer.Data.Role.IntroSound = ShipStatus.Instance.SabotageSound;
                break;

            case CustomRoles.Doctor:
            case CustomRoles.Medic:
                PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Scientist);
                break;

            case CustomRoles.GM:
                __instance.TeamTitle.text = Utils.GetRoleName(role);
                __instance.TeamTitle.color = Utils.GetRoleColor(role);
                __instance.BackgroundBar.material.color = Utils.GetRoleColor(role);
                __instance.ImpostorText.gameObject.SetActive(false);
                PlayerControl.LocalPlayer.Data.Role.IntroSound = DestroyableSingleton<HudManager>.Instance.TaskCompleteSound;
                break;
            case CustomRoles.SwordsMan:
            case CustomRoles.Minimalism:
            //case CustomRoles.Reverie:
            case CustomRoles.NiceGuesser:
                PlayerControl.LocalPlayer.Data.Role.IntroSound = PlayerControl.LocalPlayer.KillSfx;
                break;
            case CustomRoles.Swooper:
            case CustomRoles.Wraith:
            case CustomRoles.Chameleon:
                PlayerControl.LocalPlayer.Data.Role.IntroSound = PlayerControl.LocalPlayer.MyPhysics.ImpostorDiscoveredSound;
                break;
            case CustomRoles.Addict:
            case CustomRoles.Ventguard:
                PlayerControl.LocalPlayer.Data.Role.IntroSound = ShipStatus.Instance.VentEnterSound;
                break;
            case CustomRoles.ParityCop:
            case CustomRoles.NiceEraser:
            case CustomRoles.TimeManager:
                PlayerControl.LocalPlayer.Data.Role.IntroSound = HudManager.Instance.Chat.messageSound;
                break;
            case CustomRoles.Demolitionist:
            case CustomRoles.TimeMaster:
            case CustomRoles.Grenadier:
            case CustomRoles.Miner:
            case CustomRoles.Disperser:
                PlayerControl.LocalPlayer.Data.Role.IntroSound = ShipStatus.Instance.VentMoveSounds.FirstOrDefault();
                break;
        }

        if (PlayerControl.LocalPlayer.Is(CustomRoles.Madmate))
        {
            __instance.TeamTitle.text = GetString("TeamMadmate");
            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 25, 25, byte.MaxValue);
            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = GetString("SubText.Madmate");
        }

        if (PlayerControl.LocalPlayer.Is(CustomRoles.Parasite))
        {
            __instance.TeamTitle.text = GetString("TeamMadmate");
            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 25, 25, byte.MaxValue);
            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = GetString("SubText.Madmate");
        }

        if (PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor))
        {
            __instance.TeamTitle.text = GetString("TeamMadmate");
            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(255, 25, 25, byte.MaxValue);
            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Impostor);
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = GetString("SubText.Madmate");
        }

        if (Options.CurrentGameMode == CustomGameMode.SoloKombat)
        {
            var color = ColorUtility.TryParseHtmlString("#f55252", out var c) ? c : new(255, 255, 255, 255);
            __instance.TeamTitle.text = Utils.GetRoleName(role);
            __instance.TeamTitle.color = Utils.GetRoleColor(role);
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = GetString("ModeSoloKombat");
            __instance.BackgroundBar.material.color = color;
            PlayerControl.LocalPlayer.Data.Role.IntroSound = DestroyableSingleton<HnSImpostorScreamSfx>.Instance.HnSOtherImpostorTransformSfx;
        }
        if (Options.CurrentGameMode == CustomGameMode.FFA)
        {
            __instance.TeamTitle.text = "FREE FOR ALL";
            __instance.TeamTitle.color = __instance.BackgroundBar.material.color = new Color32(0, 255, 255, byte.MaxValue);
            PlayerControl.LocalPlayer.Data.Role.IntroSound = GetIntroSound(RoleTypes.Shapeshifter);
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = "KILL EVERYONE TO WIN";
        }

        if (Input.GetKey(KeyCode.RightShift))
        {
            __instance.TeamTitle.text = "明天就跑路啦";
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = "嘿嘿嘿嘿嘿嘿";
            __instance.TeamTitle.color = Color.cyan;
            StartFadeIntro(__instance, Color.cyan, Color.yellow);
        }
        if (Input.GetKey(KeyCode.RightControl))
        {
            __instance.TeamTitle.text = "警告";
            __instance.ImpostorText.gameObject.SetActive(true);
            __instance.ImpostorText.text = "请远离无知的玩家";
            __instance.TeamTitle.color = Color.magenta;
            StartFadeIntro(__instance, Color.magenta, Color.magenta);
        }
    }
    public static AudioClip GetIntroSound(RoleTypes roleType)
    {
        return RoleManager.Instance.AllRoles.Where((role) => role.Role == roleType).FirstOrDefault().IntroSound;
    }
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
}
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor))]
class BeginImpostorPatch
{
    public static bool Prefix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> yourTeam)
    {
        var role = PlayerControl.LocalPlayer.GetCustomRole();
        if (role is CustomRoles.Crewpostor)
        {
            yourTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            yourTeam.Add(PlayerControl.LocalPlayer);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return true;
        }
        else if (PlayerControl.LocalPlayer.Is(CustomRoles.Madmate))
        {
            yourTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            yourTeam.Add(PlayerControl.LocalPlayer);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return true;
        }
        else if (PlayerControl.LocalPlayer.Is(CustomRoles.Parasite))
        {
            yourTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            yourTeam.Add(PlayerControl.LocalPlayer);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return true;
        }
        else if (PlayerControl.LocalPlayer.Is(CustomRoles.Crewpostor))
        {
            yourTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            yourTeam.Add(PlayerControl.LocalPlayer);
            __instance.overlayHandle.color = Palette.ImpostorRed;
            return true;
        }
        else if (role is CustomRoles.Sheriff or CustomRoles.Jailor or CustomRoles.SwordsMan or CustomRoles.Medic/* or CustomRoles.Counterfeiter*/ or CustomRoles.Witness or CustomRoles.Monarch or CustomRoles.Farseer or CustomRoles.Admirer or CustomRoles.Deputy)
        {
            yourTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            yourTeam.Add(PlayerControl.LocalPlayer);
            foreach (var pc in Main.AllPlayerControls.Where(x => !x.AmOwner)) yourTeam.Add(pc);
            __instance.BeginCrewmate(yourTeam);
            __instance.overlayHandle.color = Palette.CrewmateBlue;
            return false;
        }
        else if (role is CustomRoles.Romantic or CustomRoles.RuthlessRomantic or CustomRoles.VengefulRomantic or CustomRoles.NSerialKiller or CustomRoles.HeadHunter or CustomRoles.Vengeance or CustomRoles.Imitator or CustomRoles.Werewolf or CustomRoles.Jackal or CustomRoles.CursedSoul or CustomRoles.Amnesiac or CustomRoles.Arsonist or CustomRoles.Sidekick or CustomRoles.Innocent or CustomRoles.Pelican or CustomRoles.Pursuer or CustomRoles.Revolutionist or CustomRoles.FFF or CustomRoles.Gamer or CustomRoles.Glitch or CustomRoles.Juggernaut or CustomRoles.DarkHide or CustomRoles.Provocateur or CustomRoles.BloodKnight or CustomRoles.NSerialKiller or CustomRoles.Maverick/* or CustomRoles.NWitch*/ or CustomRoles.Totocalcio or CustomRoles.Succubus or CustomRoles.Pelican or CustomRoles.Infectious or CustomRoles.Virus or CustomRoles.Pickpocket or CustomRoles.Traitor or CustomRoles.PlagueBearer or CustomRoles.Pestilence or CustomRoles.Spiritcaller)
        {
            yourTeam = new Il2CppSystem.Collections.Generic.List<PlayerControl>();
            yourTeam.Add(PlayerControl.LocalPlayer);
            foreach (var pc in Main.AllPlayerControls.Where(x => !x.AmOwner)) yourTeam.Add(pc);
            __instance.BeginCrewmate(yourTeam);
            __instance.overlayHandle.color = new Color32(127, 140, 141, byte.MaxValue);
            return false;
        }
        BeginCrewmatePatch.Prefix(__instance, ref yourTeam);
        return true;
    }
    public static void Postfix(IntroCutscene __instance, ref Il2CppSystem.Collections.Generic.List<PlayerControl> yourTeam)
    {
        BeginCrewmatePatch.Postfix(__instance, ref yourTeam);
    }
}
[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy))]
class IntroCutsceneDestroyPatch
{
    public static void Postfix(IntroCutscene __instance)
    {
        if (!GameStates.IsInGame) return;
        Main.introDestroyed = true;
        if (AmongUsClient.Instance.AmHost)
        {
            if (Main.NormalOptions.MapId != 4)
            {
                Main.AllPlayerControls.Do(pc => pc.RpcResetAbilityCooldown());
                if (Options.StartingKillCooldown.GetInt() != 10 && Options.StartingKillCooldown.GetInt() > 0)
                    _ = new LateTask(() =>
                    {
                        Main.AllPlayerControls.Do(x => x.ResetKillCooldown());
                        Main.AllPlayerControls.Where(x => Main.AllPlayerKillCooldown[x.PlayerId] != 7f && Main.AllPlayerKillCooldown[x.PlayerId] != 7.5f).Do(pc => pc.SetKillCooldown(Options.StartingKillCooldown.GetInt()));
                    }, 0.01f, "FixKillCooldownTask");
                else if (Options.FixFirstKillCooldown.GetBool() && Options.CurrentGameMode != CustomGameMode.SoloKombat && Options.CurrentGameMode != CustomGameMode.FFA)
                    _ = new LateTask(() =>
                    {
                        Main.AllPlayerControls.Do(x => x.ResetKillCooldown());
                        Main.AllPlayerControls.Where(x => (Main.AllPlayerKillCooldown[x.PlayerId] - 2f) > 0f).Do(pc => pc.SetKillCooldown(Main.AllPlayerKillCooldown[pc.PlayerId] - 2f));
                    }, 2f, "FixKillCooldownTask");
            }
            _ = new LateTask(() => Main.AllPlayerControls.Do(pc => pc.RpcSetRoleDesync(RoleTypes.Shapeshifter, -3)), 2f, "SetImpostorForServer");
            if (PlayerControl.LocalPlayer.Is(CustomRoles.GM))
            {
                PlayerControl.LocalPlayer.RpcExile();
                Main.PlayerStates[PlayerControl.LocalPlayer.PlayerId].SetDead();
            }
            if (Options.RandomSpawn.GetBool() || Options.CurrentGameMode == CustomGameMode.SoloKombat || Options.CurrentGameMode == CustomGameMode.FFA)
            {
                RandomSpawn.SpawnMap map;
                switch (Main.NormalOptions.MapId)
                {
                    case 0:
                        map = new RandomSpawn.SkeldSpawnMap();
                        Main.AllPlayerControls.Do(map.RandomTeleport);
                        break;
                    case 1:
                        map = new RandomSpawn.MiraHQSpawnMap();
                        Main.AllPlayerControls.Do(map.RandomTeleport);
                        break;
                    case 2:
                        map = new RandomSpawn.PolusSpawnMap();
                        Main.AllPlayerControls.Do(map.RandomTeleport);
                        break;
                }
            }

            var amDesyncImpostor = Main.ResetCamPlayerList.Contains(PlayerControl.LocalPlayer.PlayerId);
            if (amDesyncImpostor)
            {
                PlayerControl.LocalPlayer.Data.Role.AffectedByLightAffectors = false;
            }
        }
        Logger.Info("OnDestroy", "IntroCutscene");
    }
}