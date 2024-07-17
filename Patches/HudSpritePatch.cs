using System;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Neutral;
using EHR.Patches;
using HarmonyLib;
using UnityEngine;

namespace EHR;

public static class CustomButton
{
    public static Sprite Get(string name) => Utils.LoadSprite($"EHR.Resources.Images.Skills.{name}.png", 115f);
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
    private static Sprite Report;

    private static long LastErrorTime;

    public static void Postfix(HudManager __instance)
    {
        try
        {
            var player = PlayerControl.LocalPlayer;
            if (player == null) return;
            if (!SetHudActivePatch.IsActive || !player.IsAlive()) return;
            if (!AmongUsClient.Instance.IsGameStarted || !Main.IntroDestroyed)
            {
                Kill = null;
                Ability = null;
                Vent = null;
                Sabotage = null;
                Pet = null;
                Report = null;
                return;
            }

            bool shapeshifting = player.IsShifted();

            if (!Kill) Kill = __instance.KillButton.graphic.sprite;
            if (!Ability) Ability = __instance.AbilityButton.graphic.sprite;
            if (!Vent) Vent = __instance.ImpostorVentButton.graphic.sprite;
            if (!Sabotage) Sabotage = __instance.SabotageButton.graphic.sprite;
            if (!Pet) Pet = __instance.PetButton.graphic.sprite;
            if (!Report) Report = __instance.ReportButton.graphic.sprite;

            Sprite newKillButton = Kill;
            Sprite newAbilityButton = Ability;
            Sprite newVentButton = Vent;
            Sprite newSabotageButton = Sabotage;
            Sprite newPetButton = Pet;
            Sprite newReportButton = Report;

            if (!Main.EnableCustomButton.Value || !Main.ProcessShapeshifts || Mastermind.ManipulatedPlayers.ContainsKey(player.PlayerId)) goto EndOfSelectImg;

            switch (player.GetCustomRole())
            {
                case CustomRoles.Echo:
                    newAbilityButton = player.IsShifted() ? Kill : CustomButton.Get("Puttpuer");
                    break;
                case CustomRoles.Shifter:
                    newKillButton = CustomButton.Get("Swap");
                    break;
                case CustomRoles.Changeling:
                    newAbilityButton = CustomButton.Get("GlitchMimic");
                    break;
                case CustomRoles.Vulture:
                    newReportButton = CustomButton.Get("Eat");
                    break;
                case CustomRoles.Sentry:
                    newPetButton = CustomButton.Get("Sentry");
                    break;
                case CustomRoles.Commander:
                    newAbilityButton = CustomButton.Get("Commander");
                    break;
                case CustomRoles.Amnesiac:
                    if (Amnesiac.RememberMode.GetValue() == 0) newKillButton = CustomButton.Get("AmnesiacKill");
                    else newReportButton = CustomButton.Get("AmnesiacReport");
                    break;
                case CustomRoles.Assassin:
                case CustomRoles.Undertaker:
                    if (Main.PlayerStates[player.PlayerId].Role is not Assassin assassin) break;
                    if (Options.UsePets.GetBool())
                    {
                        newKillButton = CustomButton.Get("Mark");
                        if (assassin.MarkedPlayer != byte.MaxValue)
                            newPetButton = CustomButton.Get("Assassinate");
                    }
                    else
                    {
                        if (!shapeshifting)
                        {
                            newKillButton = CustomButton.Get("Mark");
                            if (assassin.MarkedPlayer != byte.MaxValue)
                                newAbilityButton = CustomButton.Get("Assassinate");
                        }
                    }

                    break;
                case CustomRoles.Gaulois:
                    newKillButton = CustomButton.Get("Gaulois");
                    break;
                case CustomRoles.Glitch:
                    if (Main.PlayerStates[player.PlayerId].Role is not Glitch gc) break;
                    if (gc.KCDTimer > 0 && gc.HackCDTimer <= 0) newKillButton = CustomButton.Get("GlitchHack");
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
                    if (Main.PlayerStates[player.PlayerId].Role is not FireWorks fw) break;
                    newAbilityButton = CustomButton.Get(fw.nowFireWorksCount == 0 ? "FireworkD" : "FireworkP");
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
                case CustomRoles.Mayor when Mayor.MayorHasPortableButton.GetBool():
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
                    newKillButton = CustomButton.Get(!Romantic.HasPickedPartner ? "Romance" : "RomanticProtect");
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
                case CustomRoles.Analyst:
                case CustomRoles.Witness:
                    newKillButton = CustomButton.Get("Examine");
                    break;
                case CustomRoles.Postman:
                    newKillButton = CustomButton.Get("Deliver");
                    break;
                case CustomRoles.Pelican:
                    newKillButton = CustomButton.Get("Vulture");
                    break;
                case CustomRoles.TimeMaster:
                    if (Options.UsePets.GetBool())
                        newPetButton = CustomButton.Get("Time Master");
                    else
                        newAbilityButton = CustomButton.Get("Time Master");
                    break;
                case CustomRoles.Sheriff:
                    newKillButton = CustomButton.Get("Kill");
                    break;
                case CustomRoles.Swiftclaw:
                    if (Options.UsePets.GetBool())
                        newPetButton = CustomButton.Get("Dash");
                    else
                        newAbilityButton = CustomButton.Get("Dash");
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
                        if (Warlock.IsCurseAndKill.TryGetValue(player.PlayerId, out bool curse) && curse)
                            newAbilityButton = CustomButton.Get("CurseKill");
                    }
                    else if (!shapeshifting)
                    {
                        newKillButton = CustomButton.Get("Curse");
                        if (Warlock.IsCurseAndKill.TryGetValue(player.PlayerId, out bool curse) && curse)
                            newAbilityButton = CustomButton.Get("CurseKill");
                    }

                    break;
                default:
                    if (player.GetCustomRole().UsesPetInsteadOfKill())
                    {
                        newPetButton = __instance.KillButton.graphic.sprite;
                    }

                    break;
            }

            if (player.GetCustomRole().UsesPetInsteadOfKill())
            {
                newPetButton = newKillButton;
            }


            EndOfSelectImg:

            __instance.KillButton.graphic.sprite = newKillButton;
            __instance.AbilityButton.graphic.sprite = newAbilityButton;
            __instance.ImpostorVentButton.graphic.sprite = newVentButton;
            __instance.SabotageButton.graphic.sprite = newSabotageButton;
            __instance.PetButton.graphic.sprite = newPetButton;
            __instance.ReportButton.graphic.sprite = newReportButton;

            __instance.KillButton.graphic.SetCooldownNormalizedUvs();
            __instance.AbilityButton.graphic.SetCooldownNormalizedUvs();
            __instance.ImpostorVentButton.graphic.SetCooldownNormalizedUvs();
            __instance.SabotageButton.graphic.SetCooldownNormalizedUvs();
            __instance.PetButton.graphic.SetCooldownNormalizedUvs();
            __instance.ReportButton.graphic.SetCooldownNormalizedUvs();
        }
        catch (Exception e)
        {
            if (Utils.TimeStamp - LastErrorTime > 10)
            {
                LastErrorTime = Utils.TimeStamp;
                Utils.ThrowException(e);
            }
        }
    }
}