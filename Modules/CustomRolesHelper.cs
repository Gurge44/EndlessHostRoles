using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;

namespace TOHE;

internal static class CustomRolesHelper
{
    public static CustomRoles GetVNRole(this CustomRoles role)
    {
        bool UsePets = Options.UsePets.GetBool();
        return role.IsVanilla()
            ? role
            : role switch
            {
                CustomRoles.Sniper => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
                CustomRoles.Jester => Options.JesterCanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
                CustomRoles.Mayor => Options.MayorHasPortableButton.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
                CustomRoles.Monitor => Monitor.CanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
                CustomRoles.Vulture => Vulture.CanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
                CustomRoles.Opportunist => CustomRoles.Engineer,
                CustomRoles.Vindicator => CustomRoles.Impostor,
                CustomRoles.Snitch => CustomRoles.Crewmate,
                //CustomRoles.Masochist => CustomRoles.Crewmate,
                CustomRoles.ParityCop => CustomRoles.Crewmate,
                CustomRoles.Marshall => CustomRoles.Crewmate,
                CustomRoles.SabotageMaster => CustomRoles.Engineer,
                CustomRoles.Mafia => Options.LegacyMafia.GetBool() ? CustomRoles.Shapeshifter : CustomRoles.Impostor,
                CustomRoles.Terrorist => CustomRoles.Engineer,
                CustomRoles.Executioner => CustomRoles.Crewmate,
                CustomRoles.Lawyer => CustomRoles.Crewmate,
                CustomRoles.NiceSwapper => CustomRoles.Crewmate,
                CustomRoles.Ignitor => CustomRoles.Crewmate,
                CustomRoles.Jailor => CustomRoles.Impostor,
                CustomRoles.Vampire => CustomRoles.Impostor,
                CustomRoles.BountyHunter => CustomRoles.Impostor,
                CustomRoles.Trickster => CustomRoles.Impostor,
                CustomRoles.Witch => CustomRoles.Impostor,
                //CustomRoles.ShapeMaster => CustomRoles.Shapeshifter,
                CustomRoles.ShapeshifterTOHE => CustomRoles.Shapeshifter,
                CustomRoles.Agitater => CustomRoles.Impostor,
                CustomRoles.ImpostorTOHE => CustomRoles.Impostor,
                CustomRoles.EvilDiviner => CustomRoles.Impostor,
                CustomRoles.Wildling => Wildling.CanShapeshift.GetBool() ? CustomRoles.Shapeshifter : CustomRoles.Impostor,
                CustomRoles.Morphling => CustomRoles.Shapeshifter,
                CustomRoles.Warlock => CustomRoles.Impostor,
                CustomRoles.SerialKiller => CustomRoles.Impostor,
                CustomRoles.FireWorks => CustomRoles.Shapeshifter,
                CustomRoles.SpeedBooster => CustomRoles.Crewmate,
                CustomRoles.Dictator => CustomRoles.Crewmate,
                CustomRoles.Inhibitor => CustomRoles.Impostor,
                CustomRoles.Kidnapper => CustomRoles.Shapeshifter,
                CustomRoles.Swapster => CustomRoles.Shapeshifter,
                CustomRoles.Kamikaze => CustomRoles.Impostor,
                CustomRoles.Librarian => CustomRoles.Shapeshifter,
                CustomRoles.Cantankerous => CustomRoles.Impostor,
                CustomRoles.Swiftclaw => CustomRoles.Impostor,
                CustomRoles.YinYanger => CustomRoles.Impostor,
                CustomRoles.Duellist => CustomRoles.Shapeshifter,
                CustomRoles.Consort => CustomRoles.Impostor,
                CustomRoles.Mafioso => CustomRoles.Impostor,
                CustomRoles.Chronomancer => CustomRoles.Impostor,
                CustomRoles.Nullifier => CustomRoles.Impostor,
                CustomRoles.Stealth => CustomRoles.Impostor,
                CustomRoles.Penguin => CustomRoles.Shapeshifter,
                CustomRoles.Sapper => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
                CustomRoles.Mastermind => CustomRoles.Impostor,
                CustomRoles.RiftMaker => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
                CustomRoles.Gambler => CustomRoles.Impostor,
                CustomRoles.Hitman => CustomRoles.Shapeshifter,
                CustomRoles.Saboteur => CustomRoles.Impostor,
                CustomRoles.Doctor => CustomRoles.Scientist,
                CustomRoles.ScientistTOHE => CustomRoles.Scientist,
                CustomRoles.Tracefinder => CustomRoles.Scientist,
                CustomRoles.Puppeteer => CustomRoles.Impostor,
                CustomRoles.TimeThief => CustomRoles.Impostor,
                CustomRoles.EvilTracker => CustomRoles.Shapeshifter,
                CustomRoles.Paranoia => CustomRoles.Engineer,
                CustomRoles.EngineerTOHE => CustomRoles.Engineer,
                CustomRoles.TimeMaster => CustomRoles.Engineer,
                CustomRoles.CrewmateTOHE => CustomRoles.Crewmate,
                CustomRoles.Cleanser => CustomRoles.Crewmate,
                CustomRoles.Miner => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
                CustomRoles.Psychic => CustomRoles.Crewmate,
                CustomRoles.Needy => CustomRoles.Crewmate,
                CustomRoles.Twister => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
                CustomRoles.SuperStar => CustomRoles.Crewmate,
                CustomRoles.Hacker => CustomRoles.Shapeshifter,
                CustomRoles.Visionary => CustomRoles.Impostor,
                CustomRoles.Assassin => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
                CustomRoles.Undertaker => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
                CustomRoles.Luckey => CustomRoles.Crewmate,
                CustomRoles.CyberStar => CustomRoles.Crewmate,
                CustomRoles.Demolitionist => CustomRoles.Crewmate,
                CustomRoles.Ventguard => CustomRoles.Engineer,
                CustomRoles.Express => CustomRoles.Crewmate,
                CustomRoles.NiceEraser => CustomRoles.Crewmate,
                CustomRoles.TaskManager => CustomRoles.Crewmate,
                CustomRoles.Rabbit => CustomRoles.Crewmate,
                CustomRoles.Shiftguard => CustomRoles.Crewmate,
                CustomRoles.Mole => CustomRoles.Engineer,
                CustomRoles.Sentinel => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
                CustomRoles.Electric => CustomRoles.Crewmate,
                CustomRoles.Philantropist => CustomRoles.Crewmate,
                CustomRoles.Tornado => CustomRoles.Crewmate,
                CustomRoles.Druid => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
                CustomRoles.Insight => CustomRoles.Crewmate,
                CustomRoles.Tunneler => CustomRoles.Crewmate,
                CustomRoles.Detour => CustomRoles.Crewmate,
                CustomRoles.Drainer => CustomRoles.Engineer,
                CustomRoles.Benefactor => CustomRoles.Crewmate,
                CustomRoles.GuessManager => CustomRoles.Crewmate,
                CustomRoles.Altruist => CustomRoles.Crewmate,
                CustomRoles.Transmitter => CustomRoles.Crewmate,
                CustomRoles.Autocrat => CustomRoles.Crewmate,
                CustomRoles.Perceiver => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
                CustomRoles.Convener => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
                CustomRoles.Mathematician => CustomRoles.Crewmate,
                CustomRoles.Nightmare => CustomRoles.Crewmate,
                CustomRoles.CameraMan => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
                CustomRoles.Spy => CustomRoles.Crewmate,
                CustomRoles.Ricochet => CustomRoles.Crewmate,
                CustomRoles.Tether => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
                CustomRoles.Doormaster => UsePets ? CustomRoles.Crewmate : CustomRoles.Engineer,
                CustomRoles.Aid => UsePets && Aid.UsePet.GetBool() ? CustomRoles.Crewmate : CustomRoles.Impostor,
                CustomRoles.Escort => UsePets && Escort.UsePet.GetBool() ? CustomRoles.Crewmate : CustomRoles.Impostor,
                CustomRoles.DonutDelivery => UsePets && DonutDelivery.UsePet.GetBool() ? CustomRoles.Crewmate : CustomRoles.Impostor,
                CustomRoles.Gaulois => UsePets && Gaulois.UsePet.GetBool() ? CustomRoles.Crewmate : CustomRoles.Impostor,
                CustomRoles.Analyzer => UsePets && Analyzer.UsePet.GetBool() ? CustomRoles.Crewmate : CustomRoles.Impostor,
                CustomRoles.Escapee => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
                CustomRoles.NiceGuesser => CustomRoles.Crewmate,
                CustomRoles.EvilGuesser => CustomRoles.Impostor,
                CustomRoles.Detective => CustomRoles.Crewmate,
                CustomRoles.God => CustomRoles.Crewmate,
                CustomRoles.GuardianAngelTOHE => CustomRoles.GuardianAngel,
                CustomRoles.Zombie => CustomRoles.Impostor,
                CustomRoles.Mario => CustomRoles.Engineer,
                CustomRoles.AntiAdminer => CustomRoles.Impostor,
                CustomRoles.Sans => CustomRoles.Impostor,
                CustomRoles.Bomber => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
                CustomRoles.Nuker => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
                CustomRoles.BoobyTrap => CustomRoles.Impostor,
                CustomRoles.Scavenger => CustomRoles.Impostor,
                CustomRoles.Transporter => CustomRoles.Crewmate,
                CustomRoles.Veteran => CustomRoles.Engineer,
                CustomRoles.Capitalism => CustomRoles.Impostor,
                CustomRoles.Bodyguard => CustomRoles.Crewmate,
                CustomRoles.Grenadier => CustomRoles.Engineer,
                CustomRoles.Lighter => CustomRoles.Engineer,
                CustomRoles.SecurityGuard => CustomRoles.Engineer,
                CustomRoles.Gangster => CustomRoles.Impostor,
                CustomRoles.Cleaner => CustomRoles.Impostor,
                CustomRoles.Konan => CustomRoles.Crewmate,
                CustomRoles.Divinator => CustomRoles.Crewmate,
                CustomRoles.Oracle => CustomRoles.Crewmate,
                CustomRoles.BallLightning => CustomRoles.Impostor,
                CustomRoles.Greedier => CustomRoles.Impostor,
                CustomRoles.Workaholic => CustomRoles.Engineer,
                CustomRoles.Speedrunner => CustomRoles.Crewmate,
                CustomRoles.CursedWolf => CustomRoles.Impostor,
                CustomRoles.Collector => CustomRoles.Crewmate,
                CustomRoles.ImperiusCurse => CustomRoles.Shapeshifter,
                CustomRoles.QuickShooter => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
                CustomRoles.Eraser => CustomRoles.Impostor,
                CustomRoles.OverKiller => CustomRoles.Impostor,
                CustomRoles.Hangman => CustomRoles.Shapeshifter,
                CustomRoles.Sunnyboy => CustomRoles.Scientist,
                CustomRoles.Phantom => Options.PhantomCanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
                CustomRoles.Judge => CustomRoles.Crewmate,
                CustomRoles.Councillor => CustomRoles.Impostor,
                CustomRoles.Mortician => CustomRoles.Crewmate,
                CustomRoles.Mediumshiper => CustomRoles.Crewmate,
                CustomRoles.Bard => CustomRoles.Impostor,
                CustomRoles.Swooper => CustomRoles.Impostor,
                CustomRoles.Crewpostor => CustomRoles.Engineer,
                CustomRoles.Observer => CustomRoles.Crewmate,
                CustomRoles.DovesOfNeace => CustomRoles.Engineer,
                CustomRoles.Disperser => UsePets ? CustomRoles.Impostor : CustomRoles.Shapeshifter,
                CustomRoles.Camouflager => CustomRoles.Shapeshifter,
                CustomRoles.Dazzler => CustomRoles.Shapeshifter,
                CustomRoles.Devourer => CustomRoles.Shapeshifter,
                CustomRoles.Deathpact => CustomRoles.Shapeshifter,
                CustomRoles.Bloodhound => CustomRoles.Crewmate,
                CustomRoles.Tracker => CustomRoles.Crewmate,
                CustomRoles.Merchant => CustomRoles.Crewmate,
                CustomRoles.Lookout => CustomRoles.Crewmate,
                //CustomRoles.Retributionist => CustomRoles.Crewmate,
                CustomRoles.Guardian => CustomRoles.Crewmate,
                CustomRoles.Enigma => CustomRoles.Crewmate,
                CustomRoles.Addict => CustomRoles.Engineer,
                CustomRoles.Alchemist => CustomRoles.Engineer,
                CustomRoles.Chameleon => CustomRoles.Engineer,
                CustomRoles.EvilSpirit => CustomRoles.GuardianAngel,
                CustomRoles.Lurker => CustomRoles.Impostor,
                CustomRoles.Doomsayer => CustomRoles.Crewmate,
                CustomRoles.Godfather => CustomRoles.Impostor,
                CustomRoles.Blackmailer => CustomRoles.Shapeshifter,

                _ => role.IsImpostor() ? CustomRoles.Impostor : CustomRoles.Crewmate,
            };
    }

