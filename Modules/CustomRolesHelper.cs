using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Roles;
using EHR.Modules;
using UnityEngine;
using EHR.Gamemodes;

namespace EHR;

internal static class CustomRolesHelper
{
    public static bool CanCheck = false;

    private static readonly List<CustomRoles> OnlySpawnsWithPetsRoleList =
    [
        CustomRoles.Tunneler,
        CustomRoles.Tornado,
        CustomRoles.Swiftclaw,
        CustomRoles.Adventurer,
        CustomRoles.Sentry,
        CustomRoles.Cherokious,
        CustomRoles.Chemist,
        CustomRoles.Evolver,
        CustomRoles.ToiletMaster,
        CustomRoles.Telekinetic,
        CustomRoles.Dad,
        CustomRoles.Whisperer,
        CustomRoles.Wizard,
        CustomRoles.NoteKiller,
        CustomRoles.Weatherman,
        CustomRoles.Amogus,
        CustomRoles.Wiper,
        CustomRoles.PortalMaker,
        CustomRoles.Gardener,
        CustomRoles.Farmer,
        CustomRoles.Tree,
        CustomRoles.Doorjammer,
        CustomRoles.Carrier,
        CustomRoles.Empress,

        // Add-ons
        CustomRoles.Energetic,

        // HnS
        CustomRoles.Jet,
        CustomRoles.Dasher
    ];

    public static bool IsExperimental(this CustomRoles role)
    {
        return role is
            CustomRoles.DoubleAgent or
            CustomRoles.Weatherman;
    }

    public static bool IsForOtherGameMode(this CustomRoles role)
    {
        return CustomHnS.AllHnSRoles.Contains(role) || role is
            CustomRoles.Challenger or
            CustomRoles.Killer or
            CustomRoles.Tasker or
            CustomRoles.Potato or
            CustomRoles.Runner or
            CustomRoles.CTFPlayer or
            CustomRoles.NDPlayer or
            CustomRoles.RRPlayer or
            CustomRoles.KOTZPlayer or
            CustomRoles.QuizPlayer or
            CustomRoles.TMGPlayer or
            CustomRoles.BedWarsPlayer or
            CustomRoles.Racer or
            CustomRoles.MinglePlayer or
            CustomRoles.SnowdownPlayer;
    }

    public static RoleBase GetRoleClass(this CustomRoles role)
    {
        RoleBase roleClass = role switch
        {
            // Roles that use the same code as another role need to be handled here
            CustomRoles.Nuker => new Bomber(),
            CustomRoles.Undertaker => new Ninja(),
            CustomRoles.Chameleon => new Swooper(),
            CustomRoles.BloodKnight => new Wildling(),
            CustomRoles.HexMaster => new Witch(),
            CustomRoles.Pulse => new Greedy(),
            CustomRoles.Jinx => new CursedWolf(),
            CustomRoles.Juggernaut => new Arrogance(),
            CustomRoles.Medusa => new Cleaner(),
            CustomRoles.Poisoner => new Vampire(),
            CustomRoles.Reckless => new Arrogance(),
            CustomRoles.Ritualist => new Consigliere(),
            CustomRoles.Wraith => new Swooper(),
            CustomRoles.Goose => new Penguin(),
            CustomRoles.Telecommunication => new AntiAdminer(),

            // Else, the role class is the role name - if the class doesn't exist, it defaults to VanillaRole
            _ => Main.AllRoleClasses.FirstOrDefault(x => x.GetType().Name.Equals(role.ToString(), StringComparison.OrdinalIgnoreCase)) ?? new VanillaRole()
        };

        return Activator.CreateInstance(roleClass.GetType()) as RoleBase;
    }

    public static CustomRoles GetVNRole(this CustomRoles role, bool checkDesyncRole = false)
    {
        if (role.IsGhostRole()) return CustomRoles.GuardianAngel;
        if (role.IsVanilla()) return role;
        if (role is CustomRoles.GM) return CustomRoles.Crewmate;
        if (checkDesyncRole && role.IsDesyncRole()) return Enum.Parse<CustomRoles>(role.GetDYRole() + "EHR");
        if ((Options.UsePhantomBasis.GetBool() || role.AlwaysUsesPhantomBase()) && role.SimpleAbilityTrigger()) return CustomRoles.Phantom;

        bool UsePets = Options.UsePets.GetBool();

        return role switch
        {
            CustomRoles.Sniper => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Jester => Jester.JesterCanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
            CustomRoles.Mayor => Mayor.MayorHasPortableButton.GetBool() && !UsePets ? CustomRoles.Engineer : CustomRoles.Crewmate,
            CustomRoles.Telecommunication => Telecommunication.CanVent.GetBool() || !UsePets ? CustomRoles.Engineer : CustomRoles.Crewmate,
            CustomRoles.Vulture => Vulture.CanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
            CustomRoles.Opportunist => Opportunist.CanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
            CustomRoles.Vindicator => CustomRoles.Impostor,
            CustomRoles.Snitch => CustomRoles.Crewmate,
            CustomRoles.Inspector => CustomRoles.Crewmate,
            CustomRoles.Marshall => CustomRoles.Crewmate,
            CustomRoles.Mechanic => Mechanic.CanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
            CustomRoles.Nemesis => Options.LegacyNemesis.GetBool() ? CustomRoles.Shapeshifter : CustomRoles.Impostor,
            CustomRoles.Terrorist => Terrorist.CanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
            CustomRoles.Executioner => CustomRoles.Crewmate,
            CustomRoles.Lawyer => CustomRoles.Crewmate,
            CustomRoles.Swapper => CustomRoles.Crewmate,
            CustomRoles.Ignitor => CustomRoles.Crewmate,
            CustomRoles.Jailor => UsePets && Jailor.UsePet.GetBool() ? CustomRoles.Crewmate : CustomRoles.Impostor,
            CustomRoles.Vampire => CustomRoles.Impostor,
            CustomRoles.BountyHunter => CustomRoles.Impostor,
            CustomRoles.Trickster => CustomRoles.Impostor,
            CustomRoles.Witch => (Witch.SwitchTrigger)Witch.ModeSwitchAction.GetValue() == Witch.SwitchTrigger.Vanish ? CustomRoles.Phantom : CustomRoles.Impostor,
            CustomRoles.Agitator => CustomRoles.Impostor,
            CustomRoles.Consigliere => CustomRoles.Impostor,
            CustomRoles.Wildling => Wildling.CanShapeshiftOpt.GetBool() ? CustomRoles.Shapeshifter : CustomRoles.Impostor,
            CustomRoles.Morphling => CustomRoles.Shapeshifter,
            CustomRoles.Warlock => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Mercenary => CustomRoles.Impostor,
            CustomRoles.Fireworker => CustomRoles.Shapeshifter,
            CustomRoles.SpeedBooster => CustomRoles.Crewmate,
            CustomRoles.Dictator => CustomRoles.Crewmate,
            CustomRoles.DoubleAgent => CustomRoles.Crewmate,
            CustomRoles.Inhibitor => CustomRoles.Impostor,
            CustomRoles.Occultist => CustomRoles.Impostor,
            CustomRoles.Wiper => CustomRoles.Impostor,
            CustomRoles.Forger => CustomRoles.Impostor,
            CustomRoles.ClockBlocker => CustomRoles.Impostor,
            CustomRoles.Psychopath => CustomRoles.Impostor,
            CustomRoles.Loner => CustomRoles.Impostor,
            CustomRoles.Postponer => CustomRoles.Impostor,
            CustomRoles.Catalyst => CustomRoles.Impostor,
            CustomRoles.Exclusionary => CustomRoles.Impostor,
            CustomRoles.Centralizer => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Venerer => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Kidnapper => CustomRoles.Shapeshifter,
            CustomRoles.Ambusher => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Stasis => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Fabricator => CustomRoles.Impostor,
            CustomRoles.Wasp => CustomRoles.Impostor,
            CustomRoles.Assumer => CustomRoles.Impostor,
            CustomRoles.Augmenter => CustomRoles.Shapeshifter,
            CustomRoles.Ventriloquist => CustomRoles.Impostor,
            CustomRoles.Echo => CustomRoles.Shapeshifter,
            CustomRoles.Abyssbringer => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Overheat => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Hypnotist => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Generator => CustomRoles.Shapeshifter,
            CustomRoles.Blackmailer => CustomRoles.Impostor,
            CustomRoles.Commander => CustomRoles.Shapeshifter,
            CustomRoles.Freezer => CustomRoles.Shapeshifter,
            CustomRoles.Changeling => CustomRoles.Phantom,
            CustomRoles.Swapster => CustomRoles.Shapeshifter,
            CustomRoles.Kamikaze => CustomRoles.Impostor,
            CustomRoles.Librarian => CustomRoles.Shapeshifter,
            CustomRoles.Cantankerous => CustomRoles.Impostor,
            CustomRoles.Swiftclaw => CustomRoles.Impostor,
            CustomRoles.YinYanger => CustomRoles.Impostor,
            CustomRoles.Duellist => CustomRoles.Shapeshifter,
            CustomRoles.Consort => CustomRoles.Impostor,
            CustomRoles.Framer => CustomRoles.Impostor,
            CustomRoles.Mafioso => CustomRoles.Impostor,
            CustomRoles.Chronomancer => CustomRoles.Impostor,
            CustomRoles.Nullifier => CustomRoles.Impostor,
            CustomRoles.Stealth => UsePets || Stealth.UseLegacyVersion.GetBool() ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Penguin => CustomRoles.Impostor,
            CustomRoles.Sapper => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Mastermind => CustomRoles.Impostor,
            CustomRoles.RiftMaker => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Gambler => CustomRoles.Impostor,
            CustomRoles.Hitman => CustomRoles.Shapeshifter,
            CustomRoles.Saboteur => CustomRoles.Impostor,
            CustomRoles.Doctor => CustomRoles.Scientist,
            CustomRoles.Tracefinder => CustomRoles.Scientist,
            CustomRoles.Puppeteer => CustomRoles.Impostor,
            CustomRoles.TimeThief => CustomRoles.Impostor,
            CustomRoles.EvilTracker => CustomRoles.Shapeshifter,
            CustomRoles.Paranoid => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.TimeMaster => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Cleanser => CustomRoles.Crewmate,
            CustomRoles.Miner => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Psychic => CustomRoles.Crewmate,
            CustomRoles.LazyGuy => CustomRoles.Crewmate,
            CustomRoles.Twister => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.SuperStar => CustomRoles.Crewmate,
            CustomRoles.Anonymous => CustomRoles.Shapeshifter,
            CustomRoles.Visionary => CustomRoles.Shapeshifter,
            CustomRoles.Ninja => CustomRoles.Phantom,
            CustomRoles.Undertaker => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Luckey => CustomRoles.Crewmate,
            CustomRoles.Demolitionist => CustomRoles.Crewmate,
            CustomRoles.Ventguard => CustomRoles.Engineer,
            CustomRoles.Express => CustomRoles.Crewmate,
            CustomRoles.NiceEraser => CustomRoles.Crewmate,
            CustomRoles.TaskManager => CustomRoles.Crewmate,
            CustomRoles.Adventurer => CustomRoles.Engineer,
            CustomRoles.Randomizer => CustomRoles.Crewmate,
            CustomRoles.Beacon => CustomRoles.Crewmate,
            CustomRoles.Rabbit => CustomRoles.Crewmate,
            CustomRoles.Shiftguard => CustomRoles.Crewmate,
            CustomRoles.Mole => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Markseeker => CustomRoles.Crewmate,
            CustomRoles.Sentinel => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Electric => CustomRoles.Crewmate,
            CustomRoles.Tornado => CustomRoles.Crewmate,
            CustomRoles.Druid => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Catcher => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Insight => CustomRoles.Crewmate,
            CustomRoles.Tunneler => CustomRoles.Crewmate,
            CustomRoles.Detour => CustomRoles.Crewmate,
            CustomRoles.Dad => CustomRoles.Engineer,
            CustomRoles.Drainer => CustomRoles.Engineer,
            CustomRoles.Benefactor => CustomRoles.Crewmate,
            CustomRoles.MeetingManager => CustomRoles.Crewmate,
            CustomRoles.Bane => CustomRoles.Crewmate,
            CustomRoles.Farmer => CustomRoles.Crewmate,
            CustomRoles.Captain => CustomRoles.Crewmate,
            CustomRoles.Transmitter => CustomRoles.Crewmate,
            CustomRoles.Sensor => CustomRoles.Crewmate,
            CustomRoles.Doorjammer => CustomRoles.Crewmate,
            CustomRoles.Tree => CustomRoles.Crewmate,
            CustomRoles.Inquisitor => CustomRoles.Crewmate,
            CustomRoles.Gardener => CustomRoles.Crewmate,
            CustomRoles.Imitator => CustomRoles.Crewmate,
            CustomRoles.PortalMaker => CustomRoles.Crewmate,
            CustomRoles.Vacuum => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Astral => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Helper => CustomRoles.Crewmate,
            CustomRoles.Ankylosaurus => CustomRoles.Crewmate,
            CustomRoles.Leery => CustomRoles.Crewmate,
            CustomRoles.Altruist => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Negotiator => CustomRoles.Crewmate,
            CustomRoles.Grappler => CustomRoles.Crewmate,
            CustomRoles.Journalist => CustomRoles.Crewmate,
            CustomRoles.Whisperer => CustomRoles.Crewmate,
            CustomRoles.Car => CustomRoles.Crewmate,
            CustomRoles.President => CustomRoles.Crewmate,
            CustomRoles.Oxyman => CustomRoles.Engineer,
            CustomRoles.Chef => CustomRoles.Crewmate,
            CustomRoles.Decryptor => CustomRoles.Crewmate,
            CustomRoles.Adrenaline => CustomRoles.Crewmate,
            CustomRoles.Safeguard => CustomRoles.Crewmate,
            CustomRoles.Clairvoyant => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Rhapsode => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Telekinetic => CustomRoles.Engineer,
            CustomRoles.Autocrat => CustomRoles.Crewmate,
            CustomRoles.Inquirer => CustomRoles.Crewmate,
            CustomRoles.Soothsayer => CustomRoles.Crewmate,
            CustomRoles.LovingCrewmate => CustomRoles.Crewmate,
            CustomRoles.LovingImpostor => CustomRoles.Impostor,
            CustomRoles.ToiletMaster => CustomRoles.Crewmate,
            CustomRoles.Sentry => CustomRoles.Crewmate,
            CustomRoles.Perceiver => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Convener => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Mathematician => CustomRoles.Crewmate,
            CustomRoles.Nightmare => CustomRoles.Crewmate,
            CustomRoles.Battery => CustomRoles.Crewmate,
            CustomRoles.CameraMan => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Spy => CustomRoles.Crewmate,
            CustomRoles.Ricochet => CustomRoles.Crewmate,
            CustomRoles.Tether => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Aid => UsePets && Aid.UsePet.GetBool() ? CustomRoles.Crewmate : CustomRoles.Impostor,
            CustomRoles.Socialite => UsePets && Socialite.UsePet.GetBool() ? CustomRoles.Crewmate : CustomRoles.Impostor,
            CustomRoles.Escort => UsePets && Escort.UsePet.GetBool() ? CustomRoles.Crewmate : CustomRoles.Impostor,
            CustomRoles.DonutDelivery => UsePets && DonutDelivery.UsePet.GetBool() ? CustomRoles.Crewmate : CustomRoles.Impostor,
            CustomRoles.Gaulois => UsePets && Gaulois.UsePet.GetBool() ? CustomRoles.Crewmate : CustomRoles.Impostor,
            CustomRoles.Analyst => UsePets && Analyst.UsePet.GetBool() ? CustomRoles.Crewmate : CustomRoles.Impostor,
            CustomRoles.Escapist => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.NiceGuesser => CustomRoles.Crewmate,
            CustomRoles.EvilGuesser => CustomRoles.Impostor,
            CustomRoles.Forensic => CustomRoles.Crewmate,
            CustomRoles.God => CustomRoles.Crewmate,
            CustomRoles.Tank => CustomRoles.Engineer,
            CustomRoles.Technician => Technician.CanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
            CustomRoles.GuardianAngelEHR => CustomRoles.GuardianAngel,
            CustomRoles.Zombie => CustomRoles.Impostor,
            CustomRoles.Vector => CustomRoles.Engineer,
            CustomRoles.AntiAdminer => CustomRoles.Shapeshifter,
            CustomRoles.Arrogance => CustomRoles.Impostor,
            CustomRoles.Bomber => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Nuker => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Trapster => Trapster.LegacyTrapster.GetBool() || UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Scavenger => CustomRoles.Impostor,
            CustomRoles.Transporter => CustomRoles.Crewmate,
            CustomRoles.Veteran => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Capitalist => CustomRoles.Impostor,
            CustomRoles.Bodyguard => CustomRoles.Crewmate,
            CustomRoles.Grenadier => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Lighter => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.SecurityGuard => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Magistrate => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Gangster => CustomRoles.Impostor,
            CustomRoles.Cleaner => CustomRoles.Impostor,
            CustomRoles.FortuneTeller => CustomRoles.Crewmate,
            CustomRoles.Oracle => CustomRoles.Crewmate,
            CustomRoles.Lightning => CustomRoles.Impostor,
            CustomRoles.Greedy => CustomRoles.Impostor,
            CustomRoles.Workaholic => CustomRoles.Engineer,
            CustomRoles.Amnesiac => Amnesiac.CanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
            CustomRoles.Speedrunner => CustomRoles.Crewmate,
            CustomRoles.CursedWolf => CustomRoles.Impostor,
            CustomRoles.Collector => CustomRoles.Crewmate,
            CustomRoles.NecroGuesser => CustomRoles.Crewmate,
            CustomRoles.SchrodingersCat => CustomRoles.Crewmate,
            CustomRoles.SoulCatcher => CustomRoles.Shapeshifter,
            CustomRoles.QuickShooter => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.EvilEraser => CustomRoles.Impostor,
            CustomRoles.Butcher => CustomRoles.Impostor,
            CustomRoles.Hangman => CustomRoles.Shapeshifter,
            CustomRoles.Sunnyboy => CustomRoles.Scientist,
            CustomRoles.Specter => Options.PhantomCanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
            CustomRoles.Judge => CustomRoles.Crewmate,
            CustomRoles.Councillor => CustomRoles.Impostor,
            CustomRoles.Mortician => CustomRoles.Crewmate,
            CustomRoles.Medium => CustomRoles.Crewmate,
            CustomRoles.Bard => CustomRoles.Impostor,
            CustomRoles.Swooper => CustomRoles.Impostor,
            CustomRoles.Crewpostor => CustomRoles.Engineer,
            CustomRoles.Hypocrite => CustomRoles.Crewmate,
            CustomRoles.Cherokious => CustomRoles.Engineer,
            CustomRoles.Pawn => CustomRoles.Crewmate,
            CustomRoles.Observer => CustomRoles.Crewmate,
            CustomRoles.Pacifist => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Disperser => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
            CustomRoles.Camouflager => CustomRoles.Shapeshifter,
            CustomRoles.Dazzler => CustomRoles.Shapeshifter,
            CustomRoles.Devourer => CustomRoles.Shapeshifter,
            CustomRoles.Deathpact => CustomRoles.Shapeshifter,
            CustomRoles.Coroner => CustomRoles.Crewmate,
            CustomRoles.Scout => CustomRoles.Crewmate,
            CustomRoles.Merchant => CustomRoles.Crewmate,
            CustomRoles.Lookout => CustomRoles.Crewmate,
            CustomRoles.Guardian => CustomRoles.Crewmate,
            CustomRoles.Enigma => CustomRoles.Crewmate,
            CustomRoles.Addict => CustomRoles.Engineer,
            CustomRoles.Alchemist => CustomRoles.Engineer, // Needs to vent to use the invisibility potion
            CustomRoles.Chameleon => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
            CustomRoles.Lurker => CustomRoles.Impostor,
            CustomRoles.Doomsayer => CustomRoles.Crewmate,
            CustomRoles.Godfather => CustomRoles.Impostor,
            CustomRoles.Silencer => Silencer.SilenceMode.GetValue() switch
            {
                0 => CustomRoles.Impostor,
                1 => CustomRoles.Shapeshifter,
                2 => CustomRoles.Phantom,
                _ => CustomRoles.Impostor
            },
            CustomRoles.NoteKiller => CustomRoles.Crewmate,
            CustomRoles.RoomRusher => RoomRusher.CanVent ? CustomRoles.Engineer : CustomRoles.Crewmate,
            CustomRoles.Clerk => CustomRoles.Crewmate,
            CustomRoles.CovenMember => CustomRoles.Crewmate,
            CustomRoles.Augur => Augur.CanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
            CustomRoles.Accumulator => Accumulator.CanVentBeforeKilling.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,

            // Vanilla roles (just in case)
            CustomRoles.ImpostorEHR => CustomRoles.Impostor,
            CustomRoles.PhantomEHR => CustomRoles.Phantom,
            CustomRoles.ShapeshifterEHR => CustomRoles.Shapeshifter,
            CustomRoles.CrewmateEHR => CustomRoles.Crewmate,
            CustomRoles.EngineerEHR => CustomRoles.Engineer,
            CustomRoles.ScientistEHR => CustomRoles.Scientist,
            CustomRoles.TrackerEHR => CustomRoles.Tracker,
            CustomRoles.NoisemakerEHR => CustomRoles.Noisemaker,
            CustomRoles.DetectiveEHR => CustomRoles.Detective,
            CustomRoles.ViperEHR => CustomRoles.Viper,

            // Hide And Seek
            CustomRoles.Hider => CustomRoles.Crewmate,
            CustomRoles.Seeker => CustomRoles.Impostor,
            CustomRoles.Fox => CustomRoles.Crewmate,
            CustomRoles.Troll => CustomRoles.Crewmate,
            CustomRoles.Jumper => CustomRoles.Engineer,
            CustomRoles.Detector => CustomRoles.Crewmate,
            CustomRoles.Jet => CustomRoles.Crewmate,
            CustomRoles.Dasher => CustomRoles.Impostor,
            CustomRoles.Locator => CustomRoles.Impostor,
            CustomRoles.Venter => CustomRoles.Impostor,
            CustomRoles.Agent => CustomRoles.Impostor,
            CustomRoles.Taskinator => CustomRoles.Crewmate,

            // Stop And Go
            CustomRoles.Tasker => CustomRoles.Crewmate,
            // Hot Potato
            CustomRoles.Potato => CustomRoles.Crewmate,
            // Speedrun
            CustomRoles.Runner => CustomRoles.Crewmate,
            // Natural Disasters
            CustomRoles.NDPlayer => CustomRoles.Crewmate,
            // Room Rush
            CustomRoles.RRPlayer => CustomRoles.Crewmate,
            // Quiz
            CustomRoles.QuizPlayer => CustomRoles.Crewmate,
            // The Mind Game
            CustomRoles.TMGPlayer => CustomRoles.Crewmate,
            // Mingle
            CustomRoles.MinglePlayer => CustomRoles.Crewmate,

            _ => role.IsImpostor() ? CustomRoles.Impostor : CustomRoles.Crewmate
        };
    }

