using AmongUs.GameOptions;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Linq;
using InnerNet;
using System;
using System.Linq;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using Mathf = UnityEngine.Mathf;

namespace TOHE.Modules;

public class PlayerGameOptionsSender(PlayerControl player) : GameOptionsSender
{
    public static void SetDirty(PlayerControl player) => SetDirty(player.PlayerId);
    public static void SetDirty(byte playerId)
    {
        foreach (var sender in AllSenders.OfType<PlayerGameOptionsSender>().Where(sender => sender.player.PlayerId == playerId).ToArray())
        {
            sender.SetDirty();
        }
    }

    public static void SetDirtyToAll()
    {
        foreach (var sender in AllSenders.OfType<PlayerGameOptionsSender>().ToArray())
        {
            sender.SetDirty();
        }
    }

    public static void SetDirtyToAllV2()
    {
        foreach (var sender in AllSenders.OfType<PlayerGameOptionsSender>().Where(sender => !sender.IsDirty && sender.player.IsAlive() && sender.player.GetCustomRole().NeedUpdateOnLights()).ToArray())
        {
            sender.SetDirty();
        }
    }

    public static void SetDirtyToAllV3()
    {
        foreach (var sender in AllSenders.OfType<PlayerGameOptionsSender>().Where(sender => !sender.IsDirty && sender.player.IsAlive() && ((Main.GrenadierBlinding.Count > 0 && (sender.player.GetCustomRole().IsImpostor() || (sender.player.GetCustomRole().IsNeutral() && Options.GrenadierCanAffectNeutral.GetBool()))) || (Main.MadGrenadierBlinding.Count > 0 && !sender.player.GetCustomRole().IsImpostorTeam() && !sender.player.Is(CustomRoles.Madmate)))).ToArray())
        {
            sender.SetDirty();
        }
    }

    public static void SetDirtyToAllV4()
    {
        foreach (var sender in AllSenders.OfType<PlayerGameOptionsSender>().Where(sender => !sender.IsDirty && sender.player.IsAlive() && sender.player.CanUseKillButton()).ToArray())
        {
            sender.SetDirty();
        }
    }

    public override IGameOptions BasedGameOptions =>
        Main.RealOptionsData.Restore(new NormalGameOptionsV07(new UnityLogger().Cast<ILogger>()).Cast<IGameOptions>());
    public override bool IsDirty { get; protected set; }

    public PlayerControl player = player;

    public void SetDirty() => IsDirty = true;

    public override void SendGameOptions()
    {
        if (player.AmOwner)
        {
            var opt = BuildGameOptions();
            foreach (var com in GameManager.Instance?.LogicComponents)
            {
                if (com.TryCast<LogicOptions>(out var lo))
                    lo.SetGameOptions(opt);
            }
            GameOptionsManager.Instance.CurrentGameOptions = opt;
        }
        else base.SendGameOptions();
    }