    public static CustomRoles GetErasedRole(this CustomRoles role) => role.IsVanilla()
        ? role
        : role switch
        {
            CustomRoles.Tracker => CustomRoles.CrewmateTOHE,
            CustomRoles.Mayor => Options.MayorHasPortableButton.GetBool() ? CustomRoles.EngineerTOHE : CustomRoles.CrewmateTOHE,
            CustomRoles.Observer => CustomRoles.CrewmateTOHE,
            CustomRoles.DovesOfNeace => CustomRoles.EngineerTOHE,
            CustomRoles.Monitor => Monitor.CanVent.GetBool() ? CustomRoles.EngineerTOHE : CustomRoles.CrewmateTOHE,
            CustomRoles.Judge => CustomRoles.CrewmateTOHE,
            CustomRoles.NiceSwapper => CustomRoles.CrewmateTOHE,
            CustomRoles.Mortician => CustomRoles.CrewmateTOHE,
            CustomRoles.Mediumshiper => CustomRoles.CrewmateTOHE,
            CustomRoles.Bodyguard => CustomRoles.CrewmateTOHE,
            CustomRoles.ParityCop => CustomRoles.CrewmateTOHE,
            CustomRoles.Enigma => CustomRoles.CrewmateTOHE,
            CustomRoles.Grenadier => CustomRoles.EngineerTOHE,
            CustomRoles.SecurityGuard => CustomRoles.EngineerTOHE,
            CustomRoles.Lighter => CustomRoles.EngineerTOHE,
            CustomRoles.Ignitor => CustomRoles.EngineerTOHE,
            CustomRoles.Speedrunner => CustomRoles.CrewmateTOHE,
            CustomRoles.Transporter => CustomRoles.CrewmateTOHE,
            CustomRoles.Veteran => CustomRoles.CrewmateTOHE,
            CustomRoles.GuardianAngelTOHE => CustomRoles.GuardianAngel,
            CustomRoles.Detective => CustomRoles.CrewmateTOHE,
            CustomRoles.NiceGuesser => CustomRoles.CrewmateTOHE,
            CustomRoles.Cleanser => CustomRoles.CrewmateTOHE,
            CustomRoles.Luckey => CustomRoles.CrewmateTOHE,
            CustomRoles.Lookout => CustomRoles.CrewmateTOHE,
            CustomRoles.CyberStar => CustomRoles.CrewmateTOHE,
            CustomRoles.Demolitionist => CustomRoles.CrewmateTOHE,
            CustomRoles.Ventguard => CustomRoles.EngineerTOHE,
            CustomRoles.Express => CustomRoles.CrewmateTOHE,
            CustomRoles.NiceEraser => CustomRoles.CrewmateTOHE,
            CustomRoles.TaskManager => CustomRoles.CrewmateTOHE,
            CustomRoles.Rabbit => CustomRoles.CrewmateTOHE,
            CustomRoles.Shiftguard => CustomRoles.CrewmateTOHE,
            CustomRoles.Mole => CustomRoles.EngineerTOHE,
            CustomRoles.Sentinel => CustomRoles.EngineerTOHE,
            CustomRoles.Electric => CustomRoles.CrewmateTOHE,
            CustomRoles.Philantropist => CustomRoles.CrewmateTOHE,
            CustomRoles.Tornado => CustomRoles.CrewmateTOHE,
            CustomRoles.Druid => CustomRoles.EngineerTOHE,
            CustomRoles.Insight => CustomRoles.CrewmateTOHE,
            CustomRoles.Tunneler => CustomRoles.CrewmateTOHE,
            CustomRoles.Detour => CustomRoles.CrewmateTOHE,
            CustomRoles.Drainer => CustomRoles.EngineerTOHE,
            CustomRoles.GuessManager => CustomRoles.CrewmateTOHE,
            CustomRoles.Benefactor => CustomRoles.CrewmateTOHE,
            CustomRoles.Altruist => CustomRoles.CrewmateTOHE,
            CustomRoles.Transmitter => CustomRoles.CrewmateTOHE,
            CustomRoles.Autocrat => CustomRoles.CrewmateTOHE,
            CustomRoles.Mathematician => CustomRoles.CrewmateTOHE,
            CustomRoles.Perceiver => CustomRoles.EngineerTOHE,
            CustomRoles.Convener => CustomRoles.EngineerTOHE,
            CustomRoles.Nightmare => CustomRoles.CrewmateTOHE,
            CustomRoles.CameraMan => CustomRoles.EngineerTOHE,
            CustomRoles.Spy => CustomRoles.CrewmateTOHE,
            CustomRoles.Ricochet => CustomRoles.CrewmateTOHE,
            CustomRoles.Aid => CustomRoles.CrewmateTOHE,
            CustomRoles.DonutDelivery => CustomRoles.CrewmateTOHE,
            CustomRoles.Gaulois => CustomRoles.CrewmateTOHE,
            CustomRoles.Analyzer => CustomRoles.CrewmateTOHE,
            CustomRoles.Escort => CustomRoles.CrewmateTOHE,
            CustomRoles.Doormaster => CustomRoles.EngineerTOHE,
            CustomRoles.Tether => CustomRoles.EngineerTOHE,
            CustomRoles.Psychic => CustomRoles.CrewmateTOHE,
            CustomRoles.Needy => CustomRoles.CrewmateTOHE,
            CustomRoles.SuperStar => CustomRoles.CrewmateTOHE,
            CustomRoles.Paranoia => CustomRoles.EngineerTOHE,
            CustomRoles.EngineerTOHE => CustomRoles.EngineerTOHE,
            CustomRoles.TimeMaster => CustomRoles.EngineerTOHE,
            CustomRoles.CrewmateTOHE => CustomRoles.CrewmateTOHE,
            CustomRoles.Doctor => CustomRoles.ScientistTOHE,
            CustomRoles.ScientistTOHE => CustomRoles.ScientistTOHE,
            CustomRoles.Tracefinder => CustomRoles.ScientistTOHE,
            CustomRoles.SpeedBooster => CustomRoles.CrewmateTOHE,
            CustomRoles.Dictator => CustomRoles.CrewmateTOHE,
            CustomRoles.Snitch => CustomRoles.CrewmateTOHE,
            CustomRoles.Marshall => CustomRoles.CrewmateTOHE,
            CustomRoles.SabotageMaster => CustomRoles.EngineerTOHE,
            //CustomRoles.Retributionist => CustomRoles.Crewmate,
            CustomRoles.Witness => CustomRoles.CrewmateTOHE,
            CustomRoles.Guardian => CustomRoles.CrewmateTOHE,
            CustomRoles.Addict => CustomRoles.EngineerTOHE,
            CustomRoles.Alchemist => CustomRoles.EngineerTOHE,
            CustomRoles.Oracle => CustomRoles.CrewmateTOHE,
            CustomRoles.Chameleon => CustomRoles.EngineerTOHE,
            _ => role.IsImpostor() ? CustomRoles.ImpostorTOHE : CustomRoles.CrewmateTOHE,
        };

    public static RoleTypes GetDYRole(this CustomRoles role)
    {
        bool UsePets = Options.UsePets.GetBool();
        return role switch
        {
            //SoloKombat
            CustomRoles.KB_Normal => RoleTypes.Impostor,
            //FFA
            CustomRoles.Killer => RoleTypes.Impostor,
            //Move And Stop
            CustomRoles.Tasker => RoleTypes.Crewmate,
            //Standard
            CustomRoles.Sheriff => UsePets && Sheriff.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Crusader => UsePets && Crusader.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.CopyCat => UsePets && CopyCat.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            //CustomRoles.CursedSoul => RoleTypes.Impostor,
            CustomRoles.Admirer => UsePets && Gaulois.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Refugee => RoleTypes.Impostor,
            CustomRoles.Amnesiac => RoleTypes.Impostor,
            CustomRoles.Agitater => RoleTypes.Impostor,
            CustomRoles.Monarch => UsePets && Monarch.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Deputy => UsePets && Deputy.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Arsonist => RoleTypes.Impostor,
            CustomRoles.Jackal => RoleTypes.Impostor,
            CustomRoles.Medusa => RoleTypes.Impostor,
            CustomRoles.Sidekick => RoleTypes.Impostor,
            CustomRoles.SwordsMan => UsePets && SwordsMan.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            //CustomRoles.Reverie => RoleTypes.Impostor,
            CustomRoles.Innocent => RoleTypes.Impostor,
            CustomRoles.Pelican => RoleTypes.Impostor,
            //CustomRoles.Counterfeiter => RoleTypes.Impostor,
            CustomRoles.Aid => UsePets && Aid.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Escort => UsePets && Escort.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.DonutDelivery => UsePets && DonutDelivery.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Gaulois => UsePets && Gaulois.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Analyzer => UsePets && Analyzer.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Witness => UsePets && Options.WitnessUsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Pursuer => RoleTypes.Impostor,
            CustomRoles.Revolutionist => RoleTypes.Impostor,
            CustomRoles.FFF => RoleTypes.Impostor,
            CustomRoles.Medic => UsePets && Medic.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Gamer => RoleTypes.Impostor,
            CustomRoles.HexMaster => RoleTypes.Impostor,
            CustomRoles.Wraith => RoleTypes.Impostor,
            CustomRoles.Glitch => RoleTypes.Impostor,
            CustomRoles.Jailor => UsePets && Jailor.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Juggernaut => RoleTypes.Impostor,
            CustomRoles.Jinx => RoleTypes.Impostor,
            CustomRoles.DarkHide => RoleTypes.Impostor,
            CustomRoles.Provocateur => RoleTypes.Impostor,
            CustomRoles.BloodKnight => RoleTypes.Impostor,
            CustomRoles.Poisoner => RoleTypes.Impostor,
            CustomRoles.NSerialKiller => RoleTypes.Impostor,
            CustomRoles.SoulHunter => RoleTypes.Impostor,
            CustomRoles.Enderman => RoleTypes.Impostor,
            CustomRoles.Mycologist => RoleTypes.Impostor,
            CustomRoles.Bubble => RoleTypes.Impostor,
            CustomRoles.Hookshot => RoleTypes.Impostor,
            CustomRoles.Sprayer => RoleTypes.Impostor,
            CustomRoles.PlagueDoctor => RoleTypes.Impostor,
            CustomRoles.Postman => RoleTypes.Impostor,
            CustomRoles.Reckless => RoleTypes.Impostor,
            CustomRoles.Magician => RoleTypes.Impostor,
            CustomRoles.WeaponMaster => RoleTypes.Impostor,
            CustomRoles.Pyromaniac => RoleTypes.Impostor,
            CustomRoles.Eclipse => RoleTypes.Impostor,
            CustomRoles.Vengeance => RoleTypes.Impostor,
            CustomRoles.HeadHunter => RoleTypes.Impostor,
            CustomRoles.Imitator => RoleTypes.Impostor,
            CustomRoles.Werewolf => RoleTypes.Impostor,
            CustomRoles.Bandit => RoleTypes.Impostor,
            CustomRoles.Maverick => RoleTypes.Impostor,
            CustomRoles.Parasite => RoleTypes.Impostor,
            //CustomRoles.NWitch => RoleTypes.Impostor,
            CustomRoles.Totocalcio => RoleTypes.Impostor,
            CustomRoles.Romantic => RoleTypes.Impostor,
            CustomRoles.VengefulRomantic => RoleTypes.Impostor,
            CustomRoles.RuthlessRomantic => RoleTypes.Impostor,
            CustomRoles.Succubus => RoleTypes.Impostor,
            CustomRoles.Infectious => RoleTypes.Impostor,
            CustomRoles.Virus => RoleTypes.Impostor,
            CustomRoles.Farseer => UsePets && Farseer.UsePet.GetBool() ? RoleTypes.GuardianAngel : RoleTypes.Impostor,
            CustomRoles.Ritualist => RoleTypes.Impostor,
            CustomRoles.Pickpocket => RoleTypes.Impostor,
            CustomRoles.Traitor => RoleTypes.Impostor,
            CustomRoles.PlagueBearer => RoleTypes.Impostor,
            CustomRoles.Pestilence => RoleTypes.Impostor,
            CustomRoles.Spiritcaller => RoleTypes.Impostor,
            CustomRoles.Doppelganger => RoleTypes.Impostor,
            _ => RoleTypes.GuardianAngel
        };
    }
    public static bool IsAdditionRole(this CustomRoles role) => role is
        CustomRoles.Lovers or
        CustomRoles.LastImpostor or
        CustomRoles.Ntr or
        CustomRoles.Madmate or
        CustomRoles.Watcher or
        CustomRoles.Sleuth or
        CustomRoles.Admired or
        CustomRoles.Flashman or
        CustomRoles.Busy or
        CustomRoles.Truant or
        CustomRoles.Disco or
        CustomRoles.Asthmatic or
        CustomRoles.Giant or
        CustomRoles.Physicist or
        CustomRoles.Nimble or
        CustomRoles.Torch or
        CustomRoles.Seer or
        CustomRoles.Bait or
        CustomRoles.Diseased or
        CustomRoles.Antidote or
        CustomRoles.Swift or
        CustomRoles.Gravestone or
        CustomRoles.Trapper or
        CustomRoles.Mare or
        CustomRoles.Brakar or
        CustomRoles.Oblivious or
        CustomRoles.Bewilder or
        CustomRoles.Sunglasses or
        CustomRoles.Knighted or
        CustomRoles.Workhorse or
        CustomRoles.Fool or
        CustomRoles.Autopsy or
        CustomRoles.Necroview or
        CustomRoles.Avanger or
        CustomRoles.Youtuber or
        CustomRoles.Soulless or
        CustomRoles.Loyal or
        CustomRoles.Egoist or
        CustomRoles.Recruit or
        CustomRoles.Glow or
        CustomRoles.TicketsStealer or
        CustomRoles.DualPersonality or
        CustomRoles.Mimic or
        CustomRoles.Reach or
        CustomRoles.Magnet or
        CustomRoles.DeadlyQuota or
        CustomRoles.Damocles or
        CustomRoles.Stressed or
        CustomRoles.Cleansed or
        CustomRoles.Charmed or
        CustomRoles.Infected or
        CustomRoles.Onbound or
        CustomRoles.Lazy or
        //     CustomRoles.Reflective or
        CustomRoles.Rascal or
        CustomRoles.Contagious or
        CustomRoles.Guesser or
        CustomRoles.Rogue or
        CustomRoles.Unreportable or
        CustomRoles.Lucky or
        CustomRoles.Unlucky or
        CustomRoles.DoubleShot or
        CustomRoles.Ghoul or
        CustomRoles.EvilSpirit;
    public static bool IsNonNK(this CustomRoles role) // ROLE ASSIGNING, NOT NEUTRAL TYPE
        => role is
        CustomRoles.Jester or
        CustomRoles.Postman or
        CustomRoles.SoulHunter or
        CustomRoles.Terrorist or
        CustomRoles.Opportunist or
        //CustomRoles.Masochist or
        CustomRoles.Executioner or
        CustomRoles.Mario or
        CustomRoles.Crewpostor or
        CustomRoles.Lawyer or
        CustomRoles.God or
        CustomRoles.Amnesiac or
        CustomRoles.Innocent or
        CustomRoles.Vulture or
        //CustomRoles.NWitch or
        CustomRoles.Pursuer or
        CustomRoles.Revolutionist or
        CustomRoles.Provocateur or
        CustomRoles.Gamer or
        CustomRoles.FFF or
        CustomRoles.Workaholic or
        //    CustomRoles.Pelican or
        CustomRoles.Collector or
        CustomRoles.Sunnyboy or
        CustomRoles.Maverick or
        //CustomRoles.CursedSoul or
        CustomRoles.Phantom or
        CustomRoles.Totocalcio or
        CustomRoles.Romantic or
        CustomRoles.VengefulRomantic or
        CustomRoles.Doomsayer or
        CustomRoles.Succubus;