    public static CustomRoles GetErasedRole(this CustomRoles role)
    {
        if (role.IsVanilla()) return role;

        CustomRoles vnRole = role.GetVNRole(true);

        if (vnRole.ToString().EndsWith("EHR")) return vnRole;

        return vnRole switch
        {
            CustomRoles.Crewmate => CustomRoles.CrewmateEHR,
            CustomRoles.Engineer => CustomRoles.EngineerEHR,
            CustomRoles.Noisemaker => CustomRoles.NoisemakerEHR,
            CustomRoles.Tracker => CustomRoles.TrackerEHR,
            CustomRoles.Scientist => CustomRoles.ScientistEHR,
            CustomRoles.Impostor when role.IsCrewmate() => CustomRoles.CrewmateEHR,
            CustomRoles.Impostor => CustomRoles.ImpostorEHR,
            CustomRoles.Phantom => CustomRoles.PhantomEHR,
            CustomRoles.Shapeshifter => CustomRoles.ShapeshifterEHR,
            CustomRoles.Detective => CustomRoles.DetectiveEHR,
            CustomRoles.Viper => CustomRoles.ViperEHR,
            _ => role.IsImpostor() ? CustomRoles.ImpostorEHR : CustomRoles.CrewmateEHR
        };
    }

    public static RoleTypes GetDYRole(this CustomRoles role, bool load = false)
    {
        if (!load && ((Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool()) || role.AlwaysUsesPhantomBase()) && !role.IsImpostor() && role.SimpleAbilityTrigger()) return RoleTypes.Phantom;

        bool UsePets = !load && Options.UsePets.GetBool();

        return role switch
        {
            // Solo PVP
            CustomRoles.Challenger => RoleTypes.Impostor,
            // FFA
            CustomRoles.Killer => RoleTypes.Impostor,
            // Capture The Flag
            CustomRoles.CTFPlayer => RoleTypes.Phantom,
            // King of the Zones
            CustomRoles.KOTZPlayer => RoleTypes.Impostor,
            // Bed Wars
            CustomRoles.BedWarsPlayer => RoleTypes.Phantom,
            // Deathrace
            CustomRoles.Racer => RoleTypes.Phantom,
            // Snowdown
            CustomRoles.SnowdownPlayer => RoleTypes.Phantom,
            // Standard
            CustomRoles.Sheriff => UsePets && Sheriff.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Crusader => UsePets && Crusader.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.CopyCat => UsePets && CopyCat.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Renegade => RoleTypes.Impostor,
            CustomRoles.Agitator => RoleTypes.Impostor,
            CustomRoles.Monarch => UsePets && Monarch.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Deputy => UsePets && Deputy.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Bestower => UsePets && Bestower.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Retributionist => UsePets && Retributionist.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Arsonist => RoleTypes.Impostor,
            CustomRoles.Jackal => RoleTypes.Impostor,
            CustomRoles.Medusa => RoleTypes.Impostor,
            CustomRoles.Sidekick => RoleTypes.Impostor,
            CustomRoles.Vigilante => UsePets && Vigilante.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Innocent => RoleTypes.Impostor,
            CustomRoles.Amnesiac when Amnesiac.RememberMode.GetValue() == 1 => RoleTypes.Impostor,
            CustomRoles.Pelican => RoleTypes.Impostor,
            CustomRoles.Aid => UsePets && Aid.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Socialite => UsePets && Socialite.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Escort => UsePets && Escort.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.DonutDelivery => UsePets && DonutDelivery.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Gaulois => UsePets && Gaulois.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Analyst => UsePets && Analyst.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Witness => UsePets && Options.WitnessUsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Goose => RoleTypes.Impostor,
            CustomRoles.Pursuer => RoleTypes.Impostor,
            CustomRoles.Revolutionist => RoleTypes.Impostor,
            CustomRoles.Hater => RoleTypes.Impostor,
            CustomRoles.Medic => UsePets && Medic.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Demon => RoleTypes.Impostor,
            CustomRoles.HexMaster => (Witch.SwitchTrigger)HexMaster.ModeSwitchAction.GetValue() == Witch.SwitchTrigger.Vanish ? RoleTypes.Phantom : RoleTypes.Impostor,
            CustomRoles.Wraith => RoleTypes.Impostor,
            CustomRoles.Glitch => RoleTypes.Shapeshifter,
            CustomRoles.Jailor => UsePets && Jailor.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Juggernaut => RoleTypes.Impostor,
            CustomRoles.Jinx => RoleTypes.Impostor,
            CustomRoles.Stalker => RoleTypes.Impostor,
            CustomRoles.Provocateur => RoleTypes.Impostor,
            CustomRoles.BloodKnight => RoleTypes.Impostor,
            CustomRoles.Poisoner => RoleTypes.Impostor,
            CustomRoles.Duality => RoleTypes.Impostor,
            CustomRoles.Berserker => RoleTypes.Impostor,
            CustomRoles.SerialKiller => RoleTypes.Impostor,
            CustomRoles.Quarry => RoleTypes.Shapeshifter,
            CustomRoles.SoulCollector => RoleTypes.Impostor,
            CustomRoles.Spider => UsePets ? RoleTypes.Impostor : RoleTypes.Shapeshifter,
            CustomRoles.Explosivist => UsePets ? RoleTypes.Impostor : RoleTypes.Shapeshifter,
            CustomRoles.Sharpshooter => UsePets ? RoleTypes.Impostor : RoleTypes.Shapeshifter,
            CustomRoles.Thanos => UsePets ? RoleTypes.Impostor : RoleTypes.Shapeshifter,
            CustomRoles.Slenderman => RoleTypes.Impostor,
            CustomRoles.Amogus => RoleTypes.Impostor,
            CustomRoles.Weatherman => RoleTypes.Impostor,
            CustomRoles.Vortex => RoleTypes.Impostor,
            CustomRoles.Beehive => RoleTypes.Impostor,
            CustomRoles.RouleteGrandeur => RoleTypes.Impostor,
            CustomRoles.Nonplus => RoleTypes.Impostor,
            CustomRoles.Tremor => RoleTypes.Impostor,
            CustomRoles.Evolver => RoleTypes.Impostor,
            CustomRoles.Rogue => RoleTypes.Impostor,
            CustomRoles.Patroller => RoleTypes.Impostor,
            CustomRoles.Simon => RoleTypes.Impostor,
            CustomRoles.Chemist => RoleTypes.Impostor,
            CustomRoles.Samurai => RoleTypes.Impostor,
            CustomRoles.QuizMaster => RoleTypes.Impostor,
            CustomRoles.Bargainer => RoleTypes.Impostor,
            CustomRoles.Tiger => RoleTypes.Impostor,
            CustomRoles.SoulHunter => RoleTypes.Impostor,
            CustomRoles.Enderman => RoleTypes.Impostor,
            CustomRoles.Mycologist => RoleTypes.Impostor,
            CustomRoles.Bubble => RoleTypes.Impostor,
            CustomRoles.Hookshot => RoleTypes.Impostor,
            CustomRoles.Sprayer => RoleTypes.Impostor,
            CustomRoles.Infection => RoleTypes.Impostor,
            CustomRoles.Curser => RoleTypes.Impostor,
            CustomRoles.Postman => RoleTypes.Impostor,
            CustomRoles.Thief => RoleTypes.Impostor,
            CustomRoles.Dealer => RoleTypes.Impostor,
            CustomRoles.Auditor => RoleTypes.Impostor,
            CustomRoles.Seamstress => RoleTypes.Shapeshifter,
            CustomRoles.Spirit => RoleTypes.Shapeshifter,
            CustomRoles.Starspawn => RoleTypes.Impostor,
            CustomRoles.Shifter => RoleTypes.Impostor,
            CustomRoles.Impartial => RoleTypes.Impostor,
            CustomRoles.Gaslighter => RoleTypes.Impostor,
            CustomRoles.Backstabber => RoleTypes.Impostor,
            CustomRoles.Predator => RoleTypes.Impostor,
            CustomRoles.Reckless => RoleTypes.Impostor,
            CustomRoles.Magician => RoleTypes.Impostor,
            CustomRoles.WeaponMaster => RoleTypes.Impostor,
            CustomRoles.Pyromaniac => RoleTypes.Impostor,
            CustomRoles.Eclipse => RoleTypes.Impostor,
            CustomRoles.Vengeance => RoleTypes.Impostor,
            CustomRoles.HeadHunter => RoleTypes.Impostor,
            CustomRoles.Pulse => RoleTypes.Impostor,
            CustomRoles.Werewolf => RoleTypes.Impostor,
            CustomRoles.Bandit => RoleTypes.Impostor,
            CustomRoles.Maverick => RoleTypes.Impostor,
            CustomRoles.Parasite => RoleTypes.Shapeshifter,
            CustomRoles.Follower => RoleTypes.Impostor,
            CustomRoles.Romantic => RoleTypes.Impostor,
            CustomRoles.VengefulRomantic => RoleTypes.Impostor,
            CustomRoles.RuthlessRomantic => RoleTypes.Impostor,
            CustomRoles.Cultist => RoleTypes.Impostor,
            CustomRoles.Necromancer => RoleTypes.Impostor,
            CustomRoles.Deathknight => RoleTypes.Impostor,
            CustomRoles.Virus => RoleTypes.Impostor,
            CustomRoles.Investigator => UsePets && Investigator.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Ritualist => RoleTypes.Impostor,
            CustomRoles.Pickpocket => RoleTypes.Impostor,
            CustomRoles.Traitor => Traitor.LegacyTraitor.GetBool() ? RoleTypes.Shapeshifter : RoleTypes.Impostor,
            CustomRoles.PlagueBearer => RoleTypes.Impostor,
            CustomRoles.Pestilence => RoleTypes.Impostor,
            CustomRoles.Spiritcaller => RoleTypes.Impostor,
            CustomRoles.Doppelganger => RoleTypes.Impostor,
            CustomRoles.Investor => RoleTypes.Impostor,
            CustomRoles.Wizard => RoleTypes.Phantom,
            CustomRoles.Carrier => RoleTypes.Shapeshifter,

            CustomRoles.CovenLeader => RoleTypes.Impostor,
            CustomRoles.SpellCaster => RoleTypes.Impostor,
            CustomRoles.PotionMaster => RoleTypes.Impostor,
            CustomRoles.Poache => RoleTypes.Impostor,
            CustomRoles.Reaper => RoleTypes.Impostor,
            CustomRoles.Death => RoleTypes.Impostor,
            CustomRoles.VoodooMaster => RoleTypes.Impostor,
            CustomRoles.Goddess => RoleTypes.Phantom,
            CustomRoles.Dreamweaver => RoleTypes.Impostor,
            CustomRoles.Banshee => RoleTypes.Phantom,
            CustomRoles.Illusionist => RoleTypes.Shapeshifter,
            CustomRoles.Timelord => RoleTypes.Impostor,
            CustomRoles.Enchanter => RoleTypes.Impostor,
            CustomRoles.Siren => RoleTypes.Phantom,
            CustomRoles.Wyrd => RoleTypes.Shapeshifter,
            CustomRoles.MoonDancer => RoleTypes.Phantom,
            CustomRoles.Empress => RoleTypes.Phantom,

            _ => RoleTypes.GuardianAngel
        };
    }