    public override void SendOptionsArray(Il2CppStructArray<byte> optionArray)
    {
        try
        {
            for (byte i = 0; i < GameManager.Instance?.LogicComponents?.Count; i++)
            {
                if (GameManager.Instance.LogicComponents[(Index)i].TryCast<LogicOptions>(out _))
                {
                    SendOptionsArray(optionArray, i, player.GetClientId());
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Fatal(ex.ToString(), "PlayerGameOptionsSender.SendOptionsArray");
        }
    }
    public static void RemoveSender(PlayerControl player)
    {
        var sender = AllSenders.OfType<PlayerGameOptionsSender>()
        .FirstOrDefault(sender => sender.player.PlayerId == player.PlayerId);
        if (sender == null) return;
        sender.player = null;
        AllSenders.Remove(sender);
    }
    public override IGameOptions BuildGameOptions()
    {
        try
        {
            Main.RealOptionsData ??= new OptionBackupData(GameOptionsManager.Instance.CurrentGameOptions);

            var opt = BasedGameOptions;
            AURoleOptions.SetOpt(opt);
            var state = Main.PlayerStates[player.PlayerId];
            opt.BlackOut(state.IsBlackOut);

            CustomRoles role = player.GetCustomRole();

            switch (Options.CurrentGameMode)
            {
                case CustomGameMode.FFA:
                    if (FFAManager.FFALowerVisionList.ContainsKey(player.PlayerId))
                    {
                        opt.SetVision(true);
                        opt.SetFloat(FloatOptionNames.CrewLightMod, FFAManager.FFA_LowerVision.GetFloat());
                        opt.SetFloat(FloatOptionNames.ImpostorLightMod, FFAManager.FFA_LowerVision.GetFloat());
                    }
                    else
                    {
                        opt.SetVision(true);
                        opt.SetFloat(FloatOptionNames.CrewLightMod, 1.25f);
                        opt.SetFloat(FloatOptionNames.ImpostorLightMod, 1.25f);
                    }
                    break;
                case CustomGameMode.MoveAndStop:
                    opt.SetVision(true);
                    opt.SetFloat(FloatOptionNames.CrewLightMod, 1.25f);
                    opt.SetFloat(FloatOptionNames.ImpostorLightMod, 1.25f);
                    break;
            }

            switch (role.GetCustomRoleTypes())
            {
                case CustomRoleTypes.Impostor:
                    AURoleOptions.ShapeshifterCooldown = Options.DefaultShapeshiftCooldown.GetFloat();
                    AURoleOptions.GuardianAngelCooldown = Spiritcaller.SpiritAbilityCooldown.GetFloat();
                    break;
                case CustomRoleTypes.Neutral:
                    AURoleOptions.GuardianAngelCooldown = Spiritcaller.SpiritAbilityCooldown.GetFloat();
                    break;
                case CustomRoleTypes.Crewmate:
                    AURoleOptions.GuardianAngelCooldown = Spiritcaller.SpiritAbilityCooldown.GetFloat();
                    break;
            }

            switch (role)
            {
                //case CustomRoles.Terrorist:
                case CustomRoles.SabotageMaster:
                //     case CustomRoles.Mario:
                //case CustomRoles.EngineerTOHE:
                //case CustomRoles.Phantom:
                case CustomRoles.Crewpostor:
                    //  case CustomRoles.Jester:
                    AURoleOptions.EngineerCooldown = 0f;
                    AURoleOptions.EngineerInVentMaxTime = 0f;
                    break;
                case CustomRoles.Chameleon:
                    AURoleOptions.EngineerCooldown = Chameleon.ChameleonCooldown.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = 0f;
                    break;
                //case CustomRoles.ShapeMaster:
                //    AURoleOptions.ShapeshifterCooldown = 1f;
                //    AURoleOptions.ShapeshifterLeaveSkin = false;
                //    AURoleOptions.ShapeshifterDuration = Options.ShapeMasterShapeshiftDuration.GetFloat();
                //    break;
                case CustomRoles.RiftMaker:
                    if (Options.UsePets.GetBool()) break;
                    AURoleOptions.ShapeshifterDuration = 1f;
                    AURoleOptions.ShapeshifterCooldown = RiftMaker.ShapeshiftCooldown.GetFloat();
                    AURoleOptions.ShapeshifterLeaveSkin = true;
                    break;
                case CustomRoles.Gambler:
                    if (Gambler.isVisionChange.ContainsKey(player.PlayerId))
                    {
                        opt.SetFloat(FloatOptionNames.CrewLightMod, Gambler.LowVision.GetFloat());
                        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Gambler.LowVision.GetFloat());
                    }
                    break;
                case CustomRoles.NiceHacker:
                    if (Options.UsePets.GetBool()) break;
                    AURoleOptions.EngineerCooldown = NiceHacker.AbilityCD.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = 1f;
                    break;
                case CustomRoles.Warlock:
                    if (Options.UsePets.GetBool()) break;
                    try
                    {
                        AURoleOptions.ShapeshifterCooldown = Main.isCursed ? 1f : Options.DefaultKillCooldown;
                        AURoleOptions.ShapeshifterDuration = Options.WarlockShiftDuration.GetFloat();
                    }
                    catch { }
                    break;
                case CustomRoles.Escapee:
                    if (Options.UsePets.GetBool()) break;
                    try
                    {
                        AURoleOptions.ShapeshifterCooldown = Options.EscapeeSSCD.GetFloat();
                        AURoleOptions.ShapeshifterDuration = Options.EscapeeSSDuration.GetFloat();
                    }
                    catch { }
                    break;
                case CustomRoles.Duellist:
                    Duellist.ApplyGameOptions();
                    break;
                case CustomRoles.Sniper:
                    if (Options.UsePets.GetBool()) break;
                    try
                    {
                        if (Sniper.bulletCount[player.PlayerId] > 0)
                        {
                            AURoleOptions.ShapeshifterDuration = Sniper.ShapeshiftDuration.GetFloat();
                        }
                        else
                        {
                            AURoleOptions.ShapeshifterDuration = 1f;
                            AURoleOptions.ShapeshifterCooldown = 255f;
                        }
                    }
                    catch { }
                    break;
                case CustomRoles.Miner:
                    if (Options.UsePets.GetBool()) break;
                    try
                    {
                        AURoleOptions.ShapeshifterCooldown = Options.MinerSSCD.GetFloat();
                        AURoleOptions.ShapeshifterDuration = Options.MinerSSDuration.GetFloat();
                    }
                    catch { }
                    break;
                //case CustomRoles.SerialKiller:
                //    SerialKiller.ApplyGameOptions(player);
                //    break;
                case CustomRoles.Tracefinder:
                    Tracefinder.ApplyGameOptions();
                    break;
                //case CustomRoles.BountyHunter:
                //    BountyHunter.ApplyGameOptions();
                //    break;
                case CustomRoles.Sheriff:
                case CustomRoles.SwordsMan:
                case CustomRoles.Arsonist:
                //     case CustomRoles.Minimalism:
                case CustomRoles.Innocent:
                case CustomRoles.Pelican:
                case CustomRoles.Revolutionist:
                case CustomRoles.Medic:
                case CustomRoles.Crusader:
                case CustomRoles.Provocateur:
                case CustomRoles.Monarch:
                case CustomRoles.Jailor:
                case CustomRoles.Deputy:
                //case CustomRoles.Counterfeiter:
                case CustomRoles.Aid:
                case CustomRoles.Escort:
                case CustomRoles.DonutDelivery:
                case CustomRoles.Gaulois:
                case CustomRoles.Analyzer:
                case CustomRoles.Witness:
                case CustomRoles.Succubus:
                //case CustomRoles.CursedSoul:
                case CustomRoles.Admirer:
                case CustomRoles.Amnesiac:
                    opt.SetVision(false);
                    break;
                case CustomRoles.Minimalism:
                case CustomRoles.Agitater:
                    opt.SetVision(true);
                    break;
                case CustomRoles.Pestilence:
                    opt.SetVision(PlagueBearer.PestilenceHasImpostorVision.GetBool());
                    break;
                case CustomRoles.Refugee:
                    opt.SetVision(true);
                    break;
                case CustomRoles.Doormaster:
                    if (Options.UsePets.GetBool()) break;
                    AURoleOptions.EngineerCooldown = Doormaster.VentCooldown.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = 1f;
                    break;
                case CustomRoles.Tether:
                    if (Options.UsePets.GetBool()) break;
                    AURoleOptions.EngineerCooldown = Tether.VentCooldown.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = 1f;
                    break;
                case CustomRoles.Monitor:
                    AURoleOptions.EngineerCooldown = 0f;
                    AURoleOptions.EngineerInVentMaxTime = 0f;
                    break;
                case CustomRoles.Virus:
                    opt.SetVision(Virus.ImpostorVision.GetBool());
                    break;
                case CustomRoles.Zombie:
                    opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0.2f);
                    break;
                case CustomRoles.Doctor:
                    AURoleOptions.ScientistCooldown = 0f;
                    AURoleOptions.ScientistBatteryCharge = Options.DoctorTaskCompletedBatteryCharge.GetFloat();
                    break;
                case CustomRoles.Mayor:
                    if (Options.UsePets.GetBool()) break;
                    AURoleOptions.EngineerCooldown =
                        !Main.MayorUsedButtonCount.TryGetValue(player.PlayerId, out var count) || count < Options.MayorNumOfUseButton.GetInt()
                        ? opt.GetInt(Int32OptionNames.EmergencyCooldown)
                        : 300f;
                    AURoleOptions.EngineerInVentMaxTime = 1;
                    break;
                case CustomRoles.Paranoia:
                    if (Options.UsePets.GetBool()) break;
                    AURoleOptions.EngineerCooldown =
                        !Main.ParaUsedButtonCount.TryGetValue(player.PlayerId, out var count2) || count2 < Options.ParanoiaNumOfUseButton.GetInt()
                        ? Options.ParanoiaVentCooldown.GetFloat()
                        : 300f;
                    AURoleOptions.EngineerInVentMaxTime = 1;
                    break;
                /*     case CustomRoles.Mare:
                         Mare.ApplyGameOptions(player.PlayerId);
                         break; */
                case CustomRoles.EvilTracker:
                    EvilTracker.ApplyGameOptions(player.PlayerId);
                    break;
                case CustomRoles.Blackmailer:
                    Blackmailer.ApplyGameOptions();
                    break;
                case CustomRoles.ShapeshifterTOHE:
                    AURoleOptions.ShapeshifterCooldown = Options.ShapeshiftCD.GetFloat();
                    AURoleOptions.ShapeshifterDuration = Options.ShapeshiftDur.GetFloat();
                    break;
                case CustomRoles.Bandit:
                    Bandit.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Bomber:
                    if (Options.UsePets.GetBool()) break;
                    try
                    {
                        AURoleOptions.ShapeshifterCooldown = Options.BombCooldown.GetFloat();
                        AURoleOptions.ShapeshifterDuration = 2f;
                    }
                    catch { }
                    break;
                case CustomRoles.Nuker:
                    if (Options.UsePets.GetBool()) break;
                    try
                    {
                        AURoleOptions.ShapeshifterCooldown = Options.NukeCooldown.GetFloat();
                        AURoleOptions.ShapeshifterDuration = 2f;
                    }
                    catch { }
                    break;
                case CustomRoles.Hitman:
                    AURoleOptions.ShapeshifterCooldown = Hitman.ShapeshiftCooldown.GetFloat();
                    AURoleOptions.ShapeshifterDuration = 1f;
                    break;
                case CustomRoles.CameraMan:
                    AURoleOptions.EngineerCooldown = CameraMan.VentCooldown.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = 1f;
                    break;
                case CustomRoles.Mafia:
                    AURoleOptions.ShapeshifterCooldown = Options.MafiaShapeshiftCD.GetFloat();
                    AURoleOptions.ShapeshifterDuration = Options.MafiaShapeshiftDur.GetFloat();
                    break;
                case CustomRoles.ScientistTOHE:
                    AURoleOptions.ScientistCooldown = Options.ScientistCD.GetFloat();
                    AURoleOptions.ScientistBatteryCharge = Options.ScientistDur.GetFloat();
                    break;
                case CustomRoles.Wildling:
                    if (Wildling.CanShapeshift.GetBool())
                    {
                        AURoleOptions.ShapeshifterCooldown = Wildling.ShapeshiftCD.GetFloat();
                        AURoleOptions.ShapeshifterDuration = Wildling.ShapeshiftDur.GetFloat();
                    }
                    break;
                case CustomRoles.Jackal:
                    Jackal.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Sidekick:
                    Sidekick.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Librarian:
                    Librarian.ApplyGameOptions();
                    break;
                case CustomRoles.Vulture:
                    Vulture.ApplyGameOptions(opt);
                    AURoleOptions.EngineerCooldown = 0f;
                    AURoleOptions.EngineerInVentMaxTime = 0f;
                    break;
                case CustomRoles.Mafioso:
                    Mafioso.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Drainer:
                    Drainer.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Poisoner:
                    Poisoner.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Veteran:
                    if (Options.UsePets.GetBool()) break;
                    AURoleOptions.EngineerCooldown = Options.VeteranSkillCooldown.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = 1;
                    break;
                case CustomRoles.Grenadier:
                    if (Options.UsePets.GetBool()) break;
                    AURoleOptions.EngineerCooldown = Options.GrenadierSkillCooldown.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = 1;
                    break;
                /*       case CustomRoles.Flashbang:
                           AURoleOptions.ShapeshifterCooldown = Options.FlashbangSkillCooldown.GetFloat();
                           AURoleOptions.ShapeshifterDuration = Options.FlashbangSkillDuration.GetFloat();
                           break; */
                case CustomRoles.Lighter:
                    if (Options.UsePets.GetBool()) break;
                    AURoleOptions.EngineerInVentMaxTime = 1;
                    AURoleOptions.EngineerCooldown = Options.LighterSkillCooldown.GetFloat();
                    break;
                case CustomRoles.SecurityGuard:
                    if (Options.UsePets.GetBool()) break;
                    AURoleOptions.EngineerInVentMaxTime = 1;
                    AURoleOptions.EngineerCooldown = Options.SecurityGuardSkillCooldown.GetFloat();
                    break;
                case CustomRoles.Ventguard:
                    AURoleOptions.EngineerInVentMaxTime = 1;
                    AURoleOptions.EngineerCooldown = 15;
                    break;
                case CustomRoles.TimeMaster:
                    if (Options.UsePets.GetBool()) break;
                    AURoleOptions.EngineerCooldown = Options.TimeMasterSkillCooldown.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = 1;
                    break;
                case CustomRoles.FFF:
                case CustomRoles.Pursuer:
                    opt.SetVision(true);
                    break;
                case CustomRoles.NSerialKiller:
                    NSerialKiller.ApplyGameOptions(opt);
                    break;
                case CustomRoles.SoulHunter:
                    SoulHunter.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Enderman:
                    Enderman.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Mycologist:
                    Mycologist.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Bubble:
                    Bubble.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Hookshot:
                    Hookshot.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Sprayer:
                    Sprayer.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Penguin:
                    Penguin.ApplyGameOptions(opt);
                    break;
                case CustomRoles.PlagueDoctor:
                    PlagueDoctor.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Magician:
                    Magician.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Reckless:
                    Reckless.ApplyGameOptions(opt);
                    break;
                case CustomRoles.WeaponMaster:
                    WeaponMaster.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Postman:
                    Postman.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Pyromaniac:
                    Pyromaniac.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Eclipse:
                    Eclipse.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Vengeance:
                    Vengeance.ApplyGameOptions(opt);
                    break;
                case CustomRoles.HeadHunter:
                    HeadHunter.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Imitator:
                    Imitator.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Ignitor:
                    Ignitor.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Werewolf:
                    Werewolf.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Morphling:
                    Morphling.ApplyGameOptions();
                    break;
                case CustomRoles.Traitor:
                    Traitor.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Glitch:
                    Glitch.ApplyGameOptions(opt);
                    break;
                //case CustomRoles.NWitch:
                //    NWitch.ApplyGameOptions(opt);
                //    break;
                case CustomRoles.Maverick:
                    Maverick.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Medusa:
                    Medusa.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Jinx:
                    Jinx.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Ritualist:
                    Ritualist.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Pickpocket:
                    Pickpocket.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Juggernaut:
                    opt.SetVision(Juggernaut.HasImpostorVision.GetBool());
                    break;
                //case CustomRoles.Reverie:
                //    opt.SetVision(false);
                //    break;
                case CustomRoles.Capitalism:
                    AURoleOptions.KillCooldown = Options.CapitalismKillCooldown.GetFloat();
                    break;
                case CustomRoles.Jester:
                    AURoleOptions.EngineerCooldown = 0f;
                    AURoleOptions.EngineerInVentMaxTime = 0f;
                    opt.SetVision(Options.JesterHasImpostorVision.GetBool());
                    break;
                case CustomRoles.Infectious:
                    opt.SetVision(Infectious.HasImpostorVision.GetBool());
                    break;
                case CustomRoles.Lawyer:
                    //Main.NormalOptions.CrewLightMod = Lawyer.LawyerVision.GetFloat();
                    break;
                case CustomRoles.Wraith:
                case CustomRoles.HexMaster:
                case CustomRoles.Parasite:
                    opt.SetVision(true);
                    break;
                /*    case CustomRoles.Chameleon:
                        opt.SetVision(false);
                        break; */

                case CustomRoles.Gamer:
                    Gamer.ApplyGameOptions(opt);
                    break;
                case CustomRoles.DarkHide:
                    DarkHide.ApplyGameOptions(opt);
                    break;
                case CustomRoles.Workaholic:
                    AURoleOptions.EngineerCooldown = Options.WorkaholicVentCooldown.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = 0f;
                    break;
                case CustomRoles.ImperiusCurse:
                    AURoleOptions.ShapeshifterCooldown = Options.ImperiusCurseShapeshiftCooldown.GetFloat();
                    AURoleOptions.ShapeshifterLeaveSkin = false;
                    AURoleOptions.ShapeshifterDuration = Options.ShapeImperiusCurseShapeshiftDuration.GetFloat();
                    break;
                case CustomRoles.QuickShooter:
                    if (Options.UsePets.GetBool()) break;
                    AURoleOptions.ShapeshifterCooldown = QuickShooter.ShapeshiftCooldown.GetFloat();
                    AURoleOptions.ShapeshifterDuration = 1f;
                    break;
                case CustomRoles.Camouflager:
                    Camouflager.ApplyGameOptions();
                    break;
                case CustomRoles.Assassin:
                    if (Options.UsePets.GetBool()) break;
                    Assassin.ApplyGameOptions();
                    break;
                case CustomRoles.Undertaker:
                    if (Options.UsePets.GetBool()) break;
                    Undertaker.ApplyGameOptions();
                    break;
                case CustomRoles.Hacker:
                    Hacker.ApplyGameOptions();
                    break;
                case CustomRoles.Hangman:
                    Hangman.ApplyGameOptions();
                    break;
                case CustomRoles.Sunnyboy:
                    AURoleOptions.ScientistCooldown = 0f;
                    AURoleOptions.ScientistBatteryCharge = 60f;
                    break;
                case CustomRoles.BloodKnight:
                    BloodKnight.ApplyGameOptions(opt);
                    break;
                case CustomRoles.DovesOfNeace:
                    if (Options.UsePets.GetBool()) break;
                    AURoleOptions.EngineerCooldown = Options.DovesOfNeaceCooldown.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = 1f;
                    break;
                case CustomRoles.Disperser:
                    if (Options.UsePets.GetBool()) break;
                    Disperser.ApplyGameOptions();
                    break;
                case CustomRoles.Farseer:
                    opt.SetVision(false);
                    opt.SetFloat(FloatOptionNames.CrewLightMod, Farseer.Vision.GetFloat());
                    opt.SetFloat(FloatOptionNames.ImpostorLightMod, Farseer.Vision.GetFloat());
                    break;
                case CustomRoles.Dazzler:
                    Dazzler.ApplyGameOptions();
                    break;
                case CustomRoles.Devourer:
                    Devourer.ApplyGameOptions();
                    break;
                case CustomRoles.Addict:
                    AURoleOptions.EngineerCooldown = Addict.VentCooldown.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = 1f;
                    break;
                case CustomRoles.Alchemist:
                    if (Options.UsePets.GetBool()) break;
                    AURoleOptions.EngineerCooldown = Alchemist.VentCooldown.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = 1f;
                    break;
                case CustomRoles.Mario:
                    AURoleOptions.EngineerCooldown = Options.MarioVentCD.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = 1f;
                    break;
                case CustomRoles.Deathpact:
                    Deathpact.ApplyGameOptions();
                    break;
                case CustomRoles.Twister:
                    if (Options.UsePets.GetBool()) break;
                    Twister.ApplyGameOptions();
                    break;
                case CustomRoles.Sapper:
                    Sapper.ApplyGameOptions();
                    break;
                case CustomRoles.Druid:
                    AURoleOptions.EngineerCooldown = Druid.VentCooldown.GetInt();
                    AURoleOptions.EngineerInVentMaxTime = 1f;
                    break;
                case CustomRoles.Mole:
                    AURoleOptions.EngineerInVentMaxTime = 1f;
                    AURoleOptions.EngineerCooldown = 5f;
                    break;
                case CustomRoles.Sentinel:
                    AURoleOptions.EngineerCooldown = Sentinel.PatrolCooldown.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = 1f;
                    break;
                case CustomRoles.Kidnapper:
                    AURoleOptions.ShapeshifterCooldown = Kidnapper.SSCD.GetFloat();
                    AURoleOptions.ShapeshifterDuration = 1f;
                    break;
                case CustomRoles.Spiritcaller:
                    opt.SetVision(Spiritcaller.ImpostorVision.GetBool());
                    break;
            }

            if (Main.AllPlayerControls.Any(x => x.Is(CustomRoles.Bewilder) && !x.IsAlive() && x.GetRealKiller()?.PlayerId == player.PlayerId && !x.Is(CustomRoles.Hangman)))
            {
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, Options.BewilderVision.GetFloat());
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, Options.BewilderVision.GetFloat());
            }
            if (Main.AllPlayerControls.Any(x => x.Is(CustomRoles.Ghoul) && !x.IsAlive() && x.GetRealKiller()?.PlayerId == player.PlayerId))
            {
                Main.KillGhoul.Add(player.PlayerId);
            }
            if (
                (Main.GrenadierBlinding.Count > 0 &&
                (player.GetCustomRole().IsImpostor() ||
                (player.GetCustomRole().IsNeutral() && Options.GrenadierCanAffectNeutral.GetBool()))
                ) || (
                Main.MadGrenadierBlinding.Count > 0 && !player.GetCustomRole().IsImpostorTeam() && !player.Is(CustomRoles.Madmate))
                )
            {
                {
                    opt.SetVision(false);
                    opt.SetFloat(FloatOptionNames.CrewLightMod, Options.GrenadierCauseVision.GetFloat());
                    opt.SetFloat(FloatOptionNames.ImpostorLightMod, Options.GrenadierCauseVision.GetFloat());
                }
            }