    public static bool IsAmneMaverick(this CustomRoles role) // ROLE ASSIGNING, NOT NEUTRAL TYPE
        => role is
        CustomRoles.Jester or
        CustomRoles.Terrorist or
        CustomRoles.Opportunist or
        //CustomRoles.Masochist or
        CustomRoles.Executioner or
        CustomRoles.Mario or
        CustomRoles.Doppelganger or
        CustomRoles.Crewpostor or
        CustomRoles.Glitch or
        CustomRoles.Lawyer or
        CustomRoles.Agitater or
        CustomRoles.God or
        CustomRoles.Bandit or
        CustomRoles.Amnesiac or
        CustomRoles.Pestilence or
        CustomRoles.PlagueBearer or
        CustomRoles.Innocent or
        CustomRoles.Vulture or
        //CustomRoles.NWitch or
        CustomRoles.Pursuer or
        CustomRoles.Revolutionist or
        CustomRoles.Provocateur or
        CustomRoles.Gamer or
        CustomRoles.FFF or
        CustomRoles.Workaholic or
        //    CustomRoles.Pelican or
        CustomRoles.Collector or
        CustomRoles.Sunnyboy or
        CustomRoles.Arsonist or
        CustomRoles.Maverick or
        //CustomRoles.CursedSoul or
        CustomRoles.Phantom or
        CustomRoles.DarkHide or
        CustomRoles.Ritualist or
        CustomRoles.Doomsayer or
        //CustomRoles.Pirate or
        //     CustomRoles.Juggernaut or
        CustomRoles.Jinx or
        CustomRoles.Romantic or
        CustomRoles.Romantic or
        CustomRoles.VengefulRomantic or
        //     CustomRoles.Poisoner or
        //     CustomRoles.HexMaster or
        CustomRoles.Totocalcio;
    public static bool IsAmneNK(this CustomRoles role) => role is
        CustomRoles.Sidekick or
        CustomRoles.Infectious or
        CustomRoles.Glitch or
        CustomRoles.Wraith or
        CustomRoles.Medusa or
        CustomRoles.Pelican or
        CustomRoles.Refugee or
        CustomRoles.Parasite or
        CustomRoles.NSerialKiller or
        CustomRoles.Enderman or
        CustomRoles.Mycologist or
        CustomRoles.Bubble or
        CustomRoles.Hookshot or
        CustomRoles.Sprayer or
        CustomRoles.PlagueDoctor or
        CustomRoles.Magician or
        CustomRoles.WeaponMaster or
        CustomRoles.Reckless or
        CustomRoles.Pyromaniac or
        CustomRoles.Eclipse or
        CustomRoles.HeadHunter or
        CustomRoles.Vengeance or
        CustomRoles.Imitator or
        CustomRoles.Werewolf or
        CustomRoles.Pickpocket or
        CustomRoles.Traitor or
        CustomRoles.Virus or
        CustomRoles.Spiritcaller or
        CustomRoles.Succubus;

    public static bool IsNK(this CustomRoles role) => role is
        CustomRoles.Jackal or
        CustomRoles.Glitch or
        CustomRoles.Sidekick or
        CustomRoles.HexMaster or
        CustomRoles.Doppelganger or
        CustomRoles.Agitater or
        CustomRoles.Infectious or
        CustomRoles.Wraith or
        CustomRoles.Bandit or
        CustomRoles.Medusa or
        CustomRoles.Pelican or
        //CustomRoles.Pirate or
        CustomRoles.DarkHide or
        CustomRoles.Juggernaut or
        CustomRoles.Jinx or
        CustomRoles.Poisoner or
        CustomRoles.Refugee or
        CustomRoles.Parasite or
        CustomRoles.NSerialKiller or
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
        CustomRoles.Imitator or
        CustomRoles.Werewolf or
        CustomRoles.Ritualist or
        CustomRoles.Arsonist or
        CustomRoles.Pickpocket or
        CustomRoles.PlagueDoctor or
        CustomRoles.Traitor or
        CustomRoles.Virus or
        CustomRoles.BloodKnight or
        CustomRoles.Spiritcaller or
        CustomRoles.RuthlessRomantic or
        CustomRoles.PlagueBearer or
        CustomRoles.Pestilence;
    public static bool IsSnitchTarget(this CustomRoles role) => role is
        CustomRoles.Jackal or
        CustomRoles.Sidekick or
        CustomRoles.HexMaster or
        CustomRoles.Refugee or
        CustomRoles.Agitater or
        CustomRoles.Doppelganger or
        CustomRoles.Infectious or
        CustomRoles.Wraith or
        CustomRoles.Crewpostor or
        CustomRoles.Juggernaut or
        CustomRoles.Jinx or
        CustomRoles.Bandit or
        CustomRoles.DarkHide or
        CustomRoles.Poisoner or
        CustomRoles.Parasite or
        CustomRoles.NSerialKiller or
        CustomRoles.Enderman or
        CustomRoles.Mycologist or
        CustomRoles.Bubble or
        CustomRoles.Hookshot or
        CustomRoles.Sprayer or
        CustomRoles.PlagueDoctor or
        CustomRoles.Pyromaniac or
        CustomRoles.Eclipse or
        CustomRoles.Vengeance or
        CustomRoles.HeadHunter or
        CustomRoles.Imitator or
        CustomRoles.Werewolf or
        CustomRoles.Ritualist or
        CustomRoles.Pickpocket or
        CustomRoles.Traitor or
        CustomRoles.Medusa or
        CustomRoles.Gamer or
        CustomRoles.Pelican or
        CustomRoles.Virus or
        CustomRoles.Succubus or
        CustomRoles.BloodKnight or
        CustomRoles.Spiritcaller or
        CustomRoles.RuthlessRomantic or
        CustomRoles.PlagueBearer or
        CustomRoles.Pestilence;
    public static bool IsNE(this CustomRoles role) => role is
        CustomRoles.Jester or
        CustomRoles.Gamer or
        //CustomRoles.Masochist or
        CustomRoles.Arsonist or
        CustomRoles.Executioner or
        CustomRoles.Doomsayer or
        CustomRoles.Innocent;
    public static bool IsNB(this CustomRoles role) => role is
        CustomRoles.Opportunist or
        CustomRoles.Lawyer or
        CustomRoles.Amnesiac or
        //CustomRoles.NWitch or
        CustomRoles.God or
        CustomRoles.Pursuer or
        CustomRoles.FFF or
        CustomRoles.Sunnyboy or
        CustomRoles.Maverick or
        CustomRoles.Romantic or
        CustomRoles.VengefulRomantic or
        CustomRoles.Totocalcio;
    public static bool IsNC(this CustomRoles role) => role is
        CustomRoles.Mario or
        CustomRoles.Terrorist or
        //CustomRoles.Pirate or
        CustomRoles.Revolutionist or
        CustomRoles.Vulture or
        //CustomRoles.Famine or
        //CustomRoles.Baker or
        //CustomRoles.CursedSoul or
        CustomRoles.Phantom or
        CustomRoles.Workaholic or
        CustomRoles.Collector or
        CustomRoles.Provocateur;
    public static bool IsNeutralKilling(this CustomRoles role) => role is
        CustomRoles.Terrorist or
        CustomRoles.Arsonist or
        CustomRoles.Juggernaut or
        CustomRoles.Medusa or
        CustomRoles.Jinx or
        CustomRoles.Agitater or
        CustomRoles.Jackal or
        CustomRoles.Sidekick or
        CustomRoles.Refugee or
        CustomRoles.God or
        CustomRoles.Mario or
        CustomRoles.Innocent or
        CustomRoles.Pelican or
        CustomRoles.Wraith or
        CustomRoles.HexMaster or
        CustomRoles.Egoist or
        CustomRoles.Gamer or
        CustomRoles.Parasite or
        CustomRoles.DarkHide or
        CustomRoles.Workaholic or
        CustomRoles.Collector or
        CustomRoles.Traitor or
        CustomRoles.Poisoner or
        CustomRoles.NSerialKiller or
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
        CustomRoles.Imitator or
        CustomRoles.Werewolf or
        CustomRoles.Ritualist or
        CustomRoles.Pickpocket or
        CustomRoles.Traitor or
        CustomRoles.Bandit or
        CustomRoles.BloodKnight or
        CustomRoles.Infectious or
        CustomRoles.PlagueDoctor or
        CustomRoles.Virus or
        CustomRoles.Succubus or
        CustomRoles.Spiritcaller or
        CustomRoles.RuthlessRomantic or
        CustomRoles.PlagueBearer or
        CustomRoles.Pestilence;
    public static bool IsCK(this CustomRoles role) => role is
        CustomRoles.SwordsMan or
        CustomRoles.Veteran or
        CustomRoles.CopyCat or
        CustomRoles.Bodyguard or
        //CustomRoles.Reverie or
        CustomRoles.Crusader or
        CustomRoles.NiceGuesser or
        //CustomRoles.Counterfeiter or
        //CustomRoles.Retributionist or
        CustomRoles.Sheriff or
        CustomRoles.Jailor;
    public static bool IsImpostor(this CustomRoles role) // IsImp
        => role is
        CustomRoles.Impostor or
        CustomRoles.Godfather or
        CustomRoles.Shapeshifter or
        CustomRoles.ShapeshifterTOHE or
        CustomRoles.ImpostorTOHE or
        CustomRoles.EvilDiviner or
        CustomRoles.Wildling or
        CustomRoles.Blackmailer or
        CustomRoles.Morphling or
        CustomRoles.BountyHunter or
        CustomRoles.Vampire or
        CustomRoles.Witch or
        CustomRoles.Vindicator or
        //CustomRoles.ShapeMaster or
        CustomRoles.Zombie or
        CustomRoles.Warlock or
        CustomRoles.Assassin or
        CustomRoles.Undertaker or
        CustomRoles.Hacker or
        CustomRoles.Visionary or
        CustomRoles.Miner or
        CustomRoles.Escapee or
        CustomRoles.SerialKiller or
        CustomRoles.Underdog or
        CustomRoles.Inhibitor or
        CustomRoles.Kidnapper or
        CustomRoles.Swapster or
        CustomRoles.Kamikaze or
        CustomRoles.Librarian or
        CustomRoles.Cantankerous or
        CustomRoles.Swiftclaw or
        CustomRoles.Duellist or
        CustomRoles.YinYanger or
        CustomRoles.Consort or
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
        CustomRoles.Mafia or
        CustomRoles.Minimalism or
        CustomRoles.FireWorks or
        CustomRoles.Sniper or
        CustomRoles.EvilTracker or
        CustomRoles.EvilGuesser or
        CustomRoles.AntiAdminer or
        CustomRoles.Sans or
        CustomRoles.Bomber or
        CustomRoles.Nuker or
        CustomRoles.Scavenger or
        CustomRoles.BoobyTrap or
        CustomRoles.Capitalism or
        CustomRoles.Gangster or
        CustomRoles.Cleaner or
        CustomRoles.BallLightning or
        CustomRoles.Greedier or
        CustomRoles.CursedWolf or
        CustomRoles.ImperiusCurse or
        CustomRoles.QuickShooter or
        CustomRoles.Eraser or
        CustomRoles.OverKiller or
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
    public static bool IsNeutral(this CustomRoles role) => role is
        //SoloKombat
        CustomRoles.KB_Normal or
        //FFA
        CustomRoles.Killer or
        //Move And Stop
        CustomRoles.Tasker or
        //Standard
        CustomRoles.Jester or
        CustomRoles.Doppelganger or
        CustomRoles.Opportunist or
        CustomRoles.Agitater or
        CustomRoles.Mario or
        //CustomRoles.Masochist or
        CustomRoles.Amnesiac or
        CustomRoles.Medusa or
        //CustomRoles.Famine or
        //CustomRoles.Baker or
        CustomRoles.HexMaster or
        CustomRoles.Glitch or
        CustomRoles.Crewpostor or
        //CustomRoles.NWitch or
        CustomRoles.Wraith or
        CustomRoles.Vulture or
        CustomRoles.Parasite or
        CustomRoles.Bandit or
        CustomRoles.Terrorist or
        CustomRoles.Executioner or
        CustomRoles.Juggernaut or
        CustomRoles.Refugee or
        CustomRoles.Jinx or
        CustomRoles.Lawyer or
        CustomRoles.Arsonist or
        CustomRoles.Sidekick or
        CustomRoles.Jackal or
        CustomRoles.God or
        CustomRoles.Innocent or
        CustomRoles.Pursuer or
        CustomRoles.PlagueBearer or
        CustomRoles.Pestilence or
        //CustomRoles.Pirate or