    public static bool IsAdditionRole(this CustomRoles role)
    {
        return role > CustomRoles.NotAssigned;
    }

    public static bool IsNonNK(this CustomRoles role, bool check = false)
    {
        return (!check && role == CustomRoles.Arsonist && CanCheck && Options.IsLoaded && Arsonist.ArsonistKeepsGameGoing != null && !Arsonist.ArsonistKeepsGameGoing.GetBool()) || role.GetNeutralRoleCategory() is RoleOptionType.Neutral_Benign or RoleOptionType.Neutral_Evil or RoleOptionType.Neutral_Pariah;
    }

    public static bool IsNK(this CustomRoles role, bool check = false)
    {
        return (role == CustomRoles.Arsonist && (check || !CanCheck || !Options.IsLoaded || Arsonist.ArsonistKeepsGameGoing == null || Arsonist.ArsonistKeepsGameGoing.GetBool())) || role is
            CustomRoles.Jackal or
            CustomRoles.Glitch or
            CustomRoles.Sidekick or
            CustomRoles.HexMaster or
            CustomRoles.Doppelganger or
            CustomRoles.Cultist or
            CustomRoles.Demon or
            CustomRoles.Crewpostor or
            CustomRoles.Hypocrite or
            CustomRoles.Cherokious or
            CustomRoles.Necromancer or
            CustomRoles.Agitator or
            CustomRoles.Wraith or
            CustomRoles.Bandit or
            CustomRoles.Medusa or
            CustomRoles.Pelican or
            CustomRoles.Convict or
            CustomRoles.Stalker or
            CustomRoles.Juggernaut or
            CustomRoles.Jinx or
            CustomRoles.Poisoner or
            CustomRoles.Renegade or
            CustomRoles.Gaslighter or
            CustomRoles.Simon or
            CustomRoles.Patroller or
            CustomRoles.Rogue or
            CustomRoles.Parasite or
            CustomRoles.Berserker or
            CustomRoles.SerialKiller or
            CustomRoles.Quarry or
            CustomRoles.Accumulator or
            CustomRoles.Spider or
            CustomRoles.SoulCollector or
            CustomRoles.Sharpshooter or
            CustomRoles.Explosivist or
            CustomRoles.Thanos or
            CustomRoles.Duality or
            CustomRoles.Slenderman or
            CustomRoles.Amogus or
            CustomRoles.Weatherman or
            CustomRoles.NoteKiller or
            CustomRoles.Vortex or
            CustomRoles.Beehive or
            CustomRoles.RouleteGrandeur or
            CustomRoles.Nonplus or
            CustomRoles.Tremor or
            CustomRoles.Evolver or
            CustomRoles.Chemist or
            CustomRoles.QuizMaster or
            CustomRoles.Samurai or
            CustomRoles.Bargainer or
            CustomRoles.Tiger or
            CustomRoles.Enderman or
            CustomRoles.Mycologist or
            CustomRoles.Bubble or
            CustomRoles.Hookshot or
            CustomRoles.Sprayer or
            CustomRoles.Magician or
            CustomRoles.WeaponMaster or
            CustomRoles.Reckless or
            CustomRoles.Eclipse or
            CustomRoles.Pyromaniac or
            CustomRoles.Vengeance or
            CustomRoles.HeadHunter or
            CustomRoles.Pulse or
            CustomRoles.Werewolf or
            CustomRoles.Ritualist or
            CustomRoles.Pickpocket or
            CustomRoles.Infection or
            CustomRoles.Traitor or
            CustomRoles.Virus or
            CustomRoles.BloodKnight or
            CustomRoles.Spiritcaller or
            CustomRoles.RuthlessRomantic or
            CustomRoles.PlagueBearer or
            CustomRoles.Pestilence;
    }

    public static bool IsGhostRole(this CustomRoles role)
    {
        return role == CustomRoles.EvilSpirit || GhostRolesManager.CreateGhostRoleInstance(role, true) != null;
    }

    public static bool IsImpostor(this CustomRoles role)
    {
        return (role == CustomRoles.DoubleAgent && (!Options.IsLoaded || !Main.IntroDestroyed)) || role is
            CustomRoles.Impostor or
            CustomRoles.ImpostorEHR or
            CustomRoles.Phantom or
            CustomRoles.PhantomEHR or
            CustomRoles.Shapeshifter or
            CustomRoles.ShapeshifterEHR or
            CustomRoles.Viper or
            CustomRoles.ViperEHR or
            CustomRoles.LovingImpostor or
            CustomRoles.Godfather or
            CustomRoles.Consigliere or
            CustomRoles.Wildling or
            CustomRoles.Silencer or
            CustomRoles.Morphling or
            CustomRoles.BountyHunter or
            CustomRoles.Vampire or
            CustomRoles.Witch or
            CustomRoles.Vindicator or
            CustomRoles.Zombie or
            CustomRoles.Warlock or
            CustomRoles.Ninja or
            CustomRoles.Undertaker or
            CustomRoles.Anonymous or
            CustomRoles.Visionary or
            CustomRoles.Miner or
            CustomRoles.Escapist or
            CustomRoles.Mercenary or
            CustomRoles.Overheat or
            CustomRoles.Abyssbringer or
            CustomRoles.Echo or
            CustomRoles.Ventriloquist or
            CustomRoles.Augmenter or
            CustomRoles.Inhibitor or
            CustomRoles.Wiper or
            CustomRoles.Psychopath or
            CustomRoles.Ambusher or
            CustomRoles.Kidnapper or
            CustomRoles.Exclusionary or
            CustomRoles.Catalyst or
            CustomRoles.Fabricator or
            CustomRoles.Centralizer or
            CustomRoles.Postponer or
            CustomRoles.Venerer or
            CustomRoles.ClockBlocker or
            CustomRoles.Forger or
            CustomRoles.Stasis or
            CustomRoles.Occultist or
            CustomRoles.Wasp or
            CustomRoles.Hypnotist or
            CustomRoles.Assumer or
            CustomRoles.Generator or
            CustomRoles.Blackmailer or
            CustomRoles.Commander or
            CustomRoles.Freezer or
            CustomRoles.Changeling or
            CustomRoles.Swapster or
            CustomRoles.Loner or
            CustomRoles.Kamikaze or
            CustomRoles.Librarian or
            CustomRoles.Cantankerous or
            CustomRoles.Swiftclaw or
            CustomRoles.Duellist or
            CustomRoles.YinYanger or
            CustomRoles.Consort or
            CustomRoles.Framer or
            CustomRoles.Mafioso or
            CustomRoles.Nullifier or
            CustomRoles.Chronomancer or
            CustomRoles.Stealth or
            CustomRoles.Penguin or
            CustomRoles.Sapper or
            CustomRoles.Hitman or
            CustomRoles.RiftMaker or
            CustomRoles.Mastermind or
            CustomRoles.Gambler or
            CustomRoles.Councillor or
            CustomRoles.Saboteur or
            CustomRoles.Puppeteer or
            CustomRoles.TimeThief or
            CustomRoles.Trickster or
            CustomRoles.Nemesis or
            CustomRoles.KillingMachine or
            CustomRoles.Fireworker or
            CustomRoles.Sniper or
            CustomRoles.EvilTracker or
            CustomRoles.EvilGuesser or
            CustomRoles.AntiAdminer or
            CustomRoles.Arrogance or
            CustomRoles.Bomber or
            CustomRoles.Nuker or
            CustomRoles.Scavenger or
            CustomRoles.Trapster or
            CustomRoles.Capitalist or
            CustomRoles.Gangster or
            CustomRoles.Cleaner or
            CustomRoles.Lightning or
            CustomRoles.Greedy or
            CustomRoles.CursedWolf or
            CustomRoles.SoulCatcher or
            CustomRoles.QuickShooter or
            CustomRoles.EvilEraser or
            CustomRoles.Butcher or
            CustomRoles.Hangman or
            CustomRoles.Bard or
            CustomRoles.Swooper or
            CustomRoles.Disperser or
            CustomRoles.Dazzler or
            CustomRoles.Deathpact or
            CustomRoles.Devourer or
            CustomRoles.Camouflager or
            CustomRoles.Twister or
            CustomRoles.Lurker;
    }