            switch (player.GetCustomRole())
            {
                case CustomRoles.Lighter when Main.Lighter.Count > 0:
                    opt.SetVisionV2();
                    if (Utils.IsActive(SystemTypes.Electrical)) opt.SetFloat(FloatOptionNames.CrewLightMod, Options.LighterVisionOnLightsOut.GetFloat() * 5);
                    else opt.SetFloat(FloatOptionNames.CrewLightMod, Options.LighterVisionNormal.GetFloat());
                    break;
                case CustomRoles.Alchemist when Alchemist.VisionPotionActive:
                    opt.SetVisionV2();
                    if (Utils.IsActive(SystemTypes.Electrical)) opt.SetFloat(FloatOptionNames.CrewLightMod, Alchemist.VisionOnLightsOut.GetFloat() * 5);
                    else opt.SetFloat(FloatOptionNames.CrewLightMod, Alchemist.Vision.GetFloat());
                    break;
            }

            if (Sprayer.LowerVisionList.Contains(player.PlayerId))
            {
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, Sprayer.LoweredVision.GetFloat());
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, Sprayer.LoweredVision.GetFloat());
            }

            if (Sentinel.IsPatrolling(player.PlayerId))
            {
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, Sentinel.LoweredVision.GetFloat());
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, Sentinel.LoweredVision.GetFloat());
            }

            /*     if ((Main.FlashbangInProtect.Count > 0 && Main.ForFlashbang.Contains(player.PlayerId) && (!player.GetCustomRole().IsCrewmate())))  
                 {
                         opt.SetVision(false);
                         opt.SetFloat(FloatOptionNames.CrewLightMod, Options.FlashbangVision.GetFloat());
                         opt.SetFloat(FloatOptionNames.ImpostorLightMod, Options.FlashbangVision.GetFloat());
                 } */

            Dazzler.SetDazzled(player, opt);
            Deathpact.SetDeathpactVision(player, opt);

            Spiritcaller.ReduceVision(opt, player);

            var array = Main.PlayerStates[player.PlayerId].SubRoles.ToArray();
            foreach (CustomRoles subRole in array)
            {
                switch (subRole)
                {
                    case CustomRoles.Watcher:
                        opt.SetBool(BoolOptionNames.AnonymousVotes, false);
                        break;
                    case CustomRoles.Flashman:
                        Main.AllPlayerSpeed[player.PlayerId] = Options.FlashmanSpeed.GetFloat();
                        break;
                    case CustomRoles.Giant:
                        Main.AllPlayerSpeed[player.PlayerId] = Options.GiantSpeed.GetFloat();
                        break;
                    case CustomRoles.Mare when Options.MareHasIncreasedSpeed.GetBool():
                        Main.AllPlayerSpeed[player.PlayerId] = Options.MareSpeedDuringLightsOut.GetFloat();
                        break;
                    case CustomRoles.Torch:
                        if (!Utils.IsActive(SystemTypes.Electrical))
                            opt.SetVision(true);
                        opt.SetFloat(FloatOptionNames.CrewLightMod, Options.TorchVision.GetFloat());
                        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Options.TorchVision.GetFloat());
                        if (Utils.IsActive(SystemTypes.Electrical) && !Options.TorchAffectedByLights.GetBool())
                            opt.SetVision(true);
                        opt.SetFloat(FloatOptionNames.CrewLightMod, Options.TorchVision.GetFloat() * 5);
                        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Options.TorchVision.GetFloat() * 5);
                        break;
                    case CustomRoles.Bewilder:
                        opt.SetVision(false);
                        opt.SetFloat(FloatOptionNames.CrewLightMod, Options.BewilderVision.GetFloat());
                        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Options.BewilderVision.GetFloat());
                        break;
                    case CustomRoles.Sunglasses:
                        opt.SetVision(false);
                        opt.SetFloat(FloatOptionNames.CrewLightMod, Options.SunglassesVision.GetFloat());
                        opt.SetFloat(FloatOptionNames.ImpostorLightMod, Options.SunglassesVision.GetFloat());
                        break;
                    case CustomRoles.Reach:
                        opt.SetInt(Int32OptionNames.KillDistance, 2);
                        break;
                    case CustomRoles.Madmate:
                        opt.SetVision(Options.MadmateHasImpostorVision.GetBool());
                        break;
                    case CustomRoles.Nimble when role.GetRoleTypes() == RoleTypes.Engineer:
                        AURoleOptions.EngineerCooldown = Options.NimbleCD.GetFloat();
                        AURoleOptions.EngineerInVentMaxTime = Options.NimbleInVentTime.GetFloat();
                        break;
                    case CustomRoles.Physicist when role.GetRoleTypes() == RoleTypes.Scientist:
                        AURoleOptions.ScientistCooldown = Options.PhysicistCD.GetFloat();
                        AURoleOptions.ScientistBatteryCharge = Options.PhysicistViewDuration.GetFloat();
                        break;
                }
            }

            if (Magician.BlindPPL.ContainsKey(player.PlayerId))
            {
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, 0.01f);
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0.01f);
            }

            // ������������ȴΪ0ʱ�޷�������ʾͼ��
            AURoleOptions.EngineerCooldown = Mathf.Max(0.01f, AURoleOptions.EngineerCooldown);

            if (Main.AllPlayerKillCooldown.TryGetValue(player.PlayerId, out var killCooldown))
            {
                AURoleOptions.KillCooldown = Mathf.Max(0.01f, killCooldown);
            }

            if (Main.AllPlayerSpeed.TryGetValue(player.PlayerId, out var speed))
            {
                AURoleOptions.PlayerSpeedMod = Mathf.Clamp(speed, Main.MinSpeed, 3f);
            }

            state.taskState.hasTasks = Utils.HasTasks(player.Data, false);
            if (Options.GhostCanSeeOtherVotes.GetBool() && player.Data.IsDead)
                opt.SetBool(BoolOptionNames.AnonymousVotes, false);
            if (Options.AdditionalEmergencyCooldown.GetBool() &&
                Options.AdditionalEmergencyCooldownThreshold.GetInt() <= Utils.AllAlivePlayersCount)
            {
                opt.SetInt(
                    Int32OptionNames.EmergencyCooldown,
                    Options.AdditionalEmergencyCooldownTime.GetInt());
            }
            if (Options.SyncButtonMode.GetBool() && Options.SyncedButtonCount.GetValue() <= Options.UsedButtonCount)
            {
                opt.SetInt(Int32OptionNames.EmergencyCooldown, 3600);
            }
            MeetingTimeManager.ApplyGameOptions(opt);

            AURoleOptions.ShapeshifterCooldown = Mathf.Max(1f, AURoleOptions.ShapeshifterCooldown);
            AURoleOptions.ProtectionDurationSeconds = Main.UseVersionProtocol.Value ? 0f : 60f;

            return opt;
        }
        catch (Exception e)
        {
            Logger.Fatal($"Error for {player.GetRealName()} ({player.GetCustomRole()}): {e}", "PlayerGameOptionsSender.BuildGameOptions");
            Logger.SendInGame($"Error syncing settings for {player.GetRealName()} - Please report this bug to the developer AND SEND LOGS");
            return BasedGameOptions;
        }
    }

    public override bool AmValid()
    {
        return base.AmValid() && player != null && !player.Data.Disconnected && Main.RealOptionsData != null;
    }
}