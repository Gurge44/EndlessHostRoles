using HarmonyLib;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;

namespace TOHE;

public static class CustomButton
{
    public static Sprite Get(string name) => Utils.LoadSprite($"TOHE.Resources.Images.Skills.{name}.png", 115f);
}

[HarmonyPriority(520)]
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class HudSpritePatch
{
    private static Sprite Kill;
    private static Sprite Ability;
    private static Sprite Vent;
    private static Sprite Sabotage;
    private static Sprite Pet;
    public static void Postfix(HudManager __instance)
    {
        var player = PlayerControl.LocalPlayer;
        if (player == null || !GameStates.IsModHost) return;
        if (!SetHudActivePatch.IsActive || !player.IsAlive()) return;
        if (!AmongUsClient.Instance.IsGameStarted || !Main.introDestroyed)
        {
            Kill = null;
            Ability = null;
            Vent = null;
            Sabotage = null;
            Pet = null;
            return;
        }

        bool shapeshifting = player.IsShifted();

        if (!Kill) Kill = __instance.KillButton.graphic.sprite;
        if (!Ability) Ability = __instance.AbilityButton.graphic.sprite;
        if (!Vent) Vent = __instance.ImpostorVentButton.graphic.sprite;
        if (!Sabotage) Sabotage = __instance.SabotageButton.graphic.sprite;
        if (!Pet) Pet = __instance.PetButton.graphic.sprite;

        Sprite newKillButton = Kill;
        Sprite newAbilityButton = Ability;
        Sprite newVentButton = Vent;
        Sprite newSabotageButton = Sabotage;
        Sprite newPetButton = Pet;

        if (!Main.EnableCustomButton.Value || !Main.ProcessShapeshifts) goto EndOfSelectImg;

        if (!Mastermind.ManipulatedPlayers.ContainsKey(player.PlayerId))
        {
            switch (player.GetCustomRole())
            {
                case CustomRoles.Assassin:
                    if (Options.UsePets.GetBool())
                    {
                        newKillButton = CustomButton.Get("Mark");
                        if (Assassin.MarkedPlayer.ContainsKey(player.PlayerId))
                            newPetButton = CustomButton.Get("Assassinate");
                    }
                    else
                    {
                        if (!shapeshifting)
                        {
                            newKillButton = CustomButton.Get("Mark");
                            if (Assassin.MarkedPlayer.ContainsKey(player.PlayerId))
                                newAbilityButton = CustomButton.Get("Assassinate");
                        }
                    }
                    break;
                case CustomRoles.Undertaker:
                    if (Options.UsePets.GetBool())
                    {
                        newKillButton = CustomButton.Get("Mark");
                        if (Undertaker.MarkedPlayer.ContainsKey(player.PlayerId))
                            newPetButton = CustomButton.Get("Assassinate");
                    }
                    else
                    {
                        if (!shapeshifting)
                        {
                            newKillButton = CustomButton.Get("Mark");
                            if (Undertaker.MarkedPlayer.ContainsKey(player.PlayerId))
                                newAbilityButton = CustomButton.Get("Assassinate");
                        }
                    }
                    break;
                case CustomRoles.Glitch:
                    if (Glitch.KCDTimer > 0 && Glitch.HackCDTimer <= 0) newKillButton = CustomButton.Get("GlitchHack");
                    newSabotageButton = CustomButton.Get("GlitchMimic");
                    break;
                case CustomRoles.Jester:
                    newAbilityButton = CustomButton.Get("JesterVent");
                    break;
                case CustomRoles.Disperser:
                    if (Options.UsePets.GetBool())
                    {
                        newPetButton = CustomButton.Get("Disperse");
                    }
                    else if (!shapeshifting)
                    {
                        newAbilityButton = CustomButton.Get("Disperse");
                    }
                    break;
                case CustomRoles.ImperiusCurse:
                case CustomRoles.Twister:
                    if (player.Is(CustomRoles.Twister) && Options.UsePets.GetBool())
                    {
                        newPetButton = CustomButton.Get("Transport");
                    }
                    else if (!shapeshifting)
                    {
                        newAbilityButton = CustomButton.Get("Transport");
                    }
                    break;
                case CustomRoles.Deputy:
                    newKillButton = CustomButton.Get("Handcuff");
                    break;
                case CustomRoles.Pursuer:
                    newKillButton = CustomButton.Get("Pursuer");
                    break;
                case CustomRoles.Alchemist:
                    if (Options.UsePets.GetBool())
                        newPetButton = CustomButton.Get("Drink");
                    else
                        newAbilityButton = CustomButton.Get("Drink");
                    break;
                case CustomRoles.Jailor:
                    newKillButton = CustomButton.Get("Jail");
                    break;
                case CustomRoles.Penguin:
                    newAbilityButton = CustomButton.Get("Timer");
                    break;
                case CustomRoles.Hitman:
                    newAbilityButton = CustomButton.Get("TargetIcon");
                    break;
                case CustomRoles.Revolutionist:
                    newKillButton = CustomButton.Get("Tag");
                    break;
                case CustomRoles.DonutDelivery:
                    newKillButton = CustomButton.Get("Donut");
                    break;
                case CustomRoles.Sapper:
                case CustomRoles.Bomber:
                case CustomRoles.Nuker:
                    if (Options.UsePets.GetBool())
                        newPetButton = CustomButton.Get("Bomb");
                    else
                        newAbilityButton = CustomButton.Get("Bomb");
                    break;
                case CustomRoles.Camouflager:
                    newAbilityButton = CustomButton.Get("Camo");
                    break;
                case CustomRoles.Agitater:
                    newKillButton = CustomButton.Get("Pass");
                    break;
                case CustomRoles.Arsonist:
                    newKillButton = CustomButton.Get("Douse");
                    if (player.IsDouseDone() || (Options.ArsonistCanIgniteAnytime.GetBool() && Utils.GetDousedPlayerCount(player.PlayerId).Item1 >= Options.ArsonistMinPlayersToIgnite.GetInt())) newVentButton = CustomButton.Get("Ignite");
                    break;
                case CustomRoles.Pyromaniac:
                    newKillButton = CustomButton.Get("Pyromaniac");
                    break;
                case CustomRoles.FireWorks:
                    if (FireWorks.nowFireWorksCount[player.PlayerId] == 0)
                        newAbilityButton = CustomButton.Get("FireworkD");
                    else
                        newAbilityButton = CustomButton.Get("FireworkP");
                    break;
                case CustomRoles.Hacker:
                    newAbilityButton = CustomButton.Get("Hack");
                    break;
                case CustomRoles.Hangman:
                    if (shapeshifting) newAbilityButton = CustomButton.Get("Hangman");
                    break;
                case CustomRoles.Paranoia:
                    if (Options.UsePets.GetBool())
                        newPetButton = CustomButton.Get("Paranoid");
                    else
                        newAbilityButton = CustomButton.Get("Paranoid");
                    break;
                case CustomRoles.Mayor:
                    if (Options.UsePets.GetBool())
                        newPetButton = CustomButton.Get("Button");
                    else
                        newAbilityButton = CustomButton.Get("Button");
                    break;
                case CustomRoles.Puppeteer:
                    newKillButton = CustomButton.Get("Puttpuer");
                    break;
                case CustomRoles.Aid:
                case CustomRoles.Medic:
                    newKillButton = CustomButton.Get("Shield");
                    break;
                case CustomRoles.Gangster:
                    if (Gangster.CanRecruit(player.PlayerId)) newKillButton = CustomButton.Get("Sidekick");
                    break;
                case CustomRoles.Succubus:
                    newKillButton = CustomButton.Get("Subbus");
                    break;
                case CustomRoles.Innocent:
                    newKillButton = CustomButton.Get("Suidce");
                    break;
                case CustomRoles.EvilTracker:
                    newAbilityButton = CustomButton.Get("Track");
                    break;
                case CustomRoles.Vampire:
                    newKillButton = CustomButton.Get("Bite");
                    break;
                case CustomRoles.Veteran:
                    if (Options.UsePets.GetBool())
                        newPetButton = CustomButton.Get("Veteran");
                    else
                        newAbilityButton = CustomButton.Get("Veteran");
                    break;
                case CustomRoles.Lighter:
                    if (Options.UsePets.GetBool())
                        newPetButton = CustomButton.Get("Lighter");
                    else
                        newAbilityButton = CustomButton.Get("Lighter");
                    break;
                case CustomRoles.SecurityGuard:
                    if (Options.UsePets.GetBool())
                        newPetButton = CustomButton.Get("BlockSabo");
                    else
                        newAbilityButton = CustomButton.Get("BlockSabo");
                    break;
                case CustomRoles.Ventguard:
                    newAbilityButton = CustomButton.Get("Block");
                    break;
                case CustomRoles.Romantic:
                    newKillButton = CustomButton.Get(Romantic.BetTimes.TryGetValue(player.PlayerId, out var times) && times >= 1 ? "Romance" : "RomanticProtect");
                    break;
                case CustomRoles.VengefulRomantic:
                    newKillButton = CustomButton.Get("RomanticKill");
                    break;
                case CustomRoles.Miner:
                    if (!Options.UsePets.GetBool())
                        newAbilityButton = CustomButton.Get("Mine");
                    else
                        newPetButton = CustomButton.Get("Mine");
                    break;
                case CustomRoles.Analyzer:
                case CustomRoles.Witness:
                    newKillButton = CustomButton.Get("Examine");
                    break;
                case CustomRoles.Postman:
                    newKillButton = CustomButton.Get("Deliver");
                    break;
                case CustomRoles.Pelican:
                    newKillButton = CustomButton.Get("Vulture");
                    break;
                //case CustomRoles.CursedSoul:
                //    newKillButton = CustomButton.Get("Soul");
                //    break;
                case CustomRoles.TimeMaster:
                    if (Options.UsePets.GetBool())
                        newPetButton = CustomButton.Get("Time Master");
                    else
                        newAbilityButton = CustomButton.Get("Time Master");
                    break;
                //case CustomRoles.Mario:
                //    newAbilityButton = CustomButton.Get("Happy");
                //    break;
                case CustomRoles.Sheriff:
                    newKillButton = CustomButton.Get("Kill");
                    break;
                case CustomRoles.Swooper:
                    newAbilityButton = CustomButton.Get("invisible");
                    break;
                case CustomRoles.Chameleon:
                    newAbilityButton = CustomButton.Get("invisible");
                    break;
                case CustomRoles.Wraith:
                    newAbilityButton = CustomButton.Get("invisible");
                    break;
                case CustomRoles.Escapee:
                    if (Options.UsePets.GetBool())
                        newPetButton = CustomButton.Get("abscond");
                    else
                        newAbilityButton = CustomButton.Get("abscond");
                    break;
                case CustomRoles.Farseer:
                    newKillButton = CustomButton.Get("prophecies");
                    break;
                case CustomRoles.Warlock:
                    if (Options.UsePets.GetBool())
                    {
                        newKillButton = CustomButton.Get("Curse");
                        if (Main.isCurseAndKill.TryGetValue(player.PlayerId, out bool curse) && curse)
                            newAbilityButton = CustomButton.Get("CurseKill");
                    }
                    else if (!shapeshifting)
                    {
                        newKillButton = CustomButton.Get("Curse");
                        if (Main.isCurseAndKill.TryGetValue(player.PlayerId, out bool curse) && curse)
                            newAbilityButton = CustomButton.Get("CurseKill");
                    }
                    break;
            }
        }

    EndOfSelectImg:

        __instance.KillButton.graphic.sprite = newKillButton;
        __instance.AbilityButton.graphic.sprite = newAbilityButton;
        __instance.ImpostorVentButton.graphic.sprite = newVentButton;
        __instance.SabotageButton.graphic.sprite = newSabotageButton;
        __instance.PetButton.graphic.sprite = newPetButton;

    }
}