    public static bool IsNeutral(this CustomRoles role, bool check = false)
    {
        return role.IsNK(check) || role.IsNonNK(check);
    }

    public static bool IsAbleToBeSidekicked(this CustomRoles role)
    {
        return !role.IsRecruitingRole() && role is not
            CustomRoles.Deathknight;
    }

    public static bool IsEvilAddon(this CustomRoles role)
    {
        return role is
            CustomRoles.Madmate or
            CustomRoles.Egoist or
            CustomRoles.Charmed or
            CustomRoles.Contagious or
            CustomRoles.Rascal or
            CustomRoles.Entranced;
    }

    public static bool IsRecruitingRole(this CustomRoles role)
    {
        return role is
            CustomRoles.Jackal or
            CustomRoles.Cultist or
            CustomRoles.Necromancer or
            CustomRoles.Virus or
            CustomRoles.Spiritcaller;
    }

    public static bool IsMadmate(this CustomRoles role)
    {
        return role is
            CustomRoles.Hypocrite or
            CustomRoles.Crewpostor or
            CustomRoles.Convict or
            CustomRoles.Renegade or
            CustomRoles.Parasite;
    }

    public static bool IsTasklessCrewmate(this CustomRoles role)
    {
        return !role.UsesPetInsteadOfKill() && role.IsCrewmate() && role.IsDesyncRole();
    }

    public static bool UsesMeetingShapeshift(this CustomRoles role)
    {
        if (!Options.UseMeetingShapeshift.GetBool()) return false;
        Type type = role.GetRoleClass().GetType();
        return type.GetMethod("OnMeetingShapeshift")?.DeclaringType == type;
    }

    public static bool PetActivatedAbility(this CustomRoles role)
    {
        if (Options.CurrentGameMode == CustomGameMode.CaptureTheFlag) return true;

        if (!Options.UsePets.GetBool()) return false;

        if (role.UsesPetInsteadOfKill()) return true;

        if (Options.UsePhantomBasis.GetBool() && (!role.IsNK() || Options.UsePhantomBasisForNKs.GetBool()) && role.SimpleAbilityTrigger()) return false;

        Type type = role.GetRoleClass().GetType();
        return type.GetMethod("OnPet")?.DeclaringType == type;
    }

    public static bool UsesPetInsteadOfKill(this CustomRoles role)
    {
        return Options.UsePets.GetBool() && role switch
        {
            CustomRoles.Gaulois when Gaulois.UsePet.GetBool() => true,
            CustomRoles.Aid when Aid.UsePet.GetBool() => true,
            CustomRoles.Socialite when Socialite.UsePet.GetBool() => true,
            CustomRoles.Escort when Escort.UsePet.GetBool() => true,
            CustomRoles.DonutDelivery when DonutDelivery.UsePet.GetBool() => true,
            CustomRoles.Analyst when Analyst.UsePet.GetBool() => true,
            CustomRoles.Jailor when Jailor.UsePet.GetBool() => true,
            CustomRoles.Sheriff when Sheriff.UsePet.GetBool() => true,
            CustomRoles.Vigilante when Vigilante.UsePet.GetBool() => true,
            CustomRoles.Medic when Medic.UsePet.GetBool() => true,
            CustomRoles.Monarch when Monarch.UsePet.GetBool() => true,
            CustomRoles.CopyCat when CopyCat.UsePet.GetBool() => true,
            CustomRoles.Investigator when Investigator.UsePet.GetBool() => true,
            CustomRoles.Deputy when Deputy.UsePet.GetBool() => true,
            CustomRoles.Bestower when Bestower.UsePet.GetBool() => true,
            CustomRoles.Retributionist when Retributionist.UsePet.GetBool() => true,
            CustomRoles.Crusader when Crusader.UsePet.GetBool() => true,
            CustomRoles.Witness when Options.WitnessUsePet.GetBool() => true,

            _ => false
        };
    }

    public static bool CancelsVote(this CustomRoles role)
    {
        return role switch
        {
            CustomRoles.FortuneTeller when FortuneTeller.CancelVote.GetBool() => true,
            CustomRoles.Soothsayer when Soothsayer.CancelVote.GetBool() => true,
            CustomRoles.Oracle when Oracle.CancelVote.GetBool() => true,
            CustomRoles.EvilEraser when EvilEraser.CancelVote.GetBool() => true,
            CustomRoles.Tether when Tether.CancelVote.GetBool() => true,
            CustomRoles.Ricochet when Ricochet.CancelVote.GetBool() => true,
            CustomRoles.Cleanser when Cleanser.CancelVote.GetBool() => true,
            CustomRoles.NiceEraser when NiceEraser.CancelVote.GetBool() => true,
            CustomRoles.Scout when Scout.CancelVote.GetBool() => true,
            CustomRoles.Markseeker when Markseeker.CancelVote.GetBool() => true,
            CustomRoles.Godfather when Options.GodfatherCancelVote.GetBool() => true,
            CustomRoles.Socialite when Socialite.CancelVote.GetBool() => true,
            CustomRoles.Negotiator when Negotiator.CancelVote.GetBool() => true,
            CustomRoles.Clerk when Clerk.CancelVote.GetBool() => true,

            CustomRoles.President => true,

            _ => false
        };
    }

    public static bool OnlySpawnsWithPets(this CustomRoles role)
    {
        return !(((Options.UsePhantomBasis.GetBool() && (!role.IsNeutral() || Options.UsePhantomBasisForNKs.GetBool()))) && role.SimpleAbilityTrigger() && role != CustomRoles.Chemist && !role.AlwaysUsesPhantomBase()) && OnlySpawnsWithPetsRoleList.Contains(role);
    }

    public static bool NeedUpdateOnLights(this CustomRoles role)
    {
        return role is CustomRoles.Wiper;
    }

    public static bool IsBetrayalAddon(this CustomRoles role)
    {
        return role is
            CustomRoles.Charmed or
            CustomRoles.Contagious or
            CustomRoles.Lovers or
            CustomRoles.Madmate or
            CustomRoles.Undead or
            CustomRoles.Egoist or
            CustomRoles.Entranced;
    }

    public static bool IsImpOnlyAddon(this CustomRoles role)
    {
        return Options.GroupedAddons[AddonTypes.ImpOnly].Contains(role);
    }

    public static bool NeedsUpdateAfterDeath(this CustomRoles role)
    {
        return role is
            CustomRoles.KOTZPlayer or
            CustomRoles.CTFPlayer or
            CustomRoles.Challenger or
            CustomRoles.BedWarsPlayer or
            CustomRoles.Weatherman or
            CustomRoles.Altruist or
            CustomRoles.Duellist;
    }

    public static bool IsTaskBasedCrewmate(this CustomRoles role)
    {
        return role is
            CustomRoles.Helper or
            CustomRoles.Snitch or
            CustomRoles.Speedrunner or
            CustomRoles.Marshall or
            CustomRoles.TimeManager or
            CustomRoles.Ignitor or
            CustomRoles.Guardian or
            CustomRoles.Merchant or
            CustomRoles.Mayor or
            CustomRoles.Insight or
            CustomRoles.Decryptor or
            CustomRoles.Transporter;
    }

    public static bool IsNotKnightable(this CustomRoles role)
    {
        return role is
            CustomRoles.Mayor or
            CustomRoles.Vindicator or
            CustomRoles.Dictator or
            CustomRoles.Knighted or
            CustomRoles.Glitch or
            CustomRoles.Pickpocket or
            CustomRoles.Stealer;
    }

    public static bool IsNotAssignableMidGame(this CustomRoles role)
    {
        return role is
            CustomRoles.Egoist or
            CustomRoles.Workhorse or
            CustomRoles.Cleansed or
            CustomRoles.Busy or
            CustomRoles.Lovers or
            CustomRoles.Stressed or
            CustomRoles.Lazy or
            CustomRoles.Rascal or
            CustomRoles.LastImpostor;
    }

    public static bool ForceCancelShapeshift(this CustomRoles role)
    {
        return role is
            CustomRoles.Glitch or
            CustomRoles.Illusionist or
            CustomRoles.Swapster or
            CustomRoles.Echo or
            CustomRoles.Hangman or
            CustomRoles.Generator;
    }

    public static bool IsNoAnimationShifter(this CustomRoles role)
    {
        return role is
            CustomRoles.Glitch or
            CustomRoles.Generator or
            CustomRoles.Echo;
    }

    public static bool AlwaysUsesPhantomBase(this CustomRoles role)
    {
        return role is
            CustomRoles.Empress or
            CustomRoles.Wizard;
    }

    public static bool SimpleAbilityTrigger(this CustomRoles role)
    {
        return role switch
        {
            CustomRoles.Stealth => !Stealth.UseLegacyVersion.GetBool(),
            CustomRoles.Trapster => !Trapster.LegacyTrapster.GetBool(),
            _ => role is CustomRoles.Dasher
                         or CustomRoles.CTFPlayer
                         or CustomRoles.BedWarsPlayer
                         or CustomRoles.Racer
                         or CustomRoles.SnowdownPlayer
                         or CustomRoles.Wizard
                         or CustomRoles.AntiAdminer
                         or CustomRoles.Stasis
                         or CustomRoles.Occultist
                         or CustomRoles.Overheat
                         or CustomRoles.Warlock
                         or CustomRoles.Swiftclaw
                         or CustomRoles.Undertaker
                         or CustomRoles.Abyssbringer
                         or CustomRoles.Ambusher
                         or CustomRoles.Bomber
                         or CustomRoles.Nuker
                         or CustomRoles.Camouflager
                         or CustomRoles.Centralizer
                         or CustomRoles.Disperser
                         or CustomRoles.Escapist
                         or CustomRoles.Fireworker
                         or CustomRoles.Hypnotist
                         or CustomRoles.Librarian
                         or CustomRoles.Miner
                         or CustomRoles.RiftMaker
                         or CustomRoles.Ninja
                         or CustomRoles.QuickShooter
                         or CustomRoles.Sapper
                         or CustomRoles.Sniper
                         or CustomRoles.Trapster
                         or CustomRoles.Twister
                         or CustomRoles.Swooper
                         or CustomRoles.Venerer
                         or CustomRoles.Wraith
                         or CustomRoles.RouleteGrandeur
                         or CustomRoles.Enderman
                         or CustomRoles.Explosivist
                         or CustomRoles.Spider
                         or CustomRoles.Hookshot
                         or CustomRoles.Mycologist
                         or CustomRoles.Magician
                         or CustomRoles.Sprayer
                         or CustomRoles.Werewolf
                         or CustomRoles.WeaponMaster
                         or CustomRoles.Thanos
                         or CustomRoles.Tiger
                         or CustomRoles.Bargainer
                         or CustomRoles.Chemist
                         or CustomRoles.Simon
                         or CustomRoles.Sharpshooter
                         or CustomRoles.Patroller
                         or CustomRoles.Weatherman
                         or CustomRoles.NoteKiller
                         or CustomRoles.Amogus
                         or CustomRoles.Auditor
                         or CustomRoles.Magistrate
        };
    }