        //    CustomRoles.Sidekick or
        CustomRoles.Poisoner or
        CustomRoles.NSerialKiller or
        CustomRoles.SoulHunter or
        CustomRoles.Enderman or
        CustomRoles.Mycologist or
        CustomRoles.Bubble or
        CustomRoles.Hookshot or
        CustomRoles.Sprayer or
        CustomRoles.PlagueDoctor or
        CustomRoles.Magician or
        CustomRoles.WeaponMaster or
        CustomRoles.Reckless or
        CustomRoles.Postman or
        CustomRoles.Eclipse or
        CustomRoles.Pyromaniac or
        CustomRoles.Vengeance or
        CustomRoles.HeadHunter or
        CustomRoles.Imitator or
        CustomRoles.Werewolf or
        CustomRoles.Ritualist or
        CustomRoles.Pickpocket or
        CustomRoles.Pelican or
        CustomRoles.Traitor or
        CustomRoles.Revolutionist or
        CustomRoles.FFF or
        CustomRoles.Konan or
        CustomRoles.Gamer or
        CustomRoles.Maverick or
        //CustomRoles.CursedSoul or
        CustomRoles.DarkHide or
        CustomRoles.Infectious or
        CustomRoles.Workaholic or
        CustomRoles.Collector or
        CustomRoles.Provocateur or
        CustomRoles.Sunnyboy or
        CustomRoles.Phantom or
        CustomRoles.BloodKnight or
        CustomRoles.Romantic or
        CustomRoles.Totocalcio or
        CustomRoles.RuthlessRomantic or
        CustomRoles.VengefulRomantic or
        CustomRoles.Virus or
        CustomRoles.Succubus or
        CustomRoles.Doomsayer or
        CustomRoles.Spiritcaller;

    public static bool IsAbleToBeSidekicked(this CustomRoles role) => role is
        CustomRoles.BloodKnight or
        CustomRoles.Virus or
        CustomRoles.Medusa or
        CustomRoles.NSerialKiller or
        CustomRoles.SoulHunter or
        CustomRoles.Enderman or
        CustomRoles.Mycologist or
        CustomRoles.Bubble or
        CustomRoles.Hookshot or
        CustomRoles.Sprayer or
        CustomRoles.Doppelganger or
        CustomRoles.PlagueDoctor or
        CustomRoles.Eclipse or
        CustomRoles.Magician or
        CustomRoles.WeaponMaster or
        CustomRoles.Reckless or
        CustomRoles.Postman or
        CustomRoles.Pyromaniac or
        CustomRoles.Vengeance or
        CustomRoles.HeadHunter or
        CustomRoles.Imitator or
        CustomRoles.Werewolf or
        CustomRoles.Traitor or
        CustomRoles.HexMaster or
        CustomRoles.Sheriff or
        CustomRoles.Medic or
        CustomRoles.Crusader or
        CustomRoles.Deputy or
        CustomRoles.Glitch or
        CustomRoles.Ritualist or
        CustomRoles.CopyCat or
        CustomRoles.Pickpocket or
        CustomRoles.Poisoner or
        //CustomRoles.Reverie or
        CustomRoles.Arsonist or
        CustomRoles.Revolutionist or
        CustomRoles.Maverick or
        //CustomRoles.NWitch or
        CustomRoles.Succubus or
        CustomRoles.Gamer or
        CustomRoles.DarkHide or
        CustomRoles.Provocateur or
        CustomRoles.Wraith or
        CustomRoles.Juggernaut or
        CustomRoles.Pelican or
        CustomRoles.Infectious or
        CustomRoles.Pursuer or
        CustomRoles.Jinx or
        //CustomRoles.Counterfeiter or
        CustomRoles.Witness or
        CustomRoles.Aid or
        CustomRoles.DonutDelivery or
        CustomRoles.Gaulois or
        CustomRoles.Analyzer or
        CustomRoles.Escort or
        CustomRoles.Totocalcio or
        CustomRoles.Farseer or
        CustomRoles.FFF or
        CustomRoles.SwordsMan or
        //CustomRoles.CursedSoul or
        CustomRoles.Admirer or
        CustomRoles.Refugee or
        CustomRoles.Amnesiac or
        CustomRoles.Monarch or
        CustomRoles.Parasite or
        CustomRoles.PlagueBearer;

    public static bool IsEvilAddons(this CustomRoles role) => role is
        CustomRoles.Madmate or
        CustomRoles.Egoist or
        CustomRoles.Charmed or
        CustomRoles.Recruit or
        CustomRoles.Infected or
        CustomRoles.Contagious or
        CustomRoles.Rogue or
        CustomRoles.Rascal or
        CustomRoles.Soulless;

    public static bool IsMadmate(this CustomRoles role) => role is
        CustomRoles.Crewpostor or
        CustomRoles.Convict or
        CustomRoles.Refugee or
        CustomRoles.Parasite;
    public static bool IsTasklessCrewmate(this CustomRoles role) => !role.UsesPetInsteadOfKill() && role is
        CustomRoles.Sheriff or
        CustomRoles.Admirer or
        CustomRoles.Medic or
        CustomRoles.CopyCat or
        //CustomRoles.Reverie or
        CustomRoles.Crusader or
        //CustomRoles.Counterfeiter or
        CustomRoles.Aid or
        CustomRoles.Escort or
        CustomRoles.DonutDelivery or
        CustomRoles.Gaulois or
        CustomRoles.Analyzer or
        CustomRoles.Witness or
        CustomRoles.Monarch or
        CustomRoles.Jailor or
        CustomRoles.Farseer or
        CustomRoles.SwordsMan or
        CustomRoles.Deputy;
    public static bool PetActivatedAbility(this CustomRoles role) => Options.UsePets.GetBool() && role switch
    {
        CustomRoles.Doormaster or
        CustomRoles.Tether or
        CustomRoles.Mayor or
        CustomRoles.Paranoia or
        CustomRoles.Grenadier or
        CustomRoles.Veteran or
        CustomRoles.Lighter or
        CustomRoles.SecurityGuard or
        CustomRoles.DovesOfNeace or
        CustomRoles.Alchemist or
        CustomRoles.TimeMaster or
        CustomRoles.Sapper or
        CustomRoles.CameraMan or
        CustomRoles.NiceHacker or
        CustomRoles.Druid or
        CustomRoles.Tunneler or
        CustomRoles.Tornado or
        CustomRoles.Sentinel or
        CustomRoles.Lookout or
        CustomRoles.Sniper or
        CustomRoles.FireWorks or
        CustomRoles.Warlock or
        CustomRoles.Assassin or
        CustomRoles.Undertaker or
        CustomRoles.Disperser or
        CustomRoles.Twister or
        CustomRoles.QuickShooter or
        CustomRoles.Miner or
        CustomRoles.Escapee or
        CustomRoles.RiftMaker or
        CustomRoles.Bomber or
        CustomRoles.Nuker or
        CustomRoles.Arsonist or
        CustomRoles.Glitch or
        CustomRoles.Magician or
        CustomRoles.WeaponMaster or
        CustomRoles.Enderman or
        CustomRoles.Mycologist or
        CustomRoles.Hookshot or
        CustomRoles.Sprayer
        => true,

        CustomRoles.Gaulois when Gaulois.UsePet.GetBool() => true,
        CustomRoles.Aid when Aid.UsePet.GetBool() => true,
        CustomRoles.Escort when Escort.UsePet.GetBool() => true,
        CustomRoles.DonutDelivery when DonutDelivery.UsePet.GetBool() => true,
        CustomRoles.Analyzer when Analyzer.UsePet.GetBool() => true,
        CustomRoles.Jailor when Jailor.UsePet.GetBool() => true,
        CustomRoles.Sheriff when Sheriff.UsePet.GetBool() => true,
        CustomRoles.SwordsMan when SwordsMan.UsePet.GetBool() => true,
        CustomRoles.Medic when Medic.UsePet.GetBool() => true,
        CustomRoles.Monarch when Monarch.UsePet.GetBool() => true,
        CustomRoles.CopyCat when CopyCat.UsePet.GetBool() => true,
        CustomRoles.Farseer when Farseer.UsePet.GetBool() => true,
        CustomRoles.Deputy when Deputy.UsePet.GetBool() => true,
        CustomRoles.Admirer when Admirer.UsePet.GetBool() => true,
        CustomRoles.Crusader when Crusader.UsePet.GetBool() => true,
        CustomRoles.Witness when Options.WitnessUsePet.GetBool() => true,

        _ => false,
    };
    public static bool UsesPetInsteadOfKill(this CustomRoles role) => Options.UsePets.GetBool() && role switch
    {
        CustomRoles.Gaulois when Gaulois.UsePet.GetBool() => true,
        CustomRoles.Aid when Aid.UsePet.GetBool() => true,
        CustomRoles.Escort when Escort.UsePet.GetBool() => true,
        CustomRoles.DonutDelivery when DonutDelivery.UsePet.GetBool() => true,
        CustomRoles.Analyzer when Analyzer.UsePet.GetBool() => true,
        CustomRoles.Jailor when Jailor.UsePet.GetBool() => true,
        CustomRoles.Sheriff when Sheriff.UsePet.GetBool() => true,
        CustomRoles.SwordsMan when SwordsMan.UsePet.GetBool() => true,
        CustomRoles.Medic when Medic.UsePet.GetBool() => true,
        CustomRoles.Monarch when Monarch.UsePet.GetBool() => true,
        CustomRoles.CopyCat when CopyCat.UsePet.GetBool() => true,
        CustomRoles.Farseer when Farseer.UsePet.GetBool() => true,
        CustomRoles.Deputy when Deputy.UsePet.GetBool() => true,
        CustomRoles.Admirer when Admirer.UsePet.GetBool() => true,
        CustomRoles.Crusader when Crusader.UsePet.GetBool() => true,
        CustomRoles.Witness when Options.WitnessUsePet.GetBool() => true,

        _ => false,
    };
    public static List<CustomRoles> OnlySpawnsWithPetsRoleList = [
        CustomRoles.Tunneler,
        CustomRoles.Tornado,
        CustomRoles.Swiftclaw,
        ];
    public static bool OnlySpawnsWithPets(this CustomRoles role) => OnlySpawnsWithPetsRoleList.Contains(role);
    public static bool NeedUpdateOnLights(this CustomRoles role) => (!role.UsesPetInsteadOfKill()) && (role.GetDYRole() != RoleTypes.GuardianAngel || role is
        CustomRoles.Sheriff or
        CustomRoles.Medic or
        CustomRoles.CopyCat or
        //CustomRoles.Reverie or
        CustomRoles.Crusader or
        //CustomRoles.Counterfeiter or
        CustomRoles.Aid or
        CustomRoles.DonutDelivery or
        CustomRoles.Gaulois or
        CustomRoles.Analyzer or
        CustomRoles.Escort or
        CustomRoles.Witness or
        CustomRoles.Monarch or
        CustomRoles.Jailor or
        CustomRoles.Farseer or
        CustomRoles.SwordsMan or
        CustomRoles.Alchemist or
        CustomRoles.Convict or
        CustomRoles.Torch or
        CustomRoles.Amnesiac or
        CustomRoles.Medusa or
        CustomRoles.HexMaster or
        CustomRoles.Glitch or
        //CustomRoles.NWitch or
        CustomRoles.Wraith or
        CustomRoles.Parasite or
        CustomRoles.Juggernaut or
        CustomRoles.Refugee or
        CustomRoles.Jinx or
        CustomRoles.Arsonist or
        CustomRoles.Jackal or
        CustomRoles.Innocent or
        CustomRoles.PlagueBearer or
        CustomRoles.Pestilence or
        CustomRoles.Poisoner or
        CustomRoles.NSerialKiller or
        CustomRoles.SoulHunter or
        CustomRoles.Enderman or
        CustomRoles.Mycologist or
        CustomRoles.Bubble or
        CustomRoles.Hookshot or
        CustomRoles.Sprayer or
        CustomRoles.PlagueDoctor or
        CustomRoles.Magician or
        CustomRoles.WeaponMaster or
        CustomRoles.Reckless or
        CustomRoles.Postman or
        CustomRoles.Eclipse or
        CustomRoles.Pyromaniac or
        CustomRoles.Vengeance or
        CustomRoles.HeadHunter or
        CustomRoles.Imitator or
        CustomRoles.Werewolf or
        CustomRoles.Ritualist or
        CustomRoles.Pickpocket or
        CustomRoles.Pelican or
        CustomRoles.Traitor or
        CustomRoles.Revolutionist or
        CustomRoles.FFF or
        CustomRoles.Gamer or
        CustomRoles.Maverick or
        //CustomRoles.CursedSoul or
        CustomRoles.DarkHide or
        CustomRoles.Infectious or
        CustomRoles.Provocateur or
        CustomRoles.BloodKnight or
        CustomRoles.Romantic or
        CustomRoles.VengefulRomantic or
        CustomRoles.RuthlessRomantic or
        CustomRoles.Totocalcio or
        CustomRoles.Virus or
        CustomRoles.Succubus or
        CustomRoles.Spiritcaller or
        CustomRoles.Refugee or
        CustomRoles.Parasite or
        CustomRoles.Lighter or
        CustomRoles.SecurityGuard or
        CustomRoles.Ignitor or
        CustomRoles.Saboteur or
        CustomRoles.Inhibitor or
        CustomRoles.Gambler or
        CustomRoles.Mare or
        CustomRoles.Bandit or
        CustomRoles.Agitater or
        CustomRoles.Deputy);