    public static bool CheckAddonConflict(CustomRoles role, PlayerControl pc)
    {
        return role.IsAdditionRole() && !(role.IsGhostRole() && pc.IsAlive()) && (!Main.NeverSpawnTogetherCombos.TryGetValue(OptionItem.CurrentPreset, out Dictionary<CustomRoles, List<CustomRoles>> neverList) || !neverList.TryGetValue(pc.GetCustomRole(), out List<CustomRoles> bannedAddonList) || !bannedAddonList.Contains(role)) && pc.GetCustomRole() is not CustomRoles.GuardianAngelEHR and not CustomRoles.God && !pc.Is(CustomRoles.Madmate) && !pc.Is(CustomRoles.GM) && role is not CustomRoles.Lovers && !pc.Is(CustomRoles.LazyGuy) && (!pc.HasSubRole() || pc.GetCustomSubRoles().Count < Options.NoLimitAddonsNumMax.GetInt()) && (!Options.AddonCanBeSettings.TryGetValue(role, out (OptionItem Imp, OptionItem Neutral, OptionItem Crew, OptionItem Coven) o) || ((o.Imp.GetBool() || !pc.GetCustomRole().IsImpostor()) && (o.Neutral.GetBool() || !pc.GetCustomRole().IsNeutral()) && (o.Crew.GetBool() || !pc.IsCrewmate()) && (o.Coven.GetBool() || !pc.Is(Team.Coven)))) && (!role.IsImpOnlyAddon() || (pc.IsImpostor() && !pc.Is(CustomRoles.DoubleAgent)) || (pc.Is(CustomRoles.Traitor) && Traitor.CanGetImpostorOnlyAddons.GetBool())) && role switch
        {
            CustomRoles.Blind when pc.Is(CustomRoles.Sensor) => false,
            CustomRoles.Composter when float.IsNaN(pc.GetAbilityUseLimit()) => false,
            CustomRoles.TaskMaster when !pc.IsCrewmate() || !Utils.HasTasks(pc.Data, forRecompute: false) => false,
            CustomRoles.Commited when pc.GetRoleTypes() is not (RoleTypes.Impostor or RoleTypes.Phantom or RoleTypes.Shapeshifter or RoleTypes.Viper) => false,
            CustomRoles.Underdog when pc.Is(CustomRoles.Mare) => false,
            CustomRoles.Shy when Options.DisableWhisperCommand.GetBool() => false,
            CustomRoles.Listener when Options.DisableWhisperCommand.GetBool() => false,
            CustomRoles.Blocked when !pc.CanUseVent() => false,
            CustomRoles.Aide when pc.IsMadmate() || pc.Is(CustomRoles.Saboteur) => false,
            CustomRoles.Sleuth when pc.GetCustomRole() is CustomRoles.NecroGuesser or CustomRoles.Imitator or CustomRoles.Forensic => false,
            CustomRoles.Introvert when pc.GetCustomRole() is CustomRoles.Leery or CustomRoles.Samurai or CustomRoles.Arsonist or CustomRoles.Revolutionist or CustomRoles.Investigator or CustomRoles.Scavenger or CustomRoles.Analyst => false,
            CustomRoles.Circumvent when pc.GetCustomRole() is CustomRoles.Swooper or CustomRoles.RiftMaker => false,
            CustomRoles.Oblivious when pc.Is(CustomRoles.Altruist) => false,
            CustomRoles.AntiTP when pc.GetCustomRole() is CustomRoles.Transmitter or CustomRoles.Miner or CustomRoles.Escapist or CustomRoles.Tunneler or CustomRoles.Ninja => false,
            CustomRoles.Swift when pc.Is(CustomRoles.Stealth) => false,
            CustomRoles.BananaMan when pc.Is(CustomRoles.Disco) || pc.Is(CustomRoles.Doppelganger) => false,
            CustomRoles.Disco when pc.GetCustomRole() is CustomRoles.Chameleon or CustomRoles.Swooper or CustomRoles.Wraith or CustomRoles.Alchemist or CustomRoles.Doppelganger => false,
            CustomRoles.Egoist when pc.Is(CustomRoles.Gangster) => false,
            CustomRoles.Nimble when pc.GetCustomRole() is CustomRoles.Oxyman or CustomRoles.EngineerEHR or CustomRoles.Mechanic => false,
            CustomRoles.Magnet when pc.Is(Team.Impostor) => false,
            CustomRoles.Swift when pc.Is(CustomRoles.Magnet) => false,
            CustomRoles.Oblivious when pc.Is(CustomRoles.Amnesiac) && Amnesiac.RememberMode.GetValue() == 0 => false,
            CustomRoles.Rookie when pc.GetRoleTypes() is not (RoleTypes.Impostor or RoleTypes.Phantom or RoleTypes.Shapeshifter or RoleTypes.Viper) => false,
            CustomRoles.Madmate when pc.Is(CustomRoles.Sidekick) => false,
            CustomRoles.Autopsy when pc.Is(CustomRoles.Doctor) || pc.Is(CustomRoles.Tracefinder) || pc.Is(CustomRoles.Scientist) || pc.Is(CustomRoles.ScientistEHR) || pc.Is(CustomRoles.Sunnyboy) => false,
            CustomRoles.Necroview when pc.Is(CustomRoles.Doctor) => false,
            CustomRoles.Lazy when pc.Is(CustomRoles.Speedrunner) => false,
            CustomRoles.Mischievous when pc.Is(Team.Impostor) || (!pc.GetCustomRole().IsDesyncRole() && !pc.Is(CustomRoles.Bloodlust)) || !pc.IsNeutralKiller() || Main.PlayerStates[pc.PlayerId].Role.CanUseSabotage(pc) => false,
            CustomRoles.Loyal when pc.IsCrewmate() && !Options.CrewCanBeLoyal.GetBool() => false,
            CustomRoles.Lazy when pc.Is(CustomRoles.LazyGuy) || pc.Is(CustomRoles.Snitch) || pc.Is(CustomRoles.Marshall) || pc.Is(CustomRoles.Transporter) || pc.Is(CustomRoles.Guardian) => false,
            CustomRoles.Tiebreaker when pc.Is(CustomRoles.Dictator) => false,
            CustomRoles.Stressed when !pc.IsCrewmate() || pc.GetCustomRole().IsTasklessCrewmate() => false,
            CustomRoles.Stealer or CustomRoles.Swift or CustomRoles.DeadlyQuota or CustomRoles.Damocles or CustomRoles.Mare when pc.Is(CustomRoles.Bomber) || pc.Is(CustomRoles.Nuker) || pc.Is(CustomRoles.Trapster) || pc.Is(CustomRoles.Capitalist) => false,
            CustomRoles.Torch when !pc.IsCrewmate() || pc.Is(CustomRoles.Bewilder) || pc.Is(CustomRoles.Sunglasses) || pc.Is(CustomRoles.GuardianAngelEHR) => false,
            CustomRoles.Bewilder when pc.Is(CustomRoles.Torch) || pc.Is(CustomRoles.GuardianAngelEHR) || pc.Is(CustomRoles.Sunglasses) => false,
            CustomRoles.Dynamo when pc.Is(CustomRoles.Spurt) || pc.GetCustomRole() is CustomRoles.Tank or CustomRoles.Ankylosaurus => false,
            CustomRoles.Spurt when pc.GetCustomSubRoles().Any(x => x is CustomRoles.Dynamo or CustomRoles.Swiftclaw or CustomRoles.Giant or CustomRoles.Flash) || pc.Is(CustomRoles.Ankylosaurus) => false,
            CustomRoles.Sunglasses when pc.Is(CustomRoles.Torch) || pc.Is(CustomRoles.GuardianAngelEHR) || pc.Is(CustomRoles.Bewilder) => false,
            CustomRoles.Guesser when pc.GetCustomRole() is CustomRoles.EvilGuesser or CustomRoles.NiceGuesser or CustomRoles.Doomsayer or CustomRoles.CopyCat => false,
            CustomRoles.Madmate when !pc.CanBeMadmate() || pc.Is(CustomRoles.Egoist) || pc.Is(CustomRoles.Rascal) => false,
            CustomRoles.Oblivious when pc.Is(CustomRoles.Forensic) || pc.Is(CustomRoles.Cleaner) || pc.Is(CustomRoles.Medusa) || pc.Is(CustomRoles.Mortician) || pc.Is(CustomRoles.Medium) || pc.Is(CustomRoles.GuardianAngelEHR) => false,
            CustomRoles.Youtuber when !pc.IsCrewmate() || pc.Is(CustomRoles.Madmate) || pc.Is(CustomRoles.Sheriff) || pc.Is(CustomRoles.GuardianAngelEHR) => false,
            CustomRoles.Egoist when !pc.IsImpostor() => false,
            CustomRoles.Damocles when pc.GetCustomRole() is CustomRoles.Bomber or CustomRoles.Nuker or CustomRoles.Mercenary or CustomRoles.Cantankerous => false,
            CustomRoles.Damocles when pc.GetRoleTypes() is not (RoleTypes.Impostor or RoleTypes.Phantom or RoleTypes.Shapeshifter or RoleTypes.Viper) => false,
            CustomRoles.Flash when pc.Is(CustomRoles.Swiftclaw) || pc.Is(CustomRoles.Giant) || pc.Is(CustomRoles.Spurt) || pc.GetCustomRole() is CustomRoles.Tank or CustomRoles.Zombie => false,
            CustomRoles.Giant when pc.Is(CustomRoles.Flash) || pc.Is(CustomRoles.Spurt) || pc.Is(CustomRoles.RoomRusher) => false,
            CustomRoles.Necroview when pc.Is(CustomRoles.Visionary) => false,
            CustomRoles.Mimic when pc.Is(CustomRoles.Nemesis) => false,
            CustomRoles.Rascal when !pc.IsCrewmate() => false,
            CustomRoles.LazyGuy when pc.GetCustomRole().IsAdditionRole() => false,
            CustomRoles.Stealer when pc.Is(CustomRoles.Vindicator) => false,
            CustomRoles.Bloodlust when !pc.GetCustomRole().IsCrewmate() || pc.GetCustomRole().IsTaskBasedCrewmate() || pc.GetCustomRole() is CustomRoles.Medic => false,
            CustomRoles.Mare when pc.GetCustomRole() is CustomRoles.Inhibitor or CustomRoles.Saboteur or CustomRoles.Swift or CustomRoles.Nemesis or CustomRoles.Sniper or CustomRoles.Fireworker or CustomRoles.Swooper or CustomRoles.Vampire => false,
            CustomRoles.Torch when pc.GetCustomRole() is CustomRoles.Lighter or CustomRoles.Ignitor or CustomRoles.Investigator or CustomRoles.Eclipse or CustomRoles.Decryptor => false,
            CustomRoles.Bewilder when pc.GetCustomRole() is CustomRoles.Lighter or CustomRoles.Ignitor or CustomRoles.Investigator or CustomRoles.Eclipse or CustomRoles.Decryptor => false,
            CustomRoles.Sunglasses when pc.GetCustomRole() is CustomRoles.Lighter or CustomRoles.Ignitor or CustomRoles.Investigator or CustomRoles.Eclipse or CustomRoles.Decryptor => false,
            CustomRoles.Bait when pc.Is(CustomRoles.Demolitionist) => false,
            CustomRoles.Beartrap when pc.Is(CustomRoles.Demolitionist) => false,
            CustomRoles.Lovers when pc.Is(CustomRoles.Romantic) => false,
            CustomRoles.Mare when pc.Is(CustomRoles.LastImpostor) => false,
            CustomRoles.Swift when pc.Is(CustomRoles.Mare) => false,
            CustomRoles.Schizophrenic when (!pc.IsImpostor() && !pc.IsCrewmate()) || pc.Is(CustomRoles.Madmate) => false,
            CustomRoles.Loyal when (!pc.IsImpostor() && !pc.IsCrewmate()) || pc.Is(CustomRoles.Madmate) => false,
            CustomRoles.Loyal when pc.IsImpostor() && !Options.ImpCanBeLoyal.GetBool() => false,
            CustomRoles.Seer when pc.GetCustomRole() is CustomRoles.Mortician or CustomRoles.TimeMaster => false,
            CustomRoles.Onbound when pc.GetCustomRole() is CustomRoles.SuperStar or CustomRoles.Ankylosaurus or CustomRoles.Car or CustomRoles.Unbound => false,
            CustomRoles.Rascal when pc.Is(CustomRoles.SuperStar) || pc.Is(CustomRoles.Madmate) => false,
            CustomRoles.Madmate when pc.Is(CustomRoles.SuperStar) => false,
            CustomRoles.Gravestone when pc.GetCustomRole() is CustomRoles.SuperStar or CustomRoles.Innocent => false,
            CustomRoles.Lucky when pc.Is(CustomRoles.Guardian) => false,
            CustomRoles.Bait when pc.Is(CustomRoles.GuardianAngelEHR) => false,
            CustomRoles.Bait when pc.Is(CustomRoles.Beartrap) => false,
            CustomRoles.Beartrap when pc.Is(CustomRoles.Bait) => false,
            CustomRoles.Schizophrenic when pc.Is(CustomRoles.Dictator) => false,
            CustomRoles.Swift when pc.GetCustomRole() is CustomRoles.Swooper or CustomRoles.Vampire or CustomRoles.Scavenger or CustomRoles.Puppeteer or CustomRoles.Warlock or CustomRoles.Consigliere or CustomRoles.Witch or CustomRoles.Nemesis => false,
            CustomRoles.Reach when pc.GetCustomRole() is CustomRoles.Mafioso or CustomRoles.Evolver or CustomRoles.Berserker => false,
            CustomRoles.Beartrap when pc.Is(CustomRoles.GuardianAngelEHR) => false,
            CustomRoles.Reach when pc.GetRoleTypes() is not (RoleTypes.Impostor or RoleTypes.Phantom or RoleTypes.Shapeshifter or RoleTypes.Viper) => false,
            CustomRoles.Magnet when pc.GetRoleTypes() is not (RoleTypes.Impostor or RoleTypes.Phantom or RoleTypes.Shapeshifter or RoleTypes.Viper) => false,
            CustomRoles.Haste when pc.GetRoleTypes() is not (RoleTypes.Impostor or RoleTypes.Phantom or RoleTypes.Shapeshifter or RoleTypes.Viper) || !pc.CanUseImpostorVentButton() || pc.Is(CustomRoles.Glitch) => false,
            CustomRoles.Diseased when pc.Is(CustomRoles.Antidote) => false,
            CustomRoles.Antidote when pc.Is(CustomRoles.Diseased) => false,
            CustomRoles.Flash or CustomRoles.Giant when pc.GetCustomRole() is CustomRoles.Swooper or CustomRoles.Wraith or CustomRoles.Chameleon or CustomRoles.Alchemist or CustomRoles.Ankylosaurus or CustomRoles.Swiftclaw => false,
            CustomRoles.Bait when pc.Is(CustomRoles.Disregarded) => false,
            CustomRoles.Busy when !pc.GetTaskState().HasTasks => false,
            CustomRoles.Truant when pc.Is(CustomRoles.SoulHunter) => false,
            CustomRoles.Nimble when !pc.IsCrewmate() => false,
            CustomRoles.Physicist when !pc.IsCrewmate() || pc.GetCustomRole().IsDesyncRole() || (pc.Is(CustomRoles.TimeMaster) && !TimeMaster.TimeMasterCanUseVitals.GetBool()) => false,
            CustomRoles.Finder when !pc.IsCrewmate() || pc.GetCustomRole().IsDesyncRole() => false,
            CustomRoles.Noisy when !pc.IsCrewmate() || pc.GetCustomRole().IsDesyncRole() => false,
            CustomRoles.Examiner when !pc.IsCrewmate() || pc.GetCustomRole().IsDesyncRole() => false,
            CustomRoles.Venom when !pc.IsImpostor() => false,
            CustomRoles.Disregarded when pc.Is(CustomRoles.Bait) => false,
            CustomRoles.Oblivious when pc.Is(CustomRoles.Coroner) => false,
            CustomRoles.Oblivious when pc.Is(CustomRoles.Vulture) => false,
            CustomRoles.DoubleShot when pc.Is(CustomRoles.CopyCat) => false,
            CustomRoles.Tiebreaker when pc.Is(CustomRoles.Dictator) => false,
            CustomRoles.Lucky when pc.Is(CustomRoles.Luckey) => false,
            CustomRoles.Unlucky when pc.GetCustomRole() is CustomRoles.Luckey or CustomRoles.Tank or CustomRoles.Vector or CustomRoles.Addict or CustomRoles.Lurker => false,
            CustomRoles.Unlucky when pc.Is(CustomRoles.Lucky) => false,
            CustomRoles.Lucky when pc.Is(CustomRoles.Unlucky) => false,
            CustomRoles.Fool when pc.GetCustomRole() is CustomRoles.Mechanic or CustomRoles.GuardianAngelEHR or CustomRoles.Technician => false,
            CustomRoles.Coroner when pc.Is(CustomRoles.Oblivious) => false,
            CustomRoles.Fragile when pc.Is(CustomRoles.Detour) => false,
            CustomRoles.Physicist when pc.GetCustomRole() is CustomRoles.Tracefinder or CustomRoles.Doctor => false,
            CustomRoles.DoubleShot when pc.GetCustomRole() is not CustomRoles.EvilGuesser and not CustomRoles.NiceGuesser and not CustomRoles.Augur && !Options.GuesserMode.GetBool() => false,
            CustomRoles.DoubleShot when !pc.Is(CustomRoles.Guesser) && !pc.Is(CustomRoles.EvilGuesser) && pc.Is(CustomRoleTypes.Impostor) && !Options.ImpostorsCanGuess.GetBool() => false,
            CustomRoles.DoubleShot when !pc.Is(CustomRoles.Guesser) && !pc.Is(CustomRoles.NiceGuesser) && pc.Is(CustomRoleTypes.Crewmate) && !Options.CrewmatesCanGuess.GetBool() => false,
            CustomRoles.DoubleShot when !pc.Is(CustomRoles.Guesser) && ((pc.GetCustomRole().IsNonNK() && !Options.PassiveNeutralsCanGuess.GetBool()) || (pc.IsNeutralKiller() && !Options.NeutralKillersCanGuess.GetBool())) => false,
            CustomRoles.DoubleShot when !pc.Is(CustomRoles.Guesser) && !pc.Is(CustomRoles.Augur) && pc.Is(CustomRoleTypes.Coven) && !Options.CovenCanGuess.GetBool() => false,
            _ => true
        };
    }

    public static Team GetTeam(this CustomRoles role)
    {
        if (role.IsCoven()) return Team.Coven;
        if (role.IsImpostorTeamV2()) return Team.Impostor;
        if (role.IsNeutralTeamV2()) return Team.Neutral;

        return role.IsCrewmateTeamV2() ? Team.Crewmate : Team.None;
    }

    public static bool Is(this CustomRoles role, Team team)
    {
        return team switch
        {
            Team.Coven => role.IsCoven(),
            Team.Impostor => role.IsImpostorTeamV2(),
            Team.Neutral => role.IsNeutralTeamV2(),
            Team.Crewmate => role.IsCrewmateTeamV2(),
            Team.None => role.GetCountTypes() is CountTypes.OutOfGame or CountTypes.None || role == CustomRoles.GM,
            _ => false
        };
    }

    public static bool IsCoven(this CustomRoles role)
    {
        return role is
            CustomRoles.CovenMember or
            CustomRoles.CovenLeader or
            CustomRoles.SpellCaster or
            CustomRoles.PotionMaster or
            CustomRoles.Poache or
            CustomRoles.Reaper or
            CustomRoles.Death or
            CustomRoles.VoodooMaster or
            CustomRoles.Goddess or
            CustomRoles.Augur or
            CustomRoles.Dreamweaver or
            CustomRoles.Banshee or
            CustomRoles.Illusionist or
            CustomRoles.Timelord or
            CustomRoles.Enchanter or
            CustomRoles.Siren or
            CustomRoles.Wyrd or
            CustomRoles.Empress or
            CustomRoles.MoonDancer;
    }

    public static bool IncompatibleWithVenom(this CustomRoles role)
    {
        return role switch
        {
            CustomRoles.Bomber when !Bomber.BomberCanKill.GetBool() => true,
            CustomRoles.Changeling when !Changeling.CanKillBeforeRoleChange.GetBool() => true,
            CustomRoles.EvilEraser when EvilEraser.EraseMethod.GetValue() == 0 => true,
            CustomRoles.Puppeteer when !Puppeteer.PuppeteerCanKillNormally.GetBool() => true,
            CustomRoles.Silencer when Silencer.SilenceMode.GetValue() == 0 => true,
            CustomRoles.Sniper when !Sniper.CanKillWithBullets.GetBool() => true,
            CustomRoles.Vampire when !Vampire.OptionCanKillNormally.GetBool() => true,

            CustomRoles.Augmenter or
                CustomRoles.Lightning or
                CustomRoles.Echo or
                CustomRoles.Fireworker or
                CustomRoles.Ninja or
                CustomRoles.Butcher or
                CustomRoles.Postponer or
                CustomRoles.Sapper or
                CustomRoles.Scavenger or
                CustomRoles.Swift or
                CustomRoles.Swooper or
                CustomRoles.Undertaker or
                CustomRoles.Warlock or
                CustomRoles.Wasp or
                CustomRoles.Wiper => true,

            _ => false
        };
    }

    public static RoleTypes GetRoleTypes(this CustomRoles role)
    {
        if (Enum.TryParse(role.GetVNRole(true).ToString().Replace("EHR", ""), true, out RoleTypes type)) return type;
        return role.IsImpostor() ? RoleTypes.Impostor : RoleTypes.Crewmate;
    }

    public static bool IsDesyncRole(this CustomRoles role)
    {
        return role.GetDYRole() != RoleTypes.GuardianAngel;
    }

    public static bool IsImpostorTeam(this CustomRoles role)
    {
        return role.IsImpostor() || role == CustomRoles.Madmate;
    }

    public static bool IsCrewmate(this CustomRoles role)
    {
        return !role.IsImpostor() && !role.IsNeutral() && !role.IsMadmate() && !role.IsCoven();
    }

    public static bool IsImpostorTeamV2(this CustomRoles role)
    {
        return (role.IsImpostorTeam() && role != CustomRoles.Trickster && !role.IsConverted()) || role is CustomRoles.Rascal or CustomRoles.Madmate || role.IsMadmate();
    }

    public static bool IsNeutralTeamV2(this CustomRoles role)
    {
        return role.IsConverted() || (role.IsNeutral() && role != CustomRoles.Madmate);
    }

    public static bool IsCrewmateTeamV2(this CustomRoles role)
    {
        return (!role.IsImpostorTeamV2() && !role.IsNeutralTeamV2() && !role.Is(Team.Coven)) || (role == CustomRoles.Trickster && !role.IsConverted());
    }

    public static bool IsConverted(this CustomRoles role)
    {
        return (role == CustomRoles.Egoist && Inspector.InspectorCheckEgoistInt() == 1) || role is
            CustomRoles.Charmed or
            CustomRoles.Contagious or
            CustomRoles.Undead or
            CustomRoles.Entranced;
    }

    public static bool IsRevealingRole(this CustomRoles role, PlayerControl target)
    {
        return (role is CustomRoles.Mayor && Mayor.MayorRevealWhenDoneTasks.GetBool() && target.AllTasksCompleted()) ||
               (role is CustomRoles.SuperStar && Options.EveryOneKnowSuperStar.GetBool()) ||
               (role is CustomRoles.Marshall && target.AllTasksCompleted()) ||
               (role is CustomRoles.Workaholic && Workaholic.WorkaholicVisibleToEveryone.GetBool()) ||
               (role is CustomRoles.Doctor && Options.DoctorVisibleToEveryone.GetBool()) ||
               (role is CustomRoles.Bait && Options.BaitNotification.GetBool() && Inspector.InspectorCheckBaitCountType.GetBool());
    }


    public static bool IsVanilla(this CustomRoles role)
    {
        return role is
            CustomRoles.Crewmate or
            CustomRoles.Engineer or
            CustomRoles.Noisemaker or
            CustomRoles.Tracker or
            CustomRoles.Scientist or
            CustomRoles.GuardianAngel or
            CustomRoles.Impostor or
            CustomRoles.Detective or
            CustomRoles.Viper or
            CustomRoles.Phantom or
            CustomRoles.Shapeshifter;
    }

    public static CustomRoleTypes GetCustomRoleTypes(this CustomRoles role)
    {
        var type = CustomRoleTypes.Crewmate;
        if (role.IsImpostor() || role.IsMadmate()) type = CustomRoleTypes.Impostor;
        if (role.IsNeutral()) type = CustomRoleTypes.Neutral;
        if (role.IsCoven()) type = CustomRoleTypes.Coven;
        if (role.IsAdditionRole()) type = CustomRoleTypes.Addon;

        return type;
    }

    public static bool RoleExist(this CustomRoles role, bool countDead = false)
    {
        return Main.AllPlayerControls.Any(x => x.Is(role) && (countDead || x.IsAlive()));
    }

    public static int GetCount(this CustomRoles role)
    {
        return role.IsVanilla() ? 0 : Options.GetRoleCount(role);
    }

    public static int GetMode(this CustomRoles role)
    {
        return Options.GetRoleSpawnMode(role);
    }

    public static bool IsEnable(this CustomRoles role)
    {
        return role switch
        {
            CustomRoles.Nuker => Bomber.NukerChance.GetInt() > 0,
            CustomRoles.Bard => Arrogance.BardChance.GetInt() > 0,
            CustomRoles.Sunnyboy => Jester.SunnyboyChance.GetInt() > 0,
            _ => role.GetCount() > 0
        };
    }

    public static bool IsDevFavoriteRole(this CustomRoles role)
    {
        return role is CustomRoles.Adventurer or CustomRoles.Chef or CustomRoles.Detour or CustomRoles.Hacker or CustomRoles.Swapper or CustomRoles.Sentinel or CustomRoles.Sentry or CustomRoles.Tornado or CustomRoles.Tunneler or CustomRoles.Whisperer or CustomRoles.Wizard or
                       CustomRoles.Abyssbringer or CustomRoles.Assumer or CustomRoles.Chronomancer or CustomRoles.Echo or CustomRoles.Escapist or CustomRoles.Hypnotist or CustomRoles.Librarian or CustomRoles.Mafioso or CustomRoles.Mastermind or CustomRoles.Mercenary or CustomRoles.Penguin or CustomRoles.RiftMaker or CustomRoles.Sapper or CustomRoles.Wiper or
                       CustomRoles.Bargainer or CustomRoles.Bubble or CustomRoles.Doomsayer or CustomRoles.Enderman or CustomRoles.Evolver or CustomRoles.HeadHunter or CustomRoles.Impartial or CustomRoles.Patroller or CustomRoles.Pawn or CustomRoles.Infection or CustomRoles.Postman or CustomRoles.Pyromaniac or CustomRoles.Revolutionist or CustomRoles.Rogue or CustomRoles.Romantic or CustomRoles.RoomRusher or CustomRoles.RouleteGrandeur or CustomRoles.Simon or CustomRoles.SoulHunter or CustomRoles.Sprayer or CustomRoles.Technician or CustomRoles.Tank or CustomRoles.Tiger or CustomRoles.Tremor or CustomRoles.Vengeance or CustomRoles.Vortex or CustomRoles.Werewolf or CustomRoles.WeaponMaster or
                       CustomRoles.Reaper or CustomRoles.VoodooMaster or CustomRoles.Dreamweaver or CustomRoles.Banshee or
                       CustomRoles.Allergic or CustomRoles.Bloodlust or CustomRoles.Damocles or CustomRoles.Deadlined or CustomRoles.DeadlyQuota or CustomRoles.Dynamo or CustomRoles.DoubleShot or CustomRoles.Energetic or CustomRoles.Haste or CustomRoles.Introvert or CustomRoles.Messenger or CustomRoles.Nimble or CustomRoles.Reach or CustomRoles.Seer or CustomRoles.Stressed;
    }

    public static CountTypes GetCountTypes(this CustomRoles role)
    {
        return role switch
        {
            CustomRoles.GM => CountTypes.OutOfGame,
            CustomRoles.Sidekick when Jackal.SidekickCountMode.GetValue() == 0 => CountTypes.Jackal,
            CustomRoles.Sidekick when Jackal.SidekickCountMode.GetValue() == 1 => CountTypes.OutOfGame,
            CustomRoles.Deathknight => CountTypes.Necromancer,
            CustomRoles.Parasite => CountTypes.Impostor,
            CustomRoles.Hypocrite => CountTypes.Impostor,
            CustomRoles.Crewpostor => CountTypes.Impostor,
            CustomRoles.Renegade => CountTypes.Impostor,
            CustomRoles.Gaslighter => Gaslighter.WinCondition.GetValue() == 2 ? CountTypes.Gaslighter : CountTypes.Crew,
            CustomRoles.Stalker when Stalker.SnatchesWin.GetBool() => CountTypes.Crew,
            CustomRoles.SchrodingersCat => SchrodingersCat.WinsWithCrewIfNotAttacked.GetBool() ? CountTypes.Crew : CountTypes.OutOfGame,
            CustomRoles.Stalker => !Stalker.SnatchesWin.GetBool() ? CountTypes.Stalker : CountTypes.Crew,
            CustomRoles.Arsonist => Arsonist.ArsonistKeepsGameGoing.GetBool() ? CountTypes.Arsonist : CountTypes.Crew,
            CustomRoles.Shifter => CountTypes.OutOfGame,
            CustomRoles.NoteKiller when !NoteKiller.CountsAsNeutralKiller => CountTypes.Crew,
            CustomRoles.DoubleAgent => CountTypes.Crew,

            _ => Enum.TryParse(role.ToString(), true, out CountTypes type)
                ? type
                : role.Is(Team.Impostor) || role == CustomRoles.Trickster
                    ? CountTypes.Impostor
                    : role.IsCoven()
                        ? CountTypes.Coven
                        : CountTypes.Crew
        };
    }

    public static Color GetColor(this Team team)
    {
        return ColorUtility.TryParseHtmlString(team switch
        {
            Team.Coven => Main.CovenColor,
            Team.Crewmate => Main.CrewmateColor,
            Team.Neutral => Main.NeutralColor,
            Team.Impostor => Main.ImpostorColor,
            _ => string.Empty
        }, out Color color)
            ? color
            : Color.clear;
    }

    public static string GetTextColor(this Team team)
    {
        return team switch
        {
            Team.Coven => Main.CovenColor,
            Team.Crewmate => Main.CrewmateColor,
            Team.Neutral => Main.NeutralColor,
            Team.Impostor => Main.ImpostorColor,
            _ => string.Empty
        };
    }

    public static string ToColoredString(this CustomRoles role)
    {
        return Utils.ColorString(Utils.GetRoleColor(role), Translator.GetString($"{role}"));
    }

    #region OptionsAndCategories

    public static RoleOptionType GetRoleOptionType(this CustomRoles role)
    {
        if (role.IsCoven()) return RoleOptionType.Coven_Miscellaneous;
        if (role.IsImpostor() || role.IsMadmate()) return role.GetImpostorRoleCategory();
        if (role.IsCrewmate()) return role.GetCrewmateRoleCategory();
        if (role.IsNeutral(true)) return role.GetNeutralRoleCategory();

        return RoleOptionType.Crewmate_Miscellaneous;
    }

    public static Color GetRoleOptionTypeColor(this RoleOptionType type)
    {
        return type switch
        {
            RoleOptionType.Impostor_Killing => Utils.GetRoleColor(CustomRoles.Witness),
            RoleOptionType.Impostor_Support => Utils.GetRoleColor(CustomRoles.Bubble),
            RoleOptionType.Impostor_Concealing => Utils.GetRoleColor(CustomRoles.CopyCat),
            RoleOptionType.Impostor_Miscellaneous => Palette.ImpostorRed,
            RoleOptionType.Impostor_Madmate => Palette.ImpostorRed,
            RoleOptionType.Crewmate_Miscellaneous => Palette.CrewmateBlue,
            RoleOptionType.Crewmate_Investigate => Utils.GetRoleColor(CustomRoles.Forensic),
            RoleOptionType.Crewmate_Support => Utils.GetRoleColor(CustomRoles.NiceEraser),
            RoleOptionType.Crewmate_Killing => Utils.GetRoleColor(CustomRoles.Sheriff),
            RoleOptionType.Crewmate_Power => Utils.GetRoleColor(CustomRoles.Electric),
            RoleOptionType.Crewmate_Chaos => Utils.GetRoleColor(CustomRoles.Tornado),
            RoleOptionType.Neutral_Benign => Utils.GetRoleColor(CustomRoles.Chameleon),
            RoleOptionType.Neutral_Evil => Utils.GetRoleColor(CustomRoles.Vector),
            RoleOptionType.Neutral_Pariah => ColorUtility.TryParseHtmlString("#a10e49", out var c) ? c : Color.magenta,
            RoleOptionType.Neutral_Killing => Palette.ImpostorRed,
            RoleOptionType.Coven_Miscellaneous => Utils.GetRoleColor(CustomRoles.CovenLeader),
            _ => Utils.GetRoleColor(CustomRoles.Vigilante)
        };
    }

    public static TabGroup GetTabFromOptionType(this RoleOptionType type)
    {
        return type switch
        {
            RoleOptionType.Impostor_Killing => TabGroup.ImpostorRoles,
            RoleOptionType.Impostor_Support => TabGroup.ImpostorRoles,
            RoleOptionType.Impostor_Concealing => TabGroup.ImpostorRoles,
            RoleOptionType.Impostor_Miscellaneous => TabGroup.ImpostorRoles,
            RoleOptionType.Impostor_Madmate => TabGroup.ImpostorRoles,
            RoleOptionType.Crewmate_Miscellaneous => TabGroup.CrewmateRoles,
            RoleOptionType.Crewmate_Investigate => TabGroup.CrewmateRoles,
            RoleOptionType.Crewmate_Support => TabGroup.CrewmateRoles,
            RoleOptionType.Crewmate_Killing => TabGroup.CrewmateRoles,
            RoleOptionType.Crewmate_Power => TabGroup.CrewmateRoles,
            RoleOptionType.Crewmate_Chaos => TabGroup.CrewmateRoles,
            RoleOptionType.Neutral_Benign => TabGroup.NeutralRoles,
            RoleOptionType.Neutral_Evil => TabGroup.NeutralRoles,
            RoleOptionType.Neutral_Pariah => TabGroup.NeutralRoles,
            RoleOptionType.Neutral_Killing => TabGroup.NeutralRoles,
            RoleOptionType.Coven_Miscellaneous => TabGroup.CovenRoles,
            _ => TabGroup.OtherRoles
        };
    }

    public static Color GetAddonTypeColor(this AddonTypes type)
    {
        return type switch
        {
            AddonTypes.ImpOnly => Palette.ImpostorRed,
            AddonTypes.Helpful => Palette.CrewmateBlue,
            AddonTypes.Harmful => Utils.GetRoleColor(CustomRoles.Sprayer),
            AddonTypes.Mixed => Utils.GetRoleColor(CustomRoles.TaskManager),
            _ => Palette.CrewmateBlue
        };
    }

    public static RoleOptionType GetNeutralRoleCategory(this CustomRoles role)
    {
        return role switch
        {
            CustomRoles.NecroGuesser => RoleOptionType.Neutral_Benign,
            CustomRoles.Opportunist => RoleOptionType.Neutral_Benign,
            CustomRoles.Lawyer => RoleOptionType.Neutral_Benign,
            CustomRoles.Amnesiac => RoleOptionType.Neutral_Benign,
            CustomRoles.Pawn => RoleOptionType.Neutral_Benign,
            CustomRoles.Postman => RoleOptionType.Neutral_Benign,
            CustomRoles.Dealer => RoleOptionType.Neutral_Benign,
            CustomRoles.RoomRusher => RoleOptionType.Neutral_Benign,
            CustomRoles.SchrodingersCat => RoleOptionType.Neutral_Benign,
            CustomRoles.Predator => RoleOptionType.Neutral_Benign,
            CustomRoles.Pursuer => RoleOptionType.Neutral_Benign,
            CustomRoles.Hater => RoleOptionType.Neutral_Benign,
            CustomRoles.Revolutionist => RoleOptionType.Neutral_Benign,
            CustomRoles.Impartial => RoleOptionType.Neutral_Benign,
            CustomRoles.Investor => RoleOptionType.Neutral_Benign,
            CustomRoles.Backstabber => RoleOptionType.Neutral_Benign,
            CustomRoles.Shifter => RoleOptionType.Neutral_Benign,
            CustomRoles.Sunnyboy => RoleOptionType.Neutral_Benign,
            CustomRoles.Maverick => RoleOptionType.Neutral_Benign,
            CustomRoles.Romantic => RoleOptionType.Neutral_Benign,
            CustomRoles.VengefulRomantic => RoleOptionType.Neutral_Benign,
            CustomRoles.Specter => RoleOptionType.Neutral_Benign,
            CustomRoles.Provocateur => RoleOptionType.Neutral_Benign,
            CustomRoles.SoulHunter => RoleOptionType.Neutral_Benign,
            CustomRoles.Technician => RoleOptionType.Neutral_Benign,
            CustomRoles.Tank => RoleOptionType.Neutral_Benign,
            CustomRoles.Follower => RoleOptionType.Neutral_Benign,
            CustomRoles.Arsonist => RoleOptionType.Neutral_Evil,
            CustomRoles.Jester => RoleOptionType.Neutral_Evil,
            CustomRoles.Gaslighter => RoleOptionType.Neutral_Evil,
            CustomRoles.God => RoleOptionType.Neutral_Evil,
            CustomRoles.Executioner => RoleOptionType.Neutral_Evil,
            CustomRoles.Doomsayer => RoleOptionType.Neutral_Evil,
            CustomRoles.Vector => RoleOptionType.Neutral_Evil,
            CustomRoles.Terrorist => RoleOptionType.Neutral_Evil,
            CustomRoles.Thief => RoleOptionType.Neutral_Evil,
            CustomRoles.Collector => RoleOptionType.Neutral_Evil,
            CustomRoles.Vulture => RoleOptionType.Neutral_Evil,
            CustomRoles.Workaholic => RoleOptionType.Neutral_Evil,
            CustomRoles.Deathknight => RoleOptionType.Neutral_Evil,
            CustomRoles.Innocent => RoleOptionType.Neutral_Evil,
            CustomRoles.Curser => RoleOptionType.Neutral_Pariah,
            CustomRoles.Auditor => RoleOptionType.Neutral_Pariah,
            CustomRoles.Clerk => RoleOptionType.Neutral_Pariah,
            CustomRoles.Magistrate => RoleOptionType.Neutral_Pariah,
            CustomRoles.Seamstress => RoleOptionType.Neutral_Pariah,
            CustomRoles.Spirit => RoleOptionType.Neutral_Pariah,
            CustomRoles.Starspawn => RoleOptionType.Neutral_Pariah,
            _ => role.IsNK(true) ? RoleOptionType.Neutral_Killing : role.IsImpostor() ? RoleOptionType.Impostor_Miscellaneous : RoleOptionType.Crewmate_Miscellaneous
        };
    }

    public static RoleOptionType GetImpostorRoleCategory(this CustomRoles role)
    {
        return role switch
        {
            CustomRoles.Arrogance => RoleOptionType.Impostor_Killing,
            CustomRoles.Abyssbringer => RoleOptionType.Impostor_Killing,
            CustomRoles.Ninja => RoleOptionType.Impostor_Killing,
            CustomRoles.Assumer => RoleOptionType.Impostor_Killing,
            CustomRoles.Augmenter => RoleOptionType.Impostor_Killing,
            CustomRoles.Bomber => RoleOptionType.Impostor_Killing,
            CustomRoles.BountyHunter => RoleOptionType.Impostor_Killing,
            CustomRoles.Cantankerous => RoleOptionType.Impostor_Killing,
            CustomRoles.Chronomancer => RoleOptionType.Impostor_Killing,
            CustomRoles.Councillor => RoleOptionType.Impostor_Killing,
            CustomRoles.EvilGuesser => RoleOptionType.Impostor_Killing,
            CustomRoles.Fireworker => RoleOptionType.Impostor_Killing,
            CustomRoles.Greedy => RoleOptionType.Impostor_Killing,
            CustomRoles.Hitman => RoleOptionType.Impostor_Killing,
            CustomRoles.Inhibitor => RoleOptionType.Impostor_Killing,
            CustomRoles.Kamikaze => RoleOptionType.Impostor_Killing,
            CustomRoles.KillingMachine => RoleOptionType.Impostor_Killing,
            CustomRoles.Lurker => RoleOptionType.Impostor_Killing,
            CustomRoles.Mafioso => RoleOptionType.Impostor_Killing,
            CustomRoles.Nemesis => RoleOptionType.Impostor_Killing,
            CustomRoles.Mercenary => RoleOptionType.Impostor_Killing,
            CustomRoles.Nuker => RoleOptionType.Impostor_Killing,
            CustomRoles.Overheat => RoleOptionType.Impostor_Killing,
            CustomRoles.Butcher => RoleOptionType.Impostor_Killing,
            CustomRoles.QuickShooter => RoleOptionType.Impostor_Killing,
            CustomRoles.Psychopath => RoleOptionType.Impostor_Killing,
            CustomRoles.Saboteur => RoleOptionType.Impostor_Killing,
            CustomRoles.Sapper => RoleOptionType.Impostor_Killing,
            CustomRoles.Sniper => RoleOptionType.Impostor_Killing,
            CustomRoles.Trapster => RoleOptionType.Impostor_Killing,
            CustomRoles.Wasp => RoleOptionType.Impostor_Killing,
            CustomRoles.Wiper => RoleOptionType.Impostor_Killing,
            CustomRoles.Witch => RoleOptionType.Impostor_Killing,
            CustomRoles.Zombie => RoleOptionType.Impostor_Killing,
            CustomRoles.Bard => RoleOptionType.Impostor_Support,
            CustomRoles.Blackmailer => RoleOptionType.Impostor_Support,
            CustomRoles.Camouflager => RoleOptionType.Impostor_Support,
            CustomRoles.Capitalist => RoleOptionType.Impostor_Support,
            CustomRoles.Catalyst => RoleOptionType.Impostor_Support,
            CustomRoles.Centralizer => RoleOptionType.Impostor_Support,
            CustomRoles.Cleaner => RoleOptionType.Impostor_Support,
            CustomRoles.ClockBlocker => RoleOptionType.Impostor_Support,
            CustomRoles.Commander => RoleOptionType.Impostor_Support,
            CustomRoles.Consort => RoleOptionType.Impostor_Support,
            CustomRoles.Framer => RoleOptionType.Impostor_Support,
            CustomRoles.Deathpact => RoleOptionType.Impostor_Support,
            CustomRoles.Devourer => RoleOptionType.Impostor_Support,
            CustomRoles.Disperser => RoleOptionType.Impostor_Support,
            CustomRoles.Dazzler => RoleOptionType.Impostor_Support,
            CustomRoles.EvilEraser => RoleOptionType.Impostor_Support,
            CustomRoles.Freezer => RoleOptionType.Impostor_Support,
            CustomRoles.Gangster => RoleOptionType.Impostor_Support,
            CustomRoles.Godfather => RoleOptionType.Impostor_Support,
            CustomRoles.Hypnotist => RoleOptionType.Impostor_Support,
            CustomRoles.Librarian => RoleOptionType.Impostor_Support,
            CustomRoles.Nullifier => RoleOptionType.Impostor_Support,
            CustomRoles.Occultist => RoleOptionType.Impostor_Support,
            CustomRoles.Silencer => RoleOptionType.Impostor_Support,
            CustomRoles.Swapster => RoleOptionType.Impostor_Support,
            CustomRoles.TimeThief => RoleOptionType.Impostor_Support,
            CustomRoles.Twister => RoleOptionType.Impostor_Support,
            CustomRoles.Ventriloquist => RoleOptionType.Impostor_Support,
            CustomRoles.Vindicator => RoleOptionType.Impostor_Support,
            CustomRoles.YinYanger => RoleOptionType.Impostor_Support,
            CustomRoles.Ambusher => RoleOptionType.Impostor_Concealing,
            CustomRoles.Anonymous => RoleOptionType.Impostor_Concealing,
            CustomRoles.Duellist => RoleOptionType.Impostor_Concealing,
            CustomRoles.Echo => RoleOptionType.Impostor_Concealing,
            CustomRoles.Escapist => RoleOptionType.Impostor_Concealing,
            CustomRoles.Exclusionary => RoleOptionType.Impostor_Concealing,
            CustomRoles.Fabricator => RoleOptionType.Impostor_Concealing,
            CustomRoles.Forger => RoleOptionType.Impostor_Concealing,
            CustomRoles.Hangman => RoleOptionType.Impostor_Concealing,
            CustomRoles.Kidnapper => RoleOptionType.Impostor_Concealing,
            CustomRoles.Lightning => RoleOptionType.Impostor_Concealing,
            CustomRoles.Mastermind => RoleOptionType.Impostor_Concealing,
            CustomRoles.Miner => RoleOptionType.Impostor_Concealing,
            CustomRoles.Morphling => RoleOptionType.Impostor_Concealing,
            CustomRoles.Penguin => RoleOptionType.Impostor_Concealing,
            CustomRoles.Postponer => RoleOptionType.Impostor_Concealing,
            CustomRoles.Puppeteer => RoleOptionType.Impostor_Concealing,
            CustomRoles.RiftMaker => RoleOptionType.Impostor_Concealing,
            CustomRoles.Scavenger => RoleOptionType.Impostor_Concealing,
            CustomRoles.SoulCatcher => RoleOptionType.Impostor_Concealing,
            CustomRoles.Stasis => RoleOptionType.Impostor_Concealing,
            CustomRoles.Swiftclaw => RoleOptionType.Impostor_Concealing,
            CustomRoles.Swooper => RoleOptionType.Impostor_Concealing,
            CustomRoles.Stealth => RoleOptionType.Impostor_Concealing,
            CustomRoles.Trickster => RoleOptionType.Impostor_Concealing,
            CustomRoles.Undertaker => RoleOptionType.Impostor_Concealing,
            CustomRoles.Vampire => RoleOptionType.Impostor_Concealing,
            CustomRoles.Venerer => RoleOptionType.Impostor_Concealing,
            CustomRoles.Warlock => RoleOptionType.Impostor_Concealing,
            CustomRoles.AntiAdminer => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.Changeling => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.Consigliere => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.CursedWolf => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.EvilTracker => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.Gambler => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.Generator => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.Loner => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.Visionary => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.Wildling => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.Impostor => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.ImpostorEHR => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.Shapeshifter => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.ShapeshifterEHR => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.Phantom => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.PhantomEHR => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.Viper => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.ViperEHR => RoleOptionType.Impostor_Miscellaneous,
            CustomRoles.Parasite => RoleOptionType.Impostor_Madmate,
            CustomRoles.Hypocrite => RoleOptionType.Impostor_Madmate,
            CustomRoles.Crewpostor => RoleOptionType.Impostor_Madmate,
            CustomRoles.Renegade => RoleOptionType.Impostor_Madmate,
            _ => role.IsCrewmate() ? RoleOptionType.Crewmate_Miscellaneous : RoleOptionType.Neutral_Benign
        };
    }

    public static RoleOptionType GetCrewmateRoleCategory(this CustomRoles role)
    {
        return role switch
        {
            CustomRoles.Crewmate => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.CrewmateEHR => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Engineer => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.EngineerEHR => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Scientist => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.ScientistEHR => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Tracker => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.TrackerEHR => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Noisemaker => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.NoisemakerEHR => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Detective => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.DetectiveEHR => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Addict => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.CameraMan => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Carrier => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Demolitionist => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Express => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.LazyGuy => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Journalist => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Luckey => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Mole => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Nightmare => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Paranoid => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.PortalMaker => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Ricochet => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Safeguard => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.SuperStar => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Tether => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Transmitter => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Tracefinder => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Tunneler => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Vacuum => RoleOptionType.Crewmate_Miscellaneous,
            CustomRoles.Analyst => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Captain => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Catcher => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Chameleon => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Clairvoyant => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Coroner => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Forensic => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Doctor => RoleOptionType.Crewmate_Investigate,
            CustomRoles.DoubleAgent => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Druid => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Enigma => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Investigator => RoleOptionType.Crewmate_Investigate,
            CustomRoles.FortuneTeller => RoleOptionType.Crewmate_Investigate,
            CustomRoles.MeetingManager => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Ignitor => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Imitator => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Inquirer => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Inquisitor => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Insight => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Inspector => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Leery => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Lighter => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Lookout => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Markseeker => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Medium => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Telecommunication => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Mortician => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Hacker => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Observer => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Oracle => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Perceiver => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Psychic => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Rabbit => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Scout => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Sensor => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Sentry => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Shiftguard => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Snitch => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Socialite => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Soothsayer => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Spiritualist => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Spy => RoleOptionType.Crewmate_Investigate,
            CustomRoles.TaskManager => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Whisperer => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Witness => RoleOptionType.Crewmate_Investigate,
            CustomRoles.Aid => RoleOptionType.Crewmate_Support,
            CustomRoles.Altruist => RoleOptionType.Crewmate_Support,
            CustomRoles.Autocrat => RoleOptionType.Crewmate_Support,
            CustomRoles.Bane => RoleOptionType.Crewmate_Support,
            CustomRoles.Battery => RoleOptionType.Crewmate_Support,
            CustomRoles.Beacon => RoleOptionType.Crewmate_Support,
            CustomRoles.Benefactor => RoleOptionType.Crewmate_Support,
            CustomRoles.Bestower => RoleOptionType.Crewmate_Support,
            CustomRoles.Bodyguard => RoleOptionType.Crewmate_Support,
            CustomRoles.Chef => RoleOptionType.Crewmate_Support,
            CustomRoles.Cleanser => RoleOptionType.Crewmate_Support,
            CustomRoles.Convener => RoleOptionType.Crewmate_Support,
            CustomRoles.Crusader => RoleOptionType.Crewmate_Support,
            CustomRoles.Deputy => RoleOptionType.Crewmate_Support,
            CustomRoles.DonutDelivery => RoleOptionType.Crewmate_Support,
            CustomRoles.Doorjammer => RoleOptionType.Crewmate_Support,
            CustomRoles.Pacifist => RoleOptionType.Crewmate_Support,
            CustomRoles.Electric => RoleOptionType.Crewmate_Support,
            CustomRoles.Escort => RoleOptionType.Crewmate_Support,
            CustomRoles.Farmer => RoleOptionType.Crewmate_Support,
            CustomRoles.Gardener => RoleOptionType.Crewmate_Support,
            CustomRoles.Gaulois => RoleOptionType.Crewmate_Support,
            CustomRoles.Grappler => RoleOptionType.Crewmate_Support,
            CustomRoles.Grenadier => RoleOptionType.Crewmate_Support,
            CustomRoles.Helper => RoleOptionType.Crewmate_Support,
            CustomRoles.Jailor => RoleOptionType.Crewmate_Support,
            CustomRoles.Mathematician => RoleOptionType.Crewmate_Support,
            CustomRoles.Mechanic => RoleOptionType.Crewmate_Support,
            CustomRoles.Medic => RoleOptionType.Crewmate_Support,
            CustomRoles.Merchant => RoleOptionType.Crewmate_Support,
            CustomRoles.Monarch => RoleOptionType.Crewmate_Support,
            CustomRoles.Negotiator => RoleOptionType.Crewmate_Support,
            CustomRoles.Rhapsode => RoleOptionType.Crewmate_Support,
            CustomRoles.SecurityGuard => RoleOptionType.Crewmate_Support,
            CustomRoles.SpeedBooster => RoleOptionType.Crewmate_Support,
            CustomRoles.TimeManager => RoleOptionType.Crewmate_Support,
            CustomRoles.TimeMaster => RoleOptionType.Crewmate_Support,
            CustomRoles.Transporter => RoleOptionType.Crewmate_Support,
            CustomRoles.Ventguard => RoleOptionType.Crewmate_Support,
            CustomRoles.Wizard => RoleOptionType.Crewmate_Support,
            CustomRoles.Drainer => RoleOptionType.Crewmate_Killing,
            CustomRoles.Judge => RoleOptionType.Crewmate_Killing,
            CustomRoles.NiceGuesser => RoleOptionType.Crewmate_Killing,
            CustomRoles.Retributionist => RoleOptionType.Crewmate_Killing,
            CustomRoles.Sentinel => RoleOptionType.Crewmate_Killing,
            CustomRoles.Sheriff => RoleOptionType.Crewmate_Killing,
            CustomRoles.Veteran => RoleOptionType.Crewmate_Killing,
            CustomRoles.Vigilante => RoleOptionType.Crewmate_Killing,
            CustomRoles.Adrenaline => RoleOptionType.Crewmate_Power,
            CustomRoles.Adventurer => RoleOptionType.Crewmate_Power,
            CustomRoles.Alchemist => RoleOptionType.Crewmate_Power,
            CustomRoles.Astral => RoleOptionType.Crewmate_Power,
            CustomRoles.CopyCat => RoleOptionType.Crewmate_Power,
            CustomRoles.Detour => RoleOptionType.Crewmate_Power,
            CustomRoles.Dictator => RoleOptionType.Crewmate_Power,
            CustomRoles.Guardian => RoleOptionType.Crewmate_Power,
            CustomRoles.Decryptor => RoleOptionType.Crewmate_Power,
            CustomRoles.Marshall => RoleOptionType.Crewmate_Power,
            CustomRoles.Mayor => RoleOptionType.Crewmate_Power,
            CustomRoles.NiceEraser => RoleOptionType.Crewmate_Power,
            CustomRoles.Swapper => RoleOptionType.Crewmate_Power,
            CustomRoles.Oxyman => RoleOptionType.Crewmate_Power,
            CustomRoles.President => RoleOptionType.Crewmate_Power,
            CustomRoles.Speedrunner => RoleOptionType.Crewmate_Power,
            CustomRoles.Telekinetic => RoleOptionType.Crewmate_Power,
            CustomRoles.Ankylosaurus => RoleOptionType.Crewmate_Chaos,
            CustomRoles.Car => RoleOptionType.Crewmate_Chaos,
            CustomRoles.Dad => RoleOptionType.Crewmate_Chaos,
            CustomRoles.Goose => RoleOptionType.Crewmate_Chaos,
            CustomRoles.Randomizer => RoleOptionType.Crewmate_Chaos,
            CustomRoles.ToiletMaster => RoleOptionType.Crewmate_Chaos,
            CustomRoles.Tornado => RoleOptionType.Crewmate_Chaos,
            CustomRoles.Tree => RoleOptionType.Crewmate_Chaos,
            _ => role.IsImpostor() ? RoleOptionType.Impostor_Miscellaneous : RoleOptionType.Neutral_Benign
        };
    }

    #endregion
}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum RoleOptionType
{
    Impostor_Killing,
    Impostor_Support,
    Impostor_Concealing,
    Impostor_Miscellaneous,
    Impostor_Madmate,
    Crewmate_Miscellaneous,
    Crewmate_Investigate,
    Crewmate_Support,
    Crewmate_Killing,
    Crewmate_Power,
    Crewmate_Chaos,
    Neutral_Benign,
    Neutral_Evil,
    Neutral_Pariah,
    Neutral_Killing,
    Coven_Miscellaneous
}

public enum AddonTypes
{
    ImpOnly,
    Helpful,
    Harmful,
    Mixed
}

public enum CustomRoleTypes
{
    Crewmate,
    Impostor,
    Neutral,
    Coven,
    Addon
}

[SuppressMessage("ReSharper", "UnusedMember.Global")]
public enum CountTypes
{
    OutOfGame,
    None,
    Crew,
    Impostor,
    CustomTeam,
    Bloodlust,
    Jackal,
    Doppelganger,
    Pelican,
    Demon,
    BloodKnight,
    Poisoner,
    Cultist,
    Necromancer,
    HexMaster,
    Wraith,
    SerialKiller,
    Quarry,
    Accumulator,
    Spider,
    SoulCollector,
    Berserker,
    Sharpshooter,
    Explosivist,
    Thanos,
    Duality,
    Slenderman,
    Amogus,
    Weatherman,
    NoteKiller,
    Vortex,
    Beehive,
    RouleteGrandeur,
    Nonplus,
    Tremor,
    Evolver,
    Rogue,
    Patroller,
    Simon,
    Chemist,
    QuizMaster,
    Samurai,
    Bargainer,
    Tiger,
    Enderman,
    Mycologist,
    Bubble,
    Hookshot,
    Sprayer,
    Infection,
    Magician,
    WeaponMaster,
    Reckless,
    Pyromaniac,
    Eclipse,
    Vengeance,
    HeadHunter,
    Gaslighter,
    Pulse,
    Werewolf,
    Juggernaut,
    Agitator,
    Virus,
    Stalker,
    Jinx,
    Ritualist,
    Pickpocket,
    Traitor,
    Medusa,
    Bandit,
    Spiritcaller,
    RuthlessRomantic,
    Pestilence,
    PlagueBearer,
    Glitch,
    Arsonist,
    Cherokious,

    Coven
}