    public static bool IsBetrayalAddon(this CustomRoles role) => role is
        CustomRoles.Charmed or
        CustomRoles.Recruit or
        CustomRoles.Infected or
        CustomRoles.Contagious or
        CustomRoles.Lovers or
        CustomRoles.Madmate or
        CustomRoles.Soulless or
        CustomRoles.Admired or
        CustomRoles.Egoist;

    public static bool IsImpOnlyAddon(this CustomRoles role) => role is
        CustomRoles.Mare or
        CustomRoles.LastImpostor or
        CustomRoles.Mare or
        CustomRoles.Mimic or
        CustomRoles.Damocles or
        CustomRoles.DeadlyQuota or
        CustomRoles.TicketsStealer or
        CustomRoles.Swift;
    public static bool IsTaskBasedCrewmate(this CustomRoles role) => role is
        CustomRoles.Snitch or
        CustomRoles.Divinator or
        CustomRoles.Marshall or
        CustomRoles.TimeManager or
        CustomRoles.Ignitor or
        CustomRoles.Guardian or
        CustomRoles.Merchant or
        CustomRoles.Mayor or
        CustomRoles.Transporter;

    public static bool IsNotKnightable(this CustomRoles role) => role is
        CustomRoles.Mayor or
        CustomRoles.Vindicator or
        CustomRoles.Dictator or
        CustomRoles.Knighted or
        CustomRoles.Glitch or
        CustomRoles.Pickpocket or
        CustomRoles.TicketsStealer;
    public static bool CheckAddonConflictV2(CustomRoles addon, CustomRoles mainRole) => addon.IsAdditionRole() && mainRole is not CustomRoles.GuardianAngelTOHE and not CustomRoles.God and not CustomRoles.GM && addon is not CustomRoles.Lovers && addon switch
    {
        CustomRoles.Lovers when mainRole is CustomRoles.RuthlessRomantic or CustomRoles.Romantic or CustomRoles.VengefulRomantic => false,
        CustomRoles.Autopsy when mainRole is CustomRoles.Doctor or CustomRoles.Tracefinder or CustomRoles.Scientist or CustomRoles.ScientistTOHE or CustomRoles.Sunnyboy => false,
        CustomRoles.Necroview when mainRole is CustomRoles.Doctor => false,
        CustomRoles.Lazy when mainRole is CustomRoles.Speedrunner => false,
        CustomRoles.Loyal when mainRole.IsCrewmate() && !Options.CrewCanBeLoyal.GetBool() => false,
        CustomRoles.DualPersonality when mainRole.IsCrewmate() && !Options.CrewCanBeDualPersonality.GetBool() => false,
        CustomRoles.Lazy when mainRole is CustomRoles.Needy or CustomRoles.Snitch or CustomRoles.Marshall or CustomRoles.Transporter or CustomRoles.Guardian => false,
        CustomRoles.Ghoul when mainRole is CustomRoles.Needy or CustomRoles.Snitch or CustomRoles.Marshall or CustomRoles.Transporter or CustomRoles.Guardian => false,
        CustomRoles.Brakar when mainRole is CustomRoles.Dictator => false,
        CustomRoles.Stressed when !mainRole.IsCrewmate() || mainRole.IsTasklessCrewmate() => false,
        CustomRoles.Avanger when mainRole.IsImpostor() && !Options.ImpCanBeAvanger.GetBool() => false,
        CustomRoles.Ghoul when mainRole.IsNeutral() || mainRole.IsImpostor() || mainRole.IsTasklessCrewmate() || mainRole.IsTaskBasedCrewmate() => false,
        CustomRoles.Lazy when mainRole.IsNeutral() || mainRole.IsImpostor() || (mainRole.IsTasklessCrewmate() && !Options.TasklessCrewCanBeLazy.GetBool()) || (mainRole.IsTaskBasedCrewmate() && !Options.TaskBasedCrewCanBeLazy.GetBool()) => false,
        CustomRoles.TicketsStealer or CustomRoles.Swift or CustomRoles.DeadlyQuota or CustomRoles.Damocles or CustomRoles.Mare when mainRole is CustomRoles.Bomber or CustomRoles.Nuker or CustomRoles.BoobyTrap or CustomRoles.Capitalism => false,
        CustomRoles.TicketsStealer or CustomRoles.Mimic or CustomRoles.Swift or CustomRoles.DeadlyQuota or CustomRoles.Damocles or CustomRoles.Mare when !mainRole.IsImpostor() => false,
        CustomRoles.Torch when !mainRole.IsCrewmate() || mainRole is CustomRoles.Bewilder or CustomRoles.Sunglasses or CustomRoles.GuardianAngelTOHE => false,
        CustomRoles.Bewilder when mainRole is CustomRoles.Torch or CustomRoles.GuardianAngelTOHE or CustomRoles.Sunglasses => false,
        CustomRoles.Sunglasses when mainRole is CustomRoles.Torch or CustomRoles.GuardianAngelTOHE or CustomRoles.Bewilder => false,
        CustomRoles.Guesser when (mainRole.IsCrewmate() && (!Options.CrewCanBeGuesser.GetBool() || mainRole is CustomRoles.NiceGuesser)) || (mainRole.IsNeutral() && !Options.NeutralCanBeGuesser.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeGuesser.GetBool()) || mainRole is CustomRoles.EvilGuesser or CustomRoles.GuardianAngelTOHE => false,
        CustomRoles.Madmate when mainRole is CustomRoles.Egoist or CustomRoles.Rascal => false,
        CustomRoles.Oblivious when mainRole is CustomRoles.Detective or CustomRoles.Cleaner or CustomRoles.Medusa or CustomRoles.Mortician or CustomRoles.Mediumshiper or CustomRoles.GuardianAngelTOHE => false,
        CustomRoles.Youtuber when !mainRole.IsCrewmate() || mainRole is CustomRoles.Madmate or CustomRoles.Sheriff or CustomRoles.GuardianAngelTOHE => false,
        CustomRoles.Egoist when !mainRole.IsImpostor() => false,
        CustomRoles.Damocles when mainRole is CustomRoles.Bomber or CustomRoles.Nuker or CustomRoles.SerialKiller or CustomRoles.Cantankerous => false,
        CustomRoles.Necroview when mainRole is CustomRoles.Visionary => false,
        CustomRoles.Flashman when mainRole is CustomRoles.Swiftclaw => false,
        CustomRoles.Ghoul when mainRole is CustomRoles.Lazy => false,
        CustomRoles.Ghoul when mainRole is CustomRoles.Needy => false,
        CustomRoles.Lazy when mainRole is CustomRoles.Ghoul => false,
        CustomRoles.Mimic when mainRole is CustomRoles.Mafia => false,
        CustomRoles.Rascal when !mainRole.IsCrewmate() => false,
        CustomRoles.Needy when mainRole.IsAdditionRole() => false,
        CustomRoles.TicketsStealer when mainRole is CustomRoles.Vindicator => false,
        CustomRoles.Mare when mainRole is CustomRoles.Underdog => false,
        CustomRoles.Mare when mainRole is CustomRoles.Inhibitor => false,
        CustomRoles.Mare when mainRole is CustomRoles.Saboteur => false,
        CustomRoles.Mare when mainRole is CustomRoles.Swift => false,
        CustomRoles.Mare when mainRole is CustomRoles.Mafia => false,
        CustomRoles.Mare when mainRole is CustomRoles.Sniper => false,
        CustomRoles.Mare when mainRole is CustomRoles.FireWorks => false,
        CustomRoles.Mare when mainRole is CustomRoles.Swooper => false,
        CustomRoles.Mare when mainRole is CustomRoles.Vampire => false,
        CustomRoles.Torch when mainRole is CustomRoles.Lighter => false,
        CustomRoles.Torch when mainRole is CustomRoles.Ignitor => false,
        CustomRoles.Bewilder when mainRole is CustomRoles.Lighter => false,
        CustomRoles.Bewilder when mainRole is CustomRoles.Ignitor => false,
        CustomRoles.Sunglasses when mainRole is CustomRoles.Lighter => false,
        CustomRoles.Sunglasses when mainRole is CustomRoles.Ignitor => false,
        CustomRoles.Bait when mainRole is CustomRoles.Demolitionist => false,
        CustomRoles.Trapper when mainRole is CustomRoles.Demolitionist => false,
        CustomRoles.Lovers when mainRole is CustomRoles.Romantic => false,
        CustomRoles.Mare when mainRole is CustomRoles.LastImpostor => false,
        CustomRoles.Swift when mainRole is CustomRoles.Mare => false,
        CustomRoles.DualPersonality when (!mainRole.IsImpostor() && !mainRole.IsCrewmate()) || mainRole is CustomRoles.Madmate => false,
        CustomRoles.DualPersonality when mainRole.IsImpostor() && !Options.ImpCanBeDualPersonality.GetBool() => false,
        CustomRoles.Loyal when (!mainRole.IsImpostor() && !mainRole.IsCrewmate()) || mainRole is CustomRoles.Madmate => false,
        CustomRoles.Loyal when mainRole.IsImpostor() && !Options.ImpCanBeLoyal.GetBool() => false,
        CustomRoles.Seer when (mainRole.IsCrewmate() && (!Options.CrewCanBeSeer.GetBool() || mainRole is CustomRoles.Mortician)) || (mainRole.IsNeutral() && !Options.NeutralCanBeSeer.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeSeer.GetBool()) || mainRole is CustomRoles.GuardianAngelTOHE => false,
        CustomRoles.Bait when (mainRole.IsCrewmate() && !Options.CrewCanBeBait.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeBait.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeBait.GetBool()) => false,
        CustomRoles.Bewilder when (mainRole.IsCrewmate() && !Options.CrewCanBeBewilder.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeBewilder.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeBewilder.GetBool()) => false,
        CustomRoles.Sunglasses when (mainRole.IsCrewmate() && !Options.CrewCanBeSunglasses.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeSunglasses.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeSunglasses.GetBool()) => false,
        CustomRoles.Autopsy when (mainRole.IsCrewmate() && !Options.CrewCanBeAutopsy.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeAutopsy.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeAutopsy.GetBool()) => false,
        CustomRoles.Glow when (mainRole.IsCrewmate() && !Options.CrewCanBeGlow.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeGlow.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeGlow.GetBool()) => false,
        CustomRoles.Trapper when (mainRole.IsCrewmate() && !Options.CrewCanBeTrapper.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeTrapper.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeTrapper.GetBool()) => false,
        CustomRoles.Sidekick when mainRole is CustomRoles.Madmate => false,
        CustomRoles.Onbound when mainRole is CustomRoles.SuperStar => false,
        CustomRoles.Rascal when mainRole is CustomRoles.SuperStar or CustomRoles.Madmate => false,
        CustomRoles.Madmate when mainRole is CustomRoles.SuperStar => false,
        CustomRoles.Gravestone when mainRole is CustomRoles.SuperStar => false,
        CustomRoles.Sidekick when mainRole.IsNeutral() && !Options.NeutralCanBeSidekick.GetBool() => false,
        CustomRoles.Sidekick when mainRole.IsCrewmate() && !Options.CrewmateCanBeSidekick.GetBool() => false,
        CustomRoles.Sidekick when mainRole.IsImpostor() && !Options.ImpostorCanBeSidekick.GetBool() => false,
        CustomRoles.Sidekick when mainRole is CustomRoles.Jackal => false,
        CustomRoles.Lucky when mainRole is CustomRoles.Guardian => false,
        CustomRoles.Bait when mainRole is CustomRoles.GuardianAngelTOHE => false,
        CustomRoles.Bait when mainRole is CustomRoles.Trapper => false,
        CustomRoles.Trapper when mainRole is CustomRoles.Bait => false,
        CustomRoles.DualPersonality when mainRole is CustomRoles.Dictator => false,
        CustomRoles.Swift when mainRole is CustomRoles.Swooper => false,
        CustomRoles.Swift when mainRole is CustomRoles.Vampire => false,
        CustomRoles.Swift when mainRole is CustomRoles.Scavenger => false,
        CustomRoles.Swift when mainRole is CustomRoles.Puppeteer => false,
        CustomRoles.Swift when mainRole is CustomRoles.Warlock => false,
        CustomRoles.Swift when mainRole is CustomRoles.EvilDiviner => false,
        CustomRoles.Swift when mainRole is CustomRoles.Witch => false,
        CustomRoles.Swift when mainRole is CustomRoles.Mafia => false,
        CustomRoles.Reach when mainRole is CustomRoles.Mafioso => false,
        CustomRoles.Trapper when mainRole is CustomRoles.GuardianAngelTOHE => false,
        CustomRoles.Watcher when (mainRole.IsCrewmate() && !Options.CrewCanBeWatcher.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeWatcher.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeWatcher.GetBool()) => false,
        CustomRoles.Diseased when (mainRole.IsCrewmate() && !Options.CrewCanBeDiseased.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeDiseased.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeDiseased.GetBool()) => false,
        CustomRoles.Antidote when (mainRole.IsCrewmate() && !Options.CrewCanBeAntidote.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeAntidote.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeAntidote.GetBool()) => false,
        CustomRoles.Diseased when mainRole is CustomRoles.Antidote => false,
        CustomRoles.Antidote when mainRole is CustomRoles.Diseased => false,
        CustomRoles.Necroview when (mainRole.IsCrewmate() && !Options.CrewCanBeNecroview.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeNecroview.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeNecroview.GetBool()) => false,
        CustomRoles.Oblivious when (mainRole.IsCrewmate() && !Options.CrewCanBeOblivious.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeOblivious.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeOblivious.GetBool()) => false,
        CustomRoles.Brakar when (mainRole.IsCrewmate() && !Options.CrewCanBeTiebreaker.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeTiebreaker.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeTiebreaker.GetBool()) => false,
        CustomRoles.Guesser when (mainRole.IsCrewmate() && !Options.CrewCanBeGuesser.GetBool() && mainRole is CustomRoles.NiceGuesser) || (mainRole.IsNeutral() && !Options.NeutralCanBeGuesser.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeGuesser.GetBool() && mainRole is CustomRoles.EvilGuesser) => false,
        CustomRoles.Onbound when (mainRole.IsCrewmate() && !Options.CrewCanBeOnbound.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeOnbound.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeOnbound.GetBool()) => false,
        CustomRoles.Unreportable when (mainRole.IsCrewmate() && !Options.CrewCanBeUnreportable.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeUnreportable.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeUnreportable.GetBool()) => false,
        CustomRoles.Lucky when (mainRole.IsCrewmate() && !Options.CrewCanBeLucky.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeLucky.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeLucky.GetBool()) => false,
        CustomRoles.Unlucky when (mainRole.IsCrewmate() && !Options.CrewCanBeUnlucky.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeUnlucky.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeUnlucky.GetBool()) => false,
        CustomRoles.Rogue when (mainRole.IsCrewmate() && !Options.CrewCanBeRogue.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeRogue.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeRogue.GetBool()) => false,
        CustomRoles.Gravestone when (mainRole.IsCrewmate() && !Options.CrewCanBeGravestone.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeGravestone.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeGravestone.GetBool()) => false,
        CustomRoles.Flashman or CustomRoles.Giant when mainRole is CustomRoles.Swooper or CustomRoles.Wraith or CustomRoles.Chameleon or CustomRoles.Alchemist => false,
        CustomRoles.Bait when mainRole is CustomRoles.Unreportable => false,
        CustomRoles.Truant when mainRole is CustomRoles.SoulHunter => false,
        CustomRoles.Nimble when !mainRole.IsCrewmate() => false,
        CustomRoles.Physicist when !mainRole.IsCrewmate() => false,
        CustomRoles.Unreportable when mainRole is CustomRoles.Bait => false,
        CustomRoles.Oblivious when mainRole is CustomRoles.Bloodhound => false,
        CustomRoles.Oblivious when mainRole is CustomRoles.Vulture => false,
        CustomRoles.Vulture when mainRole is CustomRoles.Oblivious => false,
        CustomRoles.Guesser when mainRole is CustomRoles.CopyCat or CustomRoles.Doomsayer => false,
        CustomRoles.CopyCat when mainRole is CustomRoles.Guesser => false,
        CustomRoles.DoubleShot when mainRole is CustomRoles.CopyCat => false,
        CustomRoles.CopyCat when mainRole is CustomRoles.DoubleShot => false,
        CustomRoles.Brakar when mainRole is CustomRoles.Dictator => false,
        CustomRoles.Lucky when mainRole is CustomRoles.Luckey => false,
        CustomRoles.Unlucky when mainRole is CustomRoles.Luckey => false,
        CustomRoles.Unlucky when mainRole is CustomRoles.Lucky => false,
        CustomRoles.Lucky when mainRole is CustomRoles.Unlucky => false,
        CustomRoles.Fool when (mainRole.IsCrewmate() && !Options.CrewCanBeFool.GetBool()) || (mainRole.IsNeutral() && !Options.NeutralCanBeFool.GetBool()) || (mainRole.IsImpostor() && !Options.ImpCanBeFool.GetBool()) || mainRole is CustomRoles.SabotageMaster or CustomRoles.GuardianAngelTOHE => false,
        CustomRoles.Bloodhound when mainRole is CustomRoles.Oblivious => false,
        CustomRoles.DoubleShot when (mainRole.GetCustomRoleTypes() is CustomRoleTypes.Impostor && !Options.ImpCanBeDoubleShot.GetBool()) || (mainRole.GetCustomRoleTypes() is CustomRoleTypes.Crewmate && !Options.CrewCanBeDoubleShot.GetBool()) || (mainRole.GetCustomRoleTypes() is CustomRoleTypes.Neutral && !Options.NeutralCanBeDoubleShot.GetBool()) => false,
        CustomRoles.DoubleShot when mainRole is not CustomRoles.EvilGuesser && mainRole is not CustomRoles.NiceGuesser && !Options.GuesserMode.GetBool() => false,
        CustomRoles.DoubleShot when Options.ImpCanBeDoubleShot.GetBool() && mainRole is not CustomRoles.Guesser && mainRole is not CustomRoles.EvilGuesser && mainRole.GetCustomRoleTypes() is CustomRoleTypes.Impostor && !Options.ImpostorsCanGuess.GetBool() => false,
        CustomRoles.DoubleShot when Options.CrewCanBeDoubleShot.GetBool() && mainRole is not CustomRoles.Guesser && mainRole is not CustomRoles.NiceGuesser && mainRole.GetCustomRoleTypes() is CustomRoleTypes.Crewmate && !Options.CrewmatesCanGuess.GetBool() => false,
        CustomRoles.DoubleShot when Options.NeutralCanBeDoubleShot.GetBool() && mainRole is not CustomRoles.Guesser && ((mainRole.IsNonNK() && !Options.PassiveNeutralsCanGuess.GetBool()) || (mainRole.IsNK() && !Options.NeutralKillersCanGuess.GetBool())) => false,
        _ => true
    };
    public static bool CheckAddonConflict(CustomRoles role, PlayerControl pc) => role.IsAdditionRole() && pc.GetCustomRole() is not CustomRoles.GuardianAngelTOHE and not CustomRoles.God && !pc.Is(CustomRoles.Madmate) && !pc.Is(CustomRoles.Egoist) && !pc.Is(CustomRoles.GM) && role is not CustomRoles.Lovers && !pc.Is(CustomRoles.Needy) && (!pc.HasSubRole() || pc.GetCustomSubRoles().Count < Options.NoLimitAddonsNumMax.GetInt()) && role switch
    {
        CustomRoles.Lovers when pc.Is(CustomRoles.RuthlessRomantic) || pc.Is(CustomRoles.Romantic) || pc.Is(CustomRoles.VengefulRomantic) => false,
        CustomRoles.Sidekick when pc.Is(CustomRoles.Madmate) => false,
        CustomRoles.Madmate when pc.Is(CustomRoles.Sidekick) => false,
        CustomRoles.Egoist when pc.Is(CustomRoles.Sidekick) => false,
        CustomRoles.Autopsy when pc.Is(CustomRoles.Doctor) || pc.Is(CustomRoles.Tracefinder) || pc.Is(CustomRoles.Scientist) || pc.Is(CustomRoles.ScientistTOHE) || pc.Is(CustomRoles.Sunnyboy) => false,
        CustomRoles.Necroview when pc.Is(CustomRoles.Doctor) => false,
        CustomRoles.Lazy when pc.Is(CustomRoles.Speedrunner) => false,
        CustomRoles.Loyal when pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeLoyal.GetBool() => false,
        CustomRoles.DualPersonality when pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeDualPersonality.GetBool() => false,
        CustomRoles.Lazy when pc.Is(CustomRoles.Needy) || pc.Is(CustomRoles.Snitch) || pc.Is(CustomRoles.Marshall) || pc.Is(CustomRoles.Transporter) || pc.Is(CustomRoles.Guardian) => false,
        CustomRoles.Ghoul when pc.Is(CustomRoles.Needy) || pc.Is(CustomRoles.Snitch) || pc.Is(CustomRoles.Marshall) || pc.Is(CustomRoles.Transporter) || pc.Is(CustomRoles.Guardian) => false,
        CustomRoles.Brakar when pc.Is(CustomRoles.Dictator) => false,
        CustomRoles.Stressed when !pc.GetCustomRole().IsCrewmate() || pc.GetCustomRole().IsTasklessCrewmate() => false,
        CustomRoles.Avanger when pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeAvanger.GetBool() => false,
        CustomRoles.Ghoul when pc.GetCustomRole().IsNeutral() || pc.GetCustomRole().IsImpostor() || pc.GetCustomRole().IsTasklessCrewmate() || pc.GetCustomRole().IsTaskBasedCrewmate() => false,
        CustomRoles.Lazy when pc.GetCustomRole().IsNeutral() || pc.GetCustomRole().IsImpostor() || (pc.GetCustomRole().IsTasklessCrewmate() && !Options.TasklessCrewCanBeLazy.GetBool()) || (pc.GetCustomRole().IsTaskBasedCrewmate() && !Options.TaskBasedCrewCanBeLazy.GetBool()) => false,
        CustomRoles.TicketsStealer or CustomRoles.Swift or CustomRoles.DeadlyQuota or CustomRoles.Damocles or CustomRoles.Mare when pc.Is(CustomRoles.Bomber) || pc.Is(CustomRoles.Nuker) || pc.Is(CustomRoles.BoobyTrap) || pc.Is(CustomRoles.Capitalism) => false,
        CustomRoles.TicketsStealer or CustomRoles.Mimic or CustomRoles.Swift or CustomRoles.DeadlyQuota or CustomRoles.Damocles or CustomRoles.Mare when !pc.GetCustomRole().IsImpostor() => false,
        CustomRoles.Torch when !pc.GetCustomRole().IsCrewmate() || pc.Is(CustomRoles.Bewilder) || pc.Is(CustomRoles.Sunglasses) || pc.Is(CustomRoles.GuardianAngelTOHE) => false,
        CustomRoles.Bewilder when pc.Is(CustomRoles.Torch) || pc.Is(CustomRoles.GuardianAngelTOHE) || pc.Is(CustomRoles.Sunglasses) => false,
        CustomRoles.Sunglasses when pc.Is(CustomRoles.Torch) || pc.Is(CustomRoles.GuardianAngelTOHE) || pc.Is(CustomRoles.Bewilder) => false,
        CustomRoles.Guesser when (pc.GetCustomRole().IsCrewmate() && (!Options.CrewCanBeGuesser.GetBool() || pc.Is(CustomRoles.NiceGuesser))) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeGuesser.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeGuesser.GetBool()) || pc.Is(CustomRoles.EvilGuesser) || pc.Is(CustomRoles.GuardianAngelTOHE) => false,
        CustomRoles.Madmate when !pc.CanBeMadmate() || pc.Is(CustomRoles.Egoist) || pc.Is(CustomRoles.Rascal) => false,
        CustomRoles.Oblivious when pc.Is(CustomRoles.Detective) || pc.Is(CustomRoles.Cleaner) || pc.Is(CustomRoles.Medusa) || pc.Is(CustomRoles.Mortician) || pc.Is(CustomRoles.Mediumshiper) || pc.Is(CustomRoles.GuardianAngelTOHE) => false,
        CustomRoles.Youtuber when !pc.GetCustomRole().IsCrewmate() || pc.Is(CustomRoles.Madmate) || pc.Is(CustomRoles.Sheriff) || pc.Is(CustomRoles.GuardianAngelTOHE) => false,
        CustomRoles.Egoist when !pc.GetCustomRole().IsImpostor() => false,
        CustomRoles.Damocles when pc.GetCustomRole() is CustomRoles.Bomber or CustomRoles.Nuker or CustomRoles.SerialKiller or CustomRoles.Cantankerous => false,
        CustomRoles.Damocles when !pc.CanUseKillButton() => false,
        CustomRoles.Flashman when pc.Is(CustomRoles.Swiftclaw) => false,
        CustomRoles.Necroview when pc.Is(CustomRoles.Visionary) => false,
        CustomRoles.Ghoul when pc.Is(CustomRoles.Lazy) => false,
        CustomRoles.Ghoul when pc.Is(CustomRoles.Needy) => false,
        CustomRoles.Lazy when pc.Is(CustomRoles.Ghoul) => false,
        CustomRoles.Mimic when pc.Is(CustomRoles.Mafia) => false,
        CustomRoles.Rascal when !pc.GetCustomRole().IsCrewmate() => false,
        CustomRoles.Needy when pc.GetCustomRole().IsAdditionRole() => false,
        CustomRoles.TicketsStealer when pc.Is(CustomRoles.Vindicator) => false,
        CustomRoles.Mare when pc.Is(CustomRoles.Underdog) => false,
        CustomRoles.Mare when pc.Is(CustomRoles.Inhibitor) => false,
        CustomRoles.Mare when pc.Is(CustomRoles.Saboteur) => false,
        CustomRoles.Mare when pc.Is(CustomRoles.Swift) => false,
        CustomRoles.Mare when pc.Is(CustomRoles.Mafia) => false,
        CustomRoles.Mare when pc.Is(CustomRoles.Sniper) => false,
        CustomRoles.Mare when pc.Is(CustomRoles.FireWorks) => false,
        CustomRoles.Mare when pc.Is(CustomRoles.Swooper) => false,
        CustomRoles.Mare when pc.Is(CustomRoles.Vampire) => false,
        CustomRoles.Torch when pc.Is(CustomRoles.Lighter) => false,
        CustomRoles.Torch when pc.Is(CustomRoles.Ignitor) => false,
        CustomRoles.Bewilder when pc.Is(CustomRoles.Lighter) => false,
        CustomRoles.Bewilder when pc.Is(CustomRoles.Ignitor) => false,
        CustomRoles.Sunglasses when pc.Is(CustomRoles.Lighter) => false,
        CustomRoles.Sunglasses when pc.Is(CustomRoles.Ignitor) => false,
        CustomRoles.Bait when pc.Is(CustomRoles.Demolitionist) => false,
        CustomRoles.Trapper when pc.Is(CustomRoles.Demolitionist) => false,
        CustomRoles.Lovers when pc.Is(CustomRoles.Romantic) => false,
        CustomRoles.Mare when pc.Is(CustomRoles.LastImpostor) => false,
        CustomRoles.Swift when pc.Is(CustomRoles.Mare) => false,
        CustomRoles.DualPersonality when (!pc.GetCustomRole().IsImpostor() && !pc.GetCustomRole().IsCrewmate()) || pc.Is(CustomRoles.Madmate) => false,
        CustomRoles.DualPersonality when pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeDualPersonality.GetBool() => false,
        CustomRoles.Loyal when (!pc.GetCustomRole().IsImpostor() && !pc.GetCustomRole().IsCrewmate()) || pc.Is(CustomRoles.Madmate) => false,
        CustomRoles.Loyal when pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeLoyal.GetBool() => false,
        CustomRoles.Seer when (pc.GetCustomRole().IsCrewmate() && (!Options.CrewCanBeSeer.GetBool() || pc.Is(CustomRoles.Mortician))) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeSeer.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeSeer.GetBool()) || pc.Is(CustomRoles.GuardianAngelTOHE) => false,
        CustomRoles.Bait when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeBait.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeBait.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeBait.GetBool()) => false,
        CustomRoles.Bewilder when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeBewilder.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeBewilder.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeBewilder.GetBool()) => false,
        CustomRoles.Sunglasses when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeSunglasses.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeSunglasses.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeSunglasses.GetBool()) => false,
        CustomRoles.Autopsy when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeAutopsy.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeAutopsy.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeAutopsy.GetBool()) => false,
        CustomRoles.Glow when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeGlow.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeGlow.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeGlow.GetBool()) => false,
        CustomRoles.Trapper when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeTrapper.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeTrapper.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeTrapper.GetBool()) => false,
        CustomRoles.Sidekick when pc.Is(CustomRoles.Madmate) => false,
        CustomRoles.Onbound when pc.Is(CustomRoles.SuperStar) => false,
        CustomRoles.Rascal when pc.Is(CustomRoles.SuperStar) || pc.Is(CustomRoles.Madmate) => false,
        CustomRoles.Madmate when pc.Is(CustomRoles.SuperStar) => false,
        CustomRoles.Gravestone when pc.Is(CustomRoles.SuperStar) => false,
        CustomRoles.Sidekick when pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeSidekick.GetBool() => false,
        CustomRoles.Sidekick when pc.GetCustomRole().IsCrewmate() && !Options.CrewmateCanBeSidekick.GetBool() => false,
        CustomRoles.Sidekick when pc.GetCustomRole().IsImpostor() && !Options.ImpostorCanBeSidekick.GetBool() => false,
        CustomRoles.Sidekick when pc.Is(CustomRoles.Jackal) => false,
        CustomRoles.Lucky when pc.Is(CustomRoles.Guardian) => false,
        CustomRoles.Bait when pc.Is(CustomRoles.GuardianAngelTOHE) => false,
        CustomRoles.Bait when pc.Is(CustomRoles.Trapper) => false,
        CustomRoles.Trapper when pc.Is(CustomRoles.Bait) => false,
        CustomRoles.DualPersonality when pc.Is(CustomRoles.Dictator) => false,
        CustomRoles.Swift when pc.Is(CustomRoles.Swooper) => false,
        CustomRoles.Swift when pc.Is(CustomRoles.Vampire) => false,
        CustomRoles.Swift when pc.Is(CustomRoles.Scavenger) => false,
        CustomRoles.Swift when pc.Is(CustomRoles.Puppeteer) => false,
        CustomRoles.Swift when pc.Is(CustomRoles.Warlock) => false,
        CustomRoles.Swift when pc.Is(CustomRoles.EvilDiviner) => false,
        CustomRoles.Swift when pc.Is(CustomRoles.Witch) => false,
        CustomRoles.Swift when pc.Is(CustomRoles.Mafia) => false,
        CustomRoles.Reach when pc.Is(CustomRoles.Mafioso) => false,
        CustomRoles.Trapper when pc.Is(CustomRoles.GuardianAngelTOHE) => false,
        CustomRoles.Reach when !pc.CanUseKillButton() => false,
        CustomRoles.Magnet when !pc.CanUseKillButton() => false,
        CustomRoles.Watcher when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeWatcher.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeWatcher.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeWatcher.GetBool()) => false,
        CustomRoles.Diseased when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeDiseased.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeDiseased.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeDiseased.GetBool()) => false,
        CustomRoles.Antidote when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeAntidote.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeAntidote.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeAntidote.GetBool()) => false,
        CustomRoles.Diseased when pc.Is(CustomRoles.Antidote) => false,
        CustomRoles.Antidote when pc.Is(CustomRoles.Diseased) => false,
        CustomRoles.Necroview when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeNecroview.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeNecroview.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeNecroview.GetBool()) => false,
        CustomRoles.Oblivious when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeOblivious.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeOblivious.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeOblivious.GetBool()) => false,
        CustomRoles.Brakar when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeTiebreaker.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeTiebreaker.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeTiebreaker.GetBool()) => false,
        CustomRoles.Guesser when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeGuesser.GetBool() && pc.Is(CustomRoles.NiceGuesser)) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeGuesser.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeGuesser.GetBool() && pc.Is(CustomRoles.EvilGuesser)) => false,
        CustomRoles.Onbound when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeOnbound.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeOnbound.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeOnbound.GetBool()) => false,
        CustomRoles.Unreportable when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeUnreportable.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeUnreportable.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeUnreportable.GetBool()) => false,
        CustomRoles.Lucky when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeLucky.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeLucky.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeLucky.GetBool()) => false,
        CustomRoles.Unlucky when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeUnlucky.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeUnlucky.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeUnlucky.GetBool()) => false,
        CustomRoles.Rogue when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeRogue.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeRogue.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeRogue.GetBool()) => false,
        CustomRoles.Gravestone when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeGravestone.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeGravestone.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeGravestone.GetBool()) => false,
        CustomRoles.Flashman or CustomRoles.Giant when pc.GetCustomRole() is CustomRoles.Swooper or CustomRoles.Wraith or CustomRoles.Chameleon or CustomRoles.Alchemist => false,
        CustomRoles.Bait when pc.Is(CustomRoles.Unreportable) => false,
        CustomRoles.Busy when !pc.GetTaskState().hasTasks => false,
        CustomRoles.Truant when pc.Is(CustomRoles.SoulHunter) => false,
        CustomRoles.Nimble when !pc.GetCustomRole().IsCrewmate() => false,
        CustomRoles.Physicist when !pc.GetCustomRole().IsCrewmate() => false,
        CustomRoles.Unreportable when pc.Is(CustomRoles.Bait) => false,
        CustomRoles.Oblivious when pc.Is(CustomRoles.Bloodhound) => false,
        CustomRoles.Oblivious when pc.Is(CustomRoles.Vulture) => false,
        CustomRoles.Vulture when pc.Is(CustomRoles.Oblivious) => false,
        CustomRoles.Guesser when pc.Is(CustomRoles.CopyCat) || pc.Is(CustomRoles.Doomsayer) => false,
        CustomRoles.CopyCat when pc.Is(CustomRoles.Guesser) => false,
        CustomRoles.DoubleShot when pc.Is(CustomRoles.CopyCat) => false,
        CustomRoles.CopyCat when pc.Is(CustomRoles.DoubleShot) => false,
        CustomRoles.Brakar when pc.Is(CustomRoles.Dictator) => false,
        CustomRoles.Lucky when pc.Is(CustomRoles.Luckey) => false,
        CustomRoles.Unlucky when pc.Is(CustomRoles.Luckey) => false,
        CustomRoles.Unlucky when pc.Is(CustomRoles.Lucky) => false,
        CustomRoles.Lucky when pc.Is(CustomRoles.Unlucky) => false,
        CustomRoles.Fool when (pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeFool.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeFool.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeFool.GetBool()) || pc.Is(CustomRoles.SabotageMaster) || pc.Is(CustomRoles.GuardianAngelTOHE) => false,
        CustomRoles.Bloodhound when pc.Is(CustomRoles.Oblivious) => false,
        CustomRoles.DoubleShot when (pc.Is(CustomRoleTypes.Impostor) && !Options.ImpCanBeDoubleShot.GetBool()) || (pc.Is(CustomRoleTypes.Crewmate) && !Options.CrewCanBeDoubleShot.GetBool()) || (pc.Is(CustomRoleTypes.Neutral) && !Options.NeutralCanBeDoubleShot.GetBool()) => false,
        CustomRoles.DoubleShot when !pc.Is(CustomRoles.EvilGuesser) && !pc.Is(CustomRoles.NiceGuesser) && !Options.GuesserMode.GetBool() => false,
        CustomRoles.DoubleShot when Options.ImpCanBeDoubleShot.GetBool() && !pc.Is(CustomRoles.Guesser) && !pc.Is(CustomRoles.EvilGuesser) && pc.Is(CustomRoleTypes.Impostor) && !Options.ImpostorsCanGuess.GetBool() => false,
        CustomRoles.DoubleShot when Options.CrewCanBeDoubleShot.GetBool() && !pc.Is(CustomRoles.Guesser) && !pc.Is(CustomRoles.NiceGuesser) && pc.Is(CustomRoleTypes.Crewmate) && !Options.CrewmatesCanGuess.GetBool() => false,
        CustomRoles.DoubleShot when Options.NeutralCanBeDoubleShot.GetBool() && !pc.Is(CustomRoles.Guesser) && ((pc.GetCustomRole().IsNonNK() && !Options.PassiveNeutralsCanGuess.GetBool()) || (pc.GetCustomRole().IsNK() && !Options.NeutralKillersCanGuess.GetBool())) => false,
        _ => true
    };
    public static Team GetTeam(this CustomRoles role)
    {
        if (role.IsImpostorTeamV3()) return Team.Impostor;
        else if (role.IsNeutralTeamV2()) return Team.Neutral;
        else if (role.IsCrewmateTeamV2()) return Team.Crewmate;
        else return Team.None;
    }
    public static bool Is(this CustomRoles role, Team team) => team switch
    {
        Team.Impostor => role.IsImpostorTeamV3(),
        Team.Neutral => role.IsNeutralTeamV2(),
        Team.Crewmate => role.IsCrewmateTeamV2(),
        Team.None => role.GetCountTypes() is CountTypes.OutOfGame or CountTypes.None || role == CustomRoles.GM,
        _ => false,
    };
    public static RoleTypes GetRoleTypes(this CustomRoles role)
        => GetVNRole(role) switch
        {
            CustomRoles.Impostor => RoleTypes.Impostor,
            CustomRoles.Scientist => RoleTypes.Scientist,
            CustomRoles.Engineer => RoleTypes.Engineer,
            CustomRoles.GuardianAngel => RoleTypes.GuardianAngel,
            CustomRoles.Shapeshifter => RoleTypes.Shapeshifter,
            CustomRoles.Crewmate => RoleTypes.Crewmate,
            _ => role.IsImpostor() ? RoleTypes.Impostor : RoleTypes.Crewmate,
        };
    public static bool IsDesyncRole(this CustomRoles role) => role.GetDYRole() != RoleTypes.GuardianAngel;
    public static bool IsImpostorTeam(this CustomRoles role) => role.IsImpostor() || role == CustomRoles.Madmate;
    public static bool IsCrewmate(this CustomRoles role) => !role.IsImpostor() && !role.IsNeutral() && !role.IsMadmate();

    public static bool IsImpostorTeamV2(this CustomRoles role) => (role.IsImpostorTeam() && role != CustomRoles.Trickster && !role.IsConverted()) || role == CustomRoles.Rascal;
    public static bool IsNeutralTeamV2(this CustomRoles role) => role.IsConverted() || (role.IsNeutral() && role != CustomRoles.Madmate);

    public static bool IsCrewmateTeamV2(this CustomRoles role) => (!role.IsImpostorTeamV2() && !role.IsNeutralTeamV2()) || (role == CustomRoles.Trickster && !role.IsConverted());

    public static bool IsConverted(this CustomRoles role) => role is
           CustomRoles.Charmed
        or CustomRoles.Recruit
        or CustomRoles.Infected
        or CustomRoles.Contagious
        or CustomRoles.Lovers
        || ((role is CustomRoles.Egoist) && (ParityCop.ParityCheckEgoistInt() == 1));
    public static bool IsRevealingRole(this CustomRoles role, PlayerControl target) =>
        ((role is CustomRoles.Mayor) && Options.MayorRevealWhenDoneTasks.GetBool() && target.AllTasksCompleted()) ||
        ((role is CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool()) ||
        ((role is CustomRoles.Marshall) && target.AllTasksCompleted()) ||
        ((role is CustomRoles.Workaholic) && Options.WorkaholicVisibleToEveryone.GetBool()) ||
        ((role is CustomRoles.Doctor) && Options.DoctorVisibleToEveryone.GetBool()) ||
        ((role is CustomRoles.Bait) && Options.BaitNotification.GetBool() && ParityCop.ParityCheckBaitCountType.GetBool());
    public static bool IsImpostorTeamV3(this CustomRoles role) => role.IsImpostor() || role.IsMadmate();
    public static bool IsNeutralKillerTeam(this CustomRoles role) => role.IsNK() && !role.IsMadmate();
    public static bool IsPassiveNeutralTeam(this CustomRoles role) => role.IsNonNK() && !role.IsMadmate();
    public static bool IsNNK(this CustomRoles role) => role.IsNeutral() && !role.IsNK();
    public static bool IsVanilla(this CustomRoles role) => role is
        CustomRoles.Crewmate or
        CustomRoles.Engineer or
        CustomRoles.Scientist or
        CustomRoles.GuardianAngel or
        CustomRoles.Impostor or
        CustomRoles.Shapeshifter;
    public static CustomRoleTypes GetCustomRoleTypes(this CustomRoles role)
    {
        CustomRoleTypes type = CustomRoleTypes.Crewmate;
        if (role.IsImpostor()) type = CustomRoleTypes.Impostor;
        if (role.IsNeutral()) type = CustomRoleTypes.Neutral;
        //  if (role.IsMadmate()) type = CustomRoleTypes.Madmate;
        if (role.IsAdditionRole()) type = CustomRoleTypes.Addon;
        return type;
    }
    public static bool RoleExist(this CustomRoles role, bool countDead = false) => Main.AllPlayerControls.Any(x => x.Is(role) && (x.IsAlive() || countDead));
    public static int GetCount(this CustomRoles role)
    {
        if (role.IsVanilla())
        {
            if (Options.DisableVanillaRoles.GetBool()) return 0;
            var roleOpt = Main.NormalOptions.RoleOptions;
            return role switch
            {
                CustomRoles.Engineer => roleOpt.GetNumPerGame(RoleTypes.Engineer),
                CustomRoles.Scientist => roleOpt.GetNumPerGame(RoleTypes.Scientist),
                CustomRoles.Shapeshifter => roleOpt.GetNumPerGame(RoleTypes.Shapeshifter),
                CustomRoles.GuardianAngel => roleOpt.GetNumPerGame(RoleTypes.GuardianAngel),
                CustomRoles.Crewmate => roleOpt.GetNumPerGame(RoleTypes.Crewmate),
                _ => 0
            };
        }
        else
        {
            return Options.GetRoleCount(role);
        }
    }
    public static int GetMode(this CustomRoles role) => Options.GetRoleSpawnMode(role);
    public static float GetChance(this CustomRoles role)
    {
        if (role.IsVanilla())
        {
            var roleOpt = Main.NormalOptions.RoleOptions;
            return role switch
            {
                CustomRoles.Engineer => roleOpt.GetChancePerGame(RoleTypes.Engineer),
                CustomRoles.Scientist => roleOpt.GetChancePerGame(RoleTypes.Scientist),
                CustomRoles.Shapeshifter => roleOpt.GetChancePerGame(RoleTypes.Shapeshifter),
                CustomRoles.GuardianAngel => roleOpt.GetChancePerGame(RoleTypes.GuardianAngel),
                CustomRoles.Crewmate => roleOpt.GetChancePerGame(RoleTypes.Crewmate),
                _ => 0
            } / 100f;
        }
        else
        {
            return Options.GetRoleChance(role);
        }
    }
    public static bool IsEnable(this CustomRoles role) => role.GetCount() > 0;
    public static CountTypes GetCountTypes(this CustomRoles role)
       => role switch
       {
           CustomRoles.GM => CountTypes.OutOfGame,
           CustomRoles.Jackal => CountTypes.Jackal,
           CustomRoles.Sidekick => CountTypes.Jackal,
           CustomRoles.Poisoner => CountTypes.Poisoner,
           CustomRoles.Doppelganger => CountTypes.Doppelganger,
           CustomRoles.Pelican => CountTypes.Pelican,
           CustomRoles.Gamer => CountTypes.Gamer,
           CustomRoles.BloodKnight => CountTypes.BloodKnight,
           CustomRoles.Bandit => CountTypes.Bandit,
           CustomRoles.Succubus => CountTypes.Succubus,
           CustomRoles.HexMaster => CountTypes.HexMaster,
           //CustomRoles.NWitch => CountTypes.NWitch,
           CustomRoles.Wraith => CountTypes.Wraith,
           CustomRoles.Pestilence => CountTypes.Pestilence,
           CustomRoles.PlagueBearer => CountTypes.PlagueBearer,
           CustomRoles.Parasite => CountTypes.Impostor,
           CustomRoles.NSerialKiller => CountTypes.NSerialKiller,
           CustomRoles.Enderman => CountTypes.Enderman,
           CustomRoles.Mycologist => CountTypes.Mycologist,
           CustomRoles.Bubble => CountTypes.Bubble,
           CustomRoles.Hookshot => CountTypes.Hookshot,
           CustomRoles.Sprayer => CountTypes.Sprayer,
           CustomRoles.PlagueDoctor => CountTypes.PlagueDoctor,
           CustomRoles.Magician => CountTypes.Magician,
           CustomRoles.WeaponMaster => CountTypes.WeaponMaster,
           CustomRoles.Reckless => CountTypes.Reckless,
           CustomRoles.Eclipse => CountTypes.Eclipse,
           CustomRoles.Pyromaniac => CountTypes.Pyromaniac,
           CustomRoles.Vengeance => CountTypes.Vengeance,
           CustomRoles.HeadHunter => CountTypes.HeadHunter,
           CustomRoles.Imitator => CountTypes.Imitator,
           CustomRoles.Werewolf => CountTypes.Werewolf,
           CustomRoles.Juggernaut => CountTypes.Juggernaut,
           CustomRoles.Jinx => CountTypes.Jinx,
           CustomRoles.Agitater => CountTypes.Agitater,
           CustomRoles.Infectious => CountTypes.Infectious,
           CustomRoles.Crewpostor => CountTypes.Impostor,
           CustomRoles.Virus => CountTypes.Virus,
           CustomRoles.Ritualist => CountTypes.Ritualist,
           CustomRoles.Pickpocket => CountTypes.Pickpocket,
           CustomRoles.Traitor => CountTypes.Traitor,
           CustomRoles.RuthlessRomantic => CountTypes.RuthlessRomantic,
           CustomRoles.Medusa => CountTypes.Medusa,
           CustomRoles.Refugee => CountTypes.Impostor,
           CustomRoles.Glitch => CountTypes.Glitch,
           // CustomRoles.Phantom => CountTypes.OutOfGame,
           //   CustomRoles.CursedSoul => CountTypes.OutOfGame, // if they count as OutOfGame, it prevents them from winning lmao

           CustomRoles.Spiritcaller => CountTypes.Spiritcaller,
           _ => role.IsImpostorTeam() ? CountTypes.Impostor : CountTypes.Crew,
       };

    public static bool HasSubRole(this PlayerControl pc) => Main.PlayerStates[pc.PlayerId].SubRoles.Count > 0;
}
public enum CustomRoleTypes
{
    Crewmate,
    Impostor,
    Neutral,
    Addon
}
public enum CountTypes
{
    OutOfGame,
    None,
    Crew,
    Impostor,
    Jackal,
    Doppelganger,
    Pelican,
    Gamer,
    BloodKnight,
    Poisoner,
    Charmed,
    Succubus,
    HexMaster,
    NWitch,
    Wraith,
    NSerialKiller,
    Enderman,
    Mycologist,
    Bubble,
    Hookshot,
    Sprayer,
    PlagueDoctor,
    Magician,
    WeaponMaster,
    Reckless,
    Pyromaniac,
    Eclipse,
    Vengeance,
    HeadHunter,
    Imitator,
    Werewolf,
    Juggernaut,
    Agitater,
    Infectious,
    Virus,
    Rogue,
    DarkHide,
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
    Arsonist
}