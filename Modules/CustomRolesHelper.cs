using AmongUs.GameOptions;
using System.Linq;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;

namespace TOHE;

internal static class CustomRolesHelper
{
    public static CustomRoles GetVNRole(this CustomRoles role)
    {
        return role.IsVanilla()
            ? role
            : role switch
            {
                CustomRoles.Sniper => CustomRoles.Shapeshifter,
                CustomRoles.Jester => Options.JesterCanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
                CustomRoles.Mayor => Options.MayorHasPortableButton.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
                CustomRoles.Vulture => Vulture.CanVent.GetBool() ? CustomRoles.Engineer : CustomRoles.Crewmate,
                CustomRoles.Opportunist => CustomRoles.Engineer,
                CustomRoles.Vindicator => CustomRoles.Impostor,
                CustomRoles.Snitch => CustomRoles.Crewmate,
                CustomRoles.Masochist => CustomRoles.Crewmate,
                CustomRoles.ParityCop => CustomRoles.Crewmate,
                CustomRoles.Marshall => CustomRoles.Crewmate,
                CustomRoles.SabotageMaster => CustomRoles.Engineer,
                CustomRoles.Mafia => Options.LegacyMafia.GetBool() ? CustomRoles.Shapeshifter : CustomRoles.Impostor,
                CustomRoles.Terrorist => CustomRoles.Engineer,
                CustomRoles.Executioner => CustomRoles.Crewmate,
                CustomRoles.Juggernaut => CustomRoles.Impostor,
                CustomRoles.Lawyer => CustomRoles.Crewmate,
                CustomRoles.Vampire => CustomRoles.Impostor,
                CustomRoles.Poisoner => CustomRoles.Impostor,
                CustomRoles.NSerialKiller => CustomRoles.Impostor,
                CustomRoles.RuthlessRomantic => CustomRoles.Impostor,
                CustomRoles.VengefulRomantic => CustomRoles.Impostor,
                CustomRoles.Romantic => CustomRoles.Impostor,
                CustomRoles.Maverick => CustomRoles.Impostor,
                CustomRoles.CursedSoul => CustomRoles.Impostor,
                CustomRoles.Parasite => CustomRoles.Impostor,
                CustomRoles.BountyHunter => CustomRoles.Shapeshifter,
                CustomRoles.Trickster => CustomRoles.Impostor,
                CustomRoles.Witch => CustomRoles.Impostor,
                CustomRoles.ShapeMaster => CustomRoles.Shapeshifter,
                CustomRoles.ShapeshifterTOHE => CustomRoles.Shapeshifter,
                CustomRoles.ImpostorTOHE => CustomRoles.Impostor,
                CustomRoles.EvilDiviner => CustomRoles.Impostor,
                CustomRoles.Ritualist => CustomRoles.Impostor,
                CustomRoles.Pickpocket => CustomRoles.Impostor,
                CustomRoles.Traitor => CustomRoles.Impostor,
                CustomRoles.HexMaster => CustomRoles.Impostor,
                CustomRoles.Wildling => Wildling.CanShapeshift.GetBool() ? CustomRoles.Shapeshifter : CustomRoles.Impostor,
                CustomRoles.Morphling => CustomRoles.Shapeshifter,
                CustomRoles.Warlock => CustomRoles.Shapeshifter,
                CustomRoles.SerialKiller => CustomRoles.Shapeshifter,
                CustomRoles.FireWorks => CustomRoles.Shapeshifter,
                CustomRoles.SpeedBooster => CustomRoles.Crewmate,
                CustomRoles.Dictator => CustomRoles.Crewmate,
         //       CustomRoles.Mare => CustomRoles.Impostor,
                CustomRoles.Inhibitor => CustomRoles.Impostor,
                CustomRoles.Saboteur => CustomRoles.Impostor,
                CustomRoles.Doctor => CustomRoles.Scientist,
                CustomRoles.ScientistTOHE => CustomRoles.Scientist,
                CustomRoles.Tracefinder => CustomRoles.Scientist,
                CustomRoles.Puppeteer => CustomRoles.Impostor,
                CustomRoles.NWitch => CustomRoles.Impostor,
                CustomRoles.TimeThief => CustomRoles.Impostor,
                CustomRoles.EvilTracker => CustomRoles.Shapeshifter,
                CustomRoles.Paranoia => CustomRoles.Engineer,
                CustomRoles.EngineerTOHE => CustomRoles.Engineer,
                CustomRoles.TimeMaster => CustomRoles.Engineer,
                CustomRoles.CrewmateTOHE => CustomRoles.Crewmate,
                CustomRoles.Miner => CustomRoles.Shapeshifter,
                CustomRoles.Psychic => CustomRoles.Crewmate,
                CustomRoles.Needy => CustomRoles.Crewmate,
                CustomRoles.Twister => CustomRoles.Shapeshifter,
                CustomRoles.SuperStar => CustomRoles.Crewmate,
                CustomRoles.Hacker => CustomRoles.Shapeshifter,
                CustomRoles.Visionary => CustomRoles.Impostor,
                CustomRoles.Assassin => CustomRoles.Shapeshifter,
                CustomRoles.Luckey => CustomRoles.Crewmate,
                CustomRoles.CyberStar => CustomRoles.Crewmate,
                CustomRoles.Demolitionist => CustomRoles.Crewmate,
                CustomRoles.NiceEraser => CustomRoles.Crewmate,
                CustomRoles.TaskManager => CustomRoles.Crewmate,
                CustomRoles.Escapee => CustomRoles.Shapeshifter,
                CustomRoles.NiceGuesser => CustomRoles.Crewmate,
                CustomRoles.EvilGuesser => CustomRoles.Impostor,
                CustomRoles.Detective => CustomRoles.Crewmate,
           //     CustomRoles.Minimalism => CustomRoles.Impostor,
                CustomRoles.God => CustomRoles.Crewmate,
                CustomRoles.GuardianAngelTOHE => CustomRoles.GuardianAngel,
                CustomRoles.Zombie => CustomRoles.Impostor,
                CustomRoles.Mario => CustomRoles.Engineer,
                CustomRoles.AntiAdminer => CustomRoles.Impostor,
                CustomRoles.Sans => CustomRoles.Impostor,
                CustomRoles.Bomber => CustomRoles.Shapeshifter,
                CustomRoles.Nuker => CustomRoles.Shapeshifter,
          //      CustomRoles.Flashbang => CustomRoles.Shapeshifter,
                CustomRoles.BoobyTrap => CustomRoles.Impostor,
                CustomRoles.Scavenger => CustomRoles.Impostor,
                CustomRoles.Transporter => CustomRoles.Crewmate,
                CustomRoles.Veteran => CustomRoles.Engineer,
                CustomRoles.Capitalism => CustomRoles.Impostor,
                CustomRoles.Bodyguard => CustomRoles.Crewmate,
                CustomRoles.Grenadier => CustomRoles.Engineer,
                CustomRoles.Lighter => CustomRoles.Engineer,
                CustomRoles.Gangster => CustomRoles.Impostor,
                CustomRoles.Cleaner => CustomRoles.Impostor,
                CustomRoles.Medusa => CustomRoles.Impostor,
                CustomRoles.Konan => CustomRoles.Crewmate,
                CustomRoles.Divinator => CustomRoles.Crewmate,
                CustomRoles.Oracle => CustomRoles.Crewmate,
                CustomRoles.BallLightning => CustomRoles.Impostor,
                CustomRoles.Greedier => CustomRoles.Impostor,
                CustomRoles.Workaholic => CustomRoles.Engineer,
                CustomRoles.CursedWolf => CustomRoles.Impostor,
                CustomRoles.Jinx => CustomRoles.Impostor,
                CustomRoles.Collector => CustomRoles.Crewmate,
                CustomRoles.Glitch => CustomRoles.Impostor,
                CustomRoles.ImperiusCurse => CustomRoles.Shapeshifter,
                CustomRoles.QuickShooter => CustomRoles.Shapeshifter,
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
                CustomRoles.Wraith => CustomRoles.Impostor,
                CustomRoles.Crewpostor => CustomRoles.Engineer,
                CustomRoles.Observer => CustomRoles.Crewmate,
                CustomRoles.DovesOfNeace => CustomRoles.Engineer,
                CustomRoles.Infectious => CustomRoles.Impostor,
                CustomRoles.Virus => CustomRoles.Virus,
                CustomRoles.Disperser => CustomRoles.Shapeshifter,
                CustomRoles.Camouflager => CustomRoles.Shapeshifter,
                CustomRoles.Dazzler => CustomRoles.Shapeshifter,
                CustomRoles.Devourer => CustomRoles.Shapeshifter,
                CustomRoles.Deathpact => CustomRoles.Shapeshifter,
             //   CustomRoles.Monarch => CustomRoles.Impostor,
                CustomRoles.Bloodhound => CustomRoles.Crewmate,
                CustomRoles.Tracker => CustomRoles.Crewmate,
                CustomRoles.Merchant => CustomRoles.Crewmate,
                CustomRoles.Retributionist => CustomRoles.Crewmate,
                CustomRoles.Guardian => CustomRoles.Crewmate,
                CustomRoles.Addict => CustomRoles.Engineer,
                CustomRoles.Chameleon => CustomRoles.Engineer,
                CustomRoles.Spiritcaller => CustomRoles.Impostor,
                CustomRoles.EvilSpirit => CustomRoles.GuardianAngel,
                CustomRoles.Lurker => CustomRoles.Impostor,
                CustomRoles.PlagueBearer => CustomRoles.Impostor,
                CustomRoles.Pestilence => CustomRoles.Impostor,
                CustomRoles.Doomsayer => CustomRoles.Crewmate,
                CustomRoles.Godfather => CustomRoles.Impostor,

                _ => role.IsImpostor() ? CustomRoles.Impostor : CustomRoles.Crewmate,
            };
    }

        public static CustomRoles GetErasedRole(this CustomRoles role)
    {
        return role.IsVanilla()
            ? role
            : role switch
            {
                CustomRoles.Tracker => CustomRoles.CrewmateTOHE,
                CustomRoles.Mayor => Options.MayorHasPortableButton.GetBool() ? CustomRoles.EngineerTOHE : CustomRoles.CrewmateTOHE,
                CustomRoles.Observer => CustomRoles.CrewmateTOHE,
                CustomRoles.DovesOfNeace => CustomRoles.EngineerTOHE,
                CustomRoles.Judge => CustomRoles.CrewmateTOHE,
                CustomRoles.Mortician => CustomRoles.CrewmateTOHE,
                CustomRoles.Mediumshiper => CustomRoles.CrewmateTOHE,
             //   CustomRoles.Glitch => CustomRoles.CrewmateTOHE,
                CustomRoles.Bodyguard => CustomRoles.CrewmateTOHE,
                CustomRoles.ParityCop => CustomRoles.CrewmateTOHE,
                CustomRoles.Grenadier => CustomRoles.EngineerTOHE,
                CustomRoles.Lighter => CustomRoles.EngineerTOHE,
                CustomRoles.Transporter => CustomRoles.CrewmateTOHE,
                CustomRoles.Veteran => CustomRoles.CrewmateTOHE,
                CustomRoles.GuardianAngelTOHE => CustomRoles.GuardianAngel,
                CustomRoles.Detective => CustomRoles.CrewmateTOHE,
                CustomRoles.NiceGuesser => CustomRoles.CrewmateTOHE,
                CustomRoles.Luckey => CustomRoles.CrewmateTOHE,
                CustomRoles.CyberStar => CustomRoles.CrewmateTOHE,
                CustomRoles.Demolitionist => CustomRoles.CrewmateTOHE,
                CustomRoles.NiceEraser => CustomRoles.CrewmateTOHE,
                CustomRoles.TaskManager => CustomRoles.CrewmateTOHE,
                CustomRoles.Psychic => CustomRoles.CrewmateTOHE,
                CustomRoles.Needy => CustomRoles.CrewmateTOHE,
                CustomRoles.SuperStar => CustomRoles.CrewmateTOHE,
                CustomRoles.Paranoia => CustomRoles.EngineerTOHE,
                CustomRoles.EngineerTOHE => CustomRoles.EngineerTOHE,
                CustomRoles.TimeMaster => CustomRoles.EngineerTOHE,
                CustomRoles.Reverie => CustomRoles.CrewmateTOHE,
                CustomRoles.CrewmateTOHE => CustomRoles.CrewmateTOHE,
                CustomRoles.Doctor => CustomRoles.ScientistTOHE,
                CustomRoles.ScientistTOHE => CustomRoles.ScientistTOHE,
                CustomRoles.Tracefinder => CustomRoles.ScientistTOHE,
                CustomRoles.SpeedBooster => CustomRoles.CrewmateTOHE,
                CustomRoles.Dictator => CustomRoles.CrewmateTOHE,
                CustomRoles.Snitch => CustomRoles.CrewmateTOHE,
                CustomRoles.Marshall => CustomRoles.CrewmateTOHE,
                CustomRoles.SabotageMaster => CustomRoles.EngineerTOHE,
                CustomRoles.Retributionist => CustomRoles.Crewmate,
                CustomRoles.Monarch => CustomRoles.CrewmateTOHE,
                CustomRoles.Deputy => CustomRoles.CrewmateTOHE,
                CustomRoles.Witness => CustomRoles.CrewmateTOHE,
                CustomRoles.Guardian => CustomRoles.CrewmateTOHE,
                CustomRoles.Addict => CustomRoles.EngineerTOHE,
                CustomRoles.Oracle => CustomRoles.EngineerTOHE,
                CustomRoles.Chameleon => CustomRoles.EngineerTOHE,
                _ => role.IsImpostor() ? CustomRoles.ImpostorTOHE : CustomRoles.CrewmateTOHE,
            };
    }

    public static RoleTypes GetDYRole(this CustomRoles role)
    {
        return role switch
        {
            //SoloKombat
            CustomRoles.KB_Normal => RoleTypes.Impostor,
            //Standard
            CustomRoles.Sheriff => RoleTypes.Impostor,
            CustomRoles.Crusader => RoleTypes.Impostor,
            //CustomRoles.Pirate => RoleTypes.Impostor,
            CustomRoles.CopyCat => RoleTypes.Impostor,
            CustomRoles.CursedSoul => RoleTypes.Impostor,
            CustomRoles.Admirer => RoleTypes.Impostor,
            CustomRoles.Refugee => RoleTypes.Impostor,
            CustomRoles.Amnesiac => RoleTypes.Impostor,
            CustomRoles.Monarch => RoleTypes.Impostor,
            CustomRoles.Deputy => RoleTypes.Impostor,
            CustomRoles.Arsonist => RoleTypes.Impostor,
            CustomRoles.Jackal => RoleTypes.Impostor,
            CustomRoles.Medusa => RoleTypes.Impostor,
            CustomRoles.Sidekick => RoleTypes.Impostor,
            CustomRoles.SwordsMan => RoleTypes.Impostor,
            CustomRoles.Reverie => RoleTypes.Impostor,
            CustomRoles.Innocent => RoleTypes.Impostor,
            CustomRoles.Pelican => RoleTypes.Impostor,
            CustomRoles.Counterfeiter => RoleTypes.Impostor,
            CustomRoles.Witness => RoleTypes.Impostor,
            CustomRoles.Pursuer => RoleTypes.Impostor,
            CustomRoles.Revolutionist => RoleTypes.Impostor,
            CustomRoles.FFF => RoleTypes.Impostor,
            CustomRoles.Medic => RoleTypes.Impostor,
            CustomRoles.Gamer => RoleTypes.Impostor,
            CustomRoles.HexMaster => RoleTypes.Impostor,
            CustomRoles.Wraith => RoleTypes.Impostor,
            CustomRoles.Glitch => RoleTypes.Impostor,
            CustomRoles.Juggernaut => RoleTypes.Impostor,
            CustomRoles.Jinx => RoleTypes.Impostor,
            CustomRoles.DarkHide => RoleTypes.Impostor,
            CustomRoles.Provocateur => RoleTypes.Impostor,
            CustomRoles.BloodKnight => RoleTypes.Impostor,
            CustomRoles.Poisoner => RoleTypes.Impostor,
            CustomRoles.NSerialKiller => RoleTypes.Impostor,
            CustomRoles.Maverick => RoleTypes.Impostor,
            CustomRoles.Parasite => RoleTypes.Impostor,
            CustomRoles.NWitch => RoleTypes.Impostor,
            CustomRoles.Totocalcio => RoleTypes.Impostor,
            CustomRoles.Romantic => RoleTypes.Impostor,
            CustomRoles.VengefulRomantic => RoleTypes.Impostor,
            CustomRoles.RuthlessRomantic => RoleTypes.Impostor,
            CustomRoles.Succubus => RoleTypes.Impostor,
            CustomRoles.Infectious => RoleTypes.Impostor,
            CustomRoles.Virus => RoleTypes.Impostor,
            CustomRoles.Farseer => RoleTypes.Impostor,
            CustomRoles.Ritualist => RoleTypes.Impostor,
            CustomRoles.Pickpocket => RoleTypes.Impostor,
            CustomRoles.Traitor => RoleTypes.Impostor,
            CustomRoles.PlagueBearer => RoleTypes.Impostor,
            CustomRoles.Pestilence => RoleTypes.Impostor,
            CustomRoles.Spiritcaller => RoleTypes.Impostor,
            _ => RoleTypes.GuardianAngel
        };
    }
    public static bool IsAdditionRole(this CustomRoles role)
    {
        return role is
            CustomRoles.Lovers or
            CustomRoles.LastImpostor or
            CustomRoles.Ntr or
            CustomRoles.Madmate or
            CustomRoles.Watcher or
            CustomRoles.Admired or
            CustomRoles.Flashman or
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
    }
    public static bool IsNonNK(this CustomRoles role) // ROLE ASSIGNING, NOT NEUTRAL TYPE
    {
        return role is
            CustomRoles.Jester or
            CustomRoles.Terrorist or
            CustomRoles.Opportunist or
            CustomRoles.Masochist or
            CustomRoles.Executioner or
            CustomRoles.Mario or
            CustomRoles.Crewpostor or
            CustomRoles.Lawyer or
            CustomRoles.God or
            CustomRoles.Amnesiac or
            CustomRoles.Innocent or
            CustomRoles.Vulture or
            CustomRoles.NWitch or
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
            CustomRoles.CursedSoul or
            CustomRoles.Phantom or
            CustomRoles.Totocalcio or
            CustomRoles.Romantic or
            CustomRoles.VengefulRomantic or
            CustomRoles.Doomsayer or
            CustomRoles.Succubus;
    }

    public static bool IsAmneMaverick(this CustomRoles role) // ROLE ASSIGNING, NOT NEUTRAL TYPE
    {
        return role is
            CustomRoles.Jester or
            CustomRoles.Terrorist or
            CustomRoles.Opportunist or
            CustomRoles.Masochist or
            CustomRoles.Executioner or
            CustomRoles.Mario or
            CustomRoles.Crewpostor or
            CustomRoles.Lawyer or
            CustomRoles.God or
            CustomRoles.Amnesiac or
            CustomRoles.Pestilence or
            CustomRoles.PlagueBearer or
            CustomRoles.Innocent or
            CustomRoles.Vulture or
            CustomRoles.NWitch or
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
            CustomRoles.CursedSoul or
            CustomRoles.Phantom or
            CustomRoles.DarkHide or
            CustomRoles.Ritualist or
            CustomRoles.Doomsayer or
            //CustomRoles.Pirate or
       //     CustomRoles.Juggernaut or
            CustomRoles.Jinx or
            CustomRoles.Romantic or
            //     CustomRoles.Poisoner or
            //     CustomRoles.HexMaster or
            CustomRoles.Totocalcio;
    }
    public static bool IsAmneNK(this CustomRoles role)
    {
        return role is
            CustomRoles.Sidekick or
            CustomRoles.Infectious or
            CustomRoles.Glitch or
            CustomRoles.Wraith or
            CustomRoles.Medusa or
            CustomRoles.Pelican or
            CustomRoles.Refugee or
            CustomRoles.Parasite or
            CustomRoles.NSerialKiller or
            CustomRoles.Pickpocket or
            CustomRoles.Traitor or
            CustomRoles.Virus or
            CustomRoles.Spiritcaller or
            CustomRoles.Succubus;
    }

    public static bool IsNK(this CustomRoles role)
    {
        return role is
            CustomRoles.Jackal or
            CustomRoles.Glitch or
            CustomRoles.Sidekick or
            CustomRoles.HexMaster or
            CustomRoles.Infectious or
            CustomRoles.Wraith or
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
            CustomRoles.Ritualist or
            CustomRoles.Arsonist or
            CustomRoles.Pickpocket or
            CustomRoles.Traitor or
            CustomRoles.Virus or
            CustomRoles.BloodKnight or
            CustomRoles.Spiritcaller or
            CustomRoles.RuthlessRomantic or
            CustomRoles.PlagueBearer or
            CustomRoles.Pestilence;
    }
    public static bool IsSnitchTarget(this CustomRoles role)
    {
        return role is
            CustomRoles.Jackal or
            CustomRoles.Sidekick or
            CustomRoles.HexMaster or
            CustomRoles.Refugee or
            CustomRoles.Infectious or
            CustomRoles.Wraith or
            CustomRoles.Crewpostor or
            CustomRoles.Juggernaut or
            CustomRoles.Jinx or
            CustomRoles.DarkHide or
            CustomRoles.Poisoner or
            CustomRoles.Parasite or
            CustomRoles.NSerialKiller or
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
    }
    public static bool IsNE(this CustomRoles role)
    {
        return role is
            CustomRoles.Jester or
            CustomRoles.Gamer or
            CustomRoles.Masochist or
            CustomRoles.Arsonist or
            CustomRoles.Executioner or
            CustomRoles.Doomsayer or
            CustomRoles.Innocent;
    }
    public static bool IsNB(this CustomRoles role)
    {
        return role is
            CustomRoles.Opportunist or
            CustomRoles.Lawyer or
            CustomRoles.Amnesiac or
            CustomRoles.NWitch or
            CustomRoles.God or
            CustomRoles.Pursuer or
            CustomRoles.FFF or
            CustomRoles.Sunnyboy or
            CustomRoles.Maverick or
            CustomRoles.Romantic or
            CustomRoles.VengefulRomantic or
            CustomRoles.Totocalcio;
    }
    public static bool IsNC(this CustomRoles role)
    {
        return role is
            CustomRoles.Mario or
            CustomRoles.Terrorist or
            //CustomRoles.Pirate or
            CustomRoles.Revolutionist or
            CustomRoles.Vulture or
            CustomRoles.Famine or
            CustomRoles.Baker or
            CustomRoles.CursedSoul or
            CustomRoles.Phantom or
            CustomRoles.Workaholic or
            CustomRoles.Collector or
            CustomRoles.Provocateur;
    }
    public static bool IsNeutralKilling(this CustomRoles role)
    {
        return role is
            CustomRoles.Terrorist or
            CustomRoles.Arsonist or
            CustomRoles.Juggernaut or
            CustomRoles.Medusa or
            CustomRoles.Jinx or
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
            CustomRoles.Ritualist or
            CustomRoles.Pickpocket or
            CustomRoles.Traitor or
            CustomRoles.BloodKnight or
            CustomRoles.Infectious or
            CustomRoles.Virus or
            CustomRoles.Succubus or
            CustomRoles.Spiritcaller or
            CustomRoles.RuthlessRomantic or
            CustomRoles.PlagueBearer or
            CustomRoles.Pestilence;
    }
    public static bool IsCK(this CustomRoles role)
    {
        return role is
            CustomRoles.SwordsMan or
            CustomRoles.Veteran or
            CustomRoles.CopyCat or
            CustomRoles.Bodyguard or
            CustomRoles.Reverie or
            CustomRoles.Crusader or
            CustomRoles.NiceGuesser or
            CustomRoles.Counterfeiter or
            CustomRoles.Retributionist or
            CustomRoles.Sheriff;
    }
    public static bool IsImpostor(this CustomRoles role) // IsImp
    {
        return role is
            CustomRoles.Impostor or
            CustomRoles.Godfather or
            CustomRoles.Shapeshifter or
            CustomRoles.ShapeshifterTOHE or
            CustomRoles.ImpostorTOHE or
            CustomRoles.EvilDiviner or
            CustomRoles.Wildling or
            CustomRoles.Morphling or
            CustomRoles.BountyHunter or
            CustomRoles.Vampire or
            CustomRoles.Witch or
            CustomRoles.Vindicator or
            CustomRoles.ShapeMaster or
            CustomRoles.Zombie or
            CustomRoles.Warlock or
            CustomRoles.Assassin or
            CustomRoles.Hacker or
            CustomRoles.Visionary or
            CustomRoles.Miner or
            CustomRoles.Escapee or
            CustomRoles.SerialKiller or
            CustomRoles.Underdog or
            CustomRoles.Inhibitor or
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
    }
    public static bool IsNeutral(this CustomRoles role)
    {
        return role is
            //SoloKombat
            CustomRoles.KB_Normal or
            //Standard
            CustomRoles.Jester or
            CustomRoles.Opportunist or
            CustomRoles.Mario or
            CustomRoles.Masochist or
            CustomRoles.Amnesiac or
            CustomRoles.Medusa or
            CustomRoles.Famine or
            CustomRoles.Baker or
            CustomRoles.HexMaster or
            CustomRoles.Glitch or
            CustomRoles.Crewpostor or
            CustomRoles.NWitch or
            CustomRoles.Wraith or
            CustomRoles.Vulture or
            CustomRoles.Parasite or
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
            CustomRoles.Ritualist or
            CustomRoles.Pickpocket or
            CustomRoles.Pelican or
            CustomRoles.Traitor or
            CustomRoles.Revolutionist or
            CustomRoles.FFF or
            CustomRoles.Konan or
            CustomRoles.Gamer or
            CustomRoles.Maverick or
            CustomRoles.CursedSoul or
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
    }

        public static bool IsAbleToBeSidekicked(this CustomRoles role)
    {
        return role is
        CustomRoles.BloodKnight or
        CustomRoles.Virus or
            CustomRoles.Medusa or
        CustomRoles.NSerialKiller or
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
        CustomRoles.Reverie or
        CustomRoles.Arsonist or
        CustomRoles.Revolutionist or
        CustomRoles.Maverick or
        CustomRoles.NWitch or
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
        CustomRoles.Counterfeiter or
        CustomRoles.Witness or
        CustomRoles.Totocalcio or
        CustomRoles.Farseer or
        CustomRoles.FFF or
        CustomRoles.SwordsMan or
        CustomRoles.CursedSoul or
        CustomRoles.Admirer or
        CustomRoles.Refugee or
        CustomRoles.Amnesiac or
        CustomRoles.Monarch or
        CustomRoles.Parasite or
        CustomRoles.PlagueBearer;

    }

    public static bool IsNeutralWithGuessAccess(this CustomRoles role)
    {
        return role is
            //SoloKombat
            CustomRoles.KB_Normal or
            //Standard
            CustomRoles.Jester or
            CustomRoles.Opportunist or
            CustomRoles.Mario or
            CustomRoles.HexMaster or
            CustomRoles.Crewpostor or
            CustomRoles.NWitch or
            CustomRoles.Wraith or
            CustomRoles.Parasite or
            CustomRoles.Terrorist or
            CustomRoles.Executioner or
            CustomRoles.Medusa or
            CustomRoles.Juggernaut or
            CustomRoles.Vulture or
            CustomRoles.Jinx or
            CustomRoles.Lawyer or
            CustomRoles.Arsonist or
            CustomRoles.Jackal or
            CustomRoles.Sidekick or
            CustomRoles.God or
            CustomRoles.Innocent or
            CustomRoles.Pursuer or
        //    CustomRoles.Sidekick or
            CustomRoles.Poisoner or
            CustomRoles.NSerialKiller or
            CustomRoles.Pelican or
            CustomRoles.Revolutionist or
            CustomRoles.FFF or
            CustomRoles.Traitor or
            CustomRoles.Konan or
            CustomRoles.Gamer or
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
            CustomRoles.VengefulRomantic or
            CustomRoles.RuthlessRomantic or
            CustomRoles.Virus or
            CustomRoles.Succubus or
            CustomRoles.Spiritcaller or
            CustomRoles.Doomsayer or
            CustomRoles.PlagueBearer or
            //CustomRoles.Pirate or
            CustomRoles.Pestilence;
    }

    public static bool IsEvilAddons(this CustomRoles role)
    {
        return role is
        CustomRoles.Madmate or
        CustomRoles.Egoist or
        CustomRoles.Charmed or
        CustomRoles.Recruit or
        CustomRoles.Infected or
        CustomRoles.Contagious or
        CustomRoles.Rogue or
        CustomRoles.Rascal or
        CustomRoles.Soulless;
    }

    public static bool IsMadmate(this CustomRoles role)
    {
        return role is
        CustomRoles.Crewpostor or
        CustomRoles.Convict or
        CustomRoles.Refugee or
        CustomRoles.Parasite;
    }
    public static bool IsTasklessCrewmate(this CustomRoles role)
    {
        return role is
        CustomRoles.Sheriff or
        CustomRoles.Medic or
        CustomRoles.CopyCat or
        CustomRoles.Reverie or
        CustomRoles.Crusader or
        CustomRoles.Counterfeiter or
        CustomRoles.Witness or
        CustomRoles.Monarch or
        CustomRoles.Farseer or
        CustomRoles.SwordsMan or
        CustomRoles.Deputy;
    }
    public static bool IsTaskBasedCrewmate(this CustomRoles role)
    {
        return role is
        CustomRoles.Snitch or
        CustomRoles.Divinator or
        CustomRoles.Marshall or
        CustomRoles.TimeManager or
        CustomRoles.Guardian or
        CustomRoles.Merchant or
        CustomRoles.Mayor or
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
            CustomRoles.TicketsStealer;
    }
    public static bool CheckAddonConfilct(CustomRoles role, PlayerControl pc)
    {
        if (!role.IsAdditionRole()) return false;

        if (pc.Is(CustomRoles.GM) || pc.Is(CustomRoles.Lovers) || (pc.HasSubRole() && pc.GetCustomSubRoles().Count >= Options.NoLimitAddonsNumMax.GetInt())) return false;
        if (role is CustomRoles.Torch && (!pc.GetCustomRole().IsCrewmate() || pc.Is(CustomRoles.Bewilder) || pc.Is(CustomRoles.Sunglasses) || pc.Is(CustomRoles.GuardianAngelTOHE))) return false;
        if (role is CustomRoles.Bewilder && (pc.Is(CustomRoles.Torch) || pc.Is(CustomRoles.GuardianAngelTOHE) || pc.Is(CustomRoles.Sunglasses))) return false;
        if (role is CustomRoles.Sunglasses && (pc.Is(CustomRoles.Torch) || pc.Is(CustomRoles.GuardianAngelTOHE) || pc.Is(CustomRoles.Bewilder))) return false;
        if (role is CustomRoles.Ntr && (pc.Is(CustomRoles.Lovers) || pc.Is(CustomRoles.FFF) || pc.Is(CustomRoles.GuardianAngelTOHE))) return false;
    //    if (role is CustomRoles.Lovers && pc.Is(CustomRoles.FFF)) return false;
    //    if (role is CustomRoles.Lovers && pc.Is(CustomRoles.Bomber)) return false;
        if (role is CustomRoles.Guesser && ((pc.GetCustomRole().IsCrewmate() && (!Options.CrewCanBeGuesser.GetBool() || pc.Is(CustomRoles.NiceGuesser) || pc.Is(CustomRoles.Judge))) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeGuesser.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeGuesser.GetBool() || pc.Is(CustomRoles.EvilGuesser)) || pc.Is(CustomRoles.Mafia) || pc.Is(CustomRoles.Councillor) || pc.Is(CustomRoles.GuardianAngelTOHE))) return false;
        if (role is CustomRoles.Madmate && (!Utils.CanBeMadmate(pc))) return false;
        if (role is CustomRoles.Oblivious && (pc.Is(CustomRoles.Detective) || pc.Is(CustomRoles.Cleaner) || pc.Is(CustomRoles.Medusa) || pc.Is(CustomRoles.Mortician) || pc.Is(CustomRoles.Mediumshiper) || pc.Is(CustomRoles.GuardianAngelTOHE))) return false;
        if (role is CustomRoles.Avanger && pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeAvanger.GetBool() || pc.Is(CustomRoles.GuardianAngelTOHE)) return false;
        if (role is CustomRoles.Brakar && pc.Is(CustomRoles.Dictator) || pc.Is(CustomRoles.GuardianAngelTOHE)) return false;
        if (role is CustomRoles.Youtuber && (!pc.GetCustomRole().IsCrewmate() || pc.Is(CustomRoles.Madmate) || pc.Is(CustomRoles.Sheriff) || pc.Is(CustomRoles.GuardianAngelTOHE))) return false;
        if (role is CustomRoles.Egoist && (pc.GetCustomRole().IsNeutral() || pc.Is(CustomRoles.Madmate))) return false;
        if (role is CustomRoles.Lazy && (pc.GetCustomRole().IsNeutral() || pc.GetCustomRole().IsImpostor() || pc.GetCustomRole().IsTasklessCrewmate() && !Options.TasklessCrewCanBeLazy.GetBool() || pc.GetCustomRole().IsTaskBasedCrewmate() && !Options.TaskBasedCrewCanBeLazy.GetBool()) || pc.Is(CustomRoles.Needy) || pc.Is(CustomRoles.Snitch) || pc.Is(CustomRoles.Marshall) || pc.Is(CustomRoles.Transporter) || pc.Is(CustomRoles.Guardian)) return false;
        if (role is CustomRoles.Ghoul && (pc.GetCustomRole().IsNeutral() || pc.GetCustomRole().IsImpostor() || pc.GetCustomRole().IsTasklessCrewmate() || pc.GetCustomRole().IsTaskBasedCrewmate()) || pc.Is(CustomRoles.Needy) || pc.Is(CustomRoles.Snitch) || pc.Is(CustomRoles.Marshall) || pc.Is(CustomRoles.Transporter) || pc.Is(CustomRoles.Guardian)) return false;
        if (role is CustomRoles.Egoist && pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeEgoist.GetBool()) return false;
        if (role is CustomRoles.Egoist && pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeEgoist.GetBool() || pc.Is(CustomRoles.GuardianAngelTOHE)) return false;
        if (role is CustomRoles.TicketsStealer or CustomRoles.Mimic or CustomRoles.Swift or CustomRoles.Mare && !pc.GetCustomRole().IsImpostor()) return false;
        if (role is CustomRoles.TicketsStealer or CustomRoles.Swift or CustomRoles.Mare && (pc.Is(CustomRoles.Bomber) || pc.Is(CustomRoles.Nuker) || pc.Is(CustomRoles.BoobyTrap) || pc.Is(CustomRoles.Capitalism))) return false;
        if (role is CustomRoles.Necroview && pc.Is(CustomRoles.Visionary)) return false;
        if (role is CustomRoles.Ghoul && pc.Is(CustomRoles.Lazy)) return false;
        if (role is CustomRoles.Lazy && pc.Is(CustomRoles.Ghoul)) return false;
        if (role is CustomRoles.Ghoul && pc.Is(CustomRoles.Needy)) return false;
        if (role is CustomRoles.Mimic && pc.Is(CustomRoles.Mafia)) return false;
        if (role is CustomRoles.Rascal && !pc.GetCustomRole().IsCrewmate()) return false;
        if (role is CustomRoles.Needy && pc.GetCustomRole().IsAdditionRole()) return false;
        if (role is CustomRoles.TicketsStealer && pc.Is(CustomRoles.Vindicator)) return false;
        if (role is CustomRoles.Mare && pc.Is(CustomRoles.Underdog)) return false;
        if (role is CustomRoles.Mare && pc.Is(CustomRoles.Inhibitor)) return false;
        if (role is CustomRoles.Mare && pc.Is(CustomRoles.Saboteur)) return false;
        if (role is CustomRoles.Mare && pc.Is(CustomRoles.Swift)) return false;
        if (role is CustomRoles.Mare && pc.Is(CustomRoles.Mafia)) return false;
        if (role is CustomRoles.Mare && pc.Is(CustomRoles.Sniper)) return false;
        if (role is CustomRoles.Mare && pc.Is(CustomRoles.FireWorks)) return false;
        if (role is CustomRoles.Mare && pc.Is(CustomRoles.Swooper)) return false;
        if (role is CustomRoles.Mare && pc.Is(CustomRoles.Vampire)) return false;
        if (role is CustomRoles.Mare && pc.Is(CustomRoles.Sans)) return false;
        if (role is CustomRoles.Torch && pc.Is(CustomRoles.Lighter)) return false;
        if (role is CustomRoles.Bewilder && pc.Is(CustomRoles.Lighter)) return false;
        if (role is CustomRoles.Sunglasses && pc.Is(CustomRoles.Lighter)) return false;
        if (role is CustomRoles.Bait && pc.Is(CustomRoles.Demolitionist)) return false;
        if (role is CustomRoles.Trapper && pc.Is(CustomRoles.Demolitionist)) return false;
        if (role is CustomRoles.Lovers && pc.Is(CustomRoles.Romantic)) return false;
        if (role is CustomRoles.Ntr && pc.Is(CustomRoles.Romantic)) return false;
        if (role is CustomRoles.Mare && pc.Is(CustomRoles.LastImpostor)) return false;
        if (role is CustomRoles.Swift && pc.Is(CustomRoles.Mare)) return false;
        if (role is CustomRoles.DualPersonality && ((!pc.GetCustomRole().IsImpostor() && !pc.GetCustomRole().IsCrewmate()) || pc.Is(CustomRoles.Madmate))) return false;
        if (role is CustomRoles.DualPersonality && pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeDualPersonality.GetBool()) return false;
        if (role is CustomRoles.DualPersonality && pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeDualPersonality.GetBool() || pc.Is(CustomRoles.GuardianAngelTOHE)) return false;
        if (role is CustomRoles.Loyal && ((!pc.GetCustomRole().IsImpostor() && !pc.GetCustomRole().IsCrewmate()) || pc.Is(CustomRoles.Madmate))) return false;
        if (role is CustomRoles.Loyal && pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeLoyal.GetBool()) return false;
        if (role is CustomRoles.Loyal && pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeLoyal.GetBool() || pc.Is(CustomRoles.GuardianAngelTOHE)) return false;
        if (role is CustomRoles.Seer && ((pc.GetCustomRole().IsCrewmate() && (!Options.CrewCanBeSeer.GetBool() || pc.Is(CustomRoles.Mortician))) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeSeer.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeSeer.GetBool()) || pc.Is(CustomRoles.GuardianAngelTOHE))) return false;
        if (role is CustomRoles.Necroview && pc.Is(CustomRoles.Doctor) || pc.Is(CustomRoles.God) || pc.Is(CustomRoles.GuardianAngelTOHE)) return false;
        if (role is CustomRoles.Bait && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeBait.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeBait.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeBait.GetBool()))) return false;
        if (role is CustomRoles.Bewilder && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeBewilder.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeBewilder.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeBewilder.GetBool()))) return false;
        if (role is CustomRoles.Sunglasses && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeSunglasses.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeSunglasses.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeSunglasses.GetBool()))) return false;
        if (role is CustomRoles.Autopsy && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeAutopsy.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeAutopsy.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeAutopsy.GetBool()))) return false;
        if (role is CustomRoles.Glow && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeGlow.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeGlow.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeGlow.GetBool()))) return false;
        if (role is CustomRoles.Trapper && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeTrapper.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeTrapper.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeTrapper.GetBool()))) return false;
        if (role is CustomRoles.Sidekick && (pc.Is(CustomRoles.Madmate))) return false;
        if (role is CustomRoles.Onbound && (pc.Is(CustomRoles.SuperStar))) return false;
        if (role is CustomRoles.Rascal && (pc.Is(CustomRoles.SuperStar))) return false;
        if (role is CustomRoles.Madmate && (pc.Is(CustomRoles.SuperStar))) return false;
        if (role is CustomRoles.Gravestone && (pc.Is(CustomRoles.SuperStar))) return false;
//        if (role is CustomRoles.SuperStar && (pc.Is(CustomRoles.Madmate) || pc.Is(CustomRoles.Rascal) || pc.Is(CustomRoles.Onbound))) return false;
        if (role is CustomRoles.Autopsy && (pc.Is(CustomRoles.Doctor)) || pc.Is(CustomRoles.Tracefinder) || pc.Is(CustomRoles.Scientist)  || pc.Is(CustomRoles.ScientistTOHE)  || pc.Is(CustomRoles.Sunnyboy) ) return false;
        if (role is CustomRoles.Sidekick && pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeSidekick.GetBool()) return false;
        if (role is CustomRoles.Sidekick && pc.GetCustomRole().IsCrewmate() && !Options.CrewmateCanBeSidekick.GetBool()) return false;
        if (role is CustomRoles.Sidekick && pc.GetCustomRole().IsImpostor() && !Options.ImpostorCanBeSidekick.GetBool()) return false;
        if (role is CustomRoles.Madmate && pc.Is(CustomRoles.Sidekick) || pc.Is(CustomRoles.Egoist)) return false;
        if (role is CustomRoles.Sidekick && pc.Is(CustomRoles.Madmate) || pc.Is(CustomRoles.Egoist)) return false;
        if (role is CustomRoles.Egoist && pc.Is(CustomRoles.Sidekick) || pc.Is(CustomRoles.Madmate)) return false;
        if (role is CustomRoles.Sidekick && pc.Is(CustomRoles.Jackal)) return false;
        if (role is CustomRoles.Lucky && pc.Is(CustomRoles.Guardian)) return false;
        if (role is CustomRoles.Bait && pc.Is(CustomRoles.GuardianAngelTOHE)) return false;
        if (role is CustomRoles.Bait && pc.Is(CustomRoles.Trapper)) return false;
        if (role is CustomRoles.Trapper && pc.Is(CustomRoles.Bait)) return false;
        if (role is CustomRoles.DualPersonality && pc.Is(CustomRoles.Dictator)) return false;
        if (role is CustomRoles.Swift && pc.Is(CustomRoles.Swooper)) return false;
        if (role is CustomRoles.Swift && pc.Is(CustomRoles.Vampire)) return false;
        if (role is CustomRoles.Swift && pc.Is(CustomRoles.Scavenger)) return false;
        if (role is CustomRoles.Swift && pc.Is(CustomRoles.Puppeteer)) return false;
        if (role is CustomRoles.Swift && pc.Is(CustomRoles.Warlock)) return false;
        if (role is CustomRoles.Swift && pc.Is(CustomRoles.EvilDiviner)) return false;
        if (role is CustomRoles.Swift && pc.Is(CustomRoles.Witch)) return false;
        if (role is CustomRoles.Swift && pc.Is(CustomRoles.Mafia)) return false;
        if (role is CustomRoles.Trapper && pc.Is(CustomRoles.GuardianAngelTOHE)) return false;
        if (role is CustomRoles.Reach && !pc.CanUseKillButton()) return false;
        if (role is CustomRoles.Watcher && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeWatcher.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeWatcher.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeWatcher.GetBool()))) return false;
        if (role is CustomRoles.Diseased && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeDiseased.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeDiseased.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeDiseased.GetBool()))) return false;
        if (role is CustomRoles.Antidote && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeAntidote.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeAntidote.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeAntidote.GetBool()))) return false;
        if (role is CustomRoles.Diseased && pc.Is(CustomRoles.Antidote)) return false;
        if (role is CustomRoles.Antidote && pc.Is(CustomRoles.Diseased)) return false;

        if (role is CustomRoles.Necroview && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeNecroview.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeNecroview.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeNecroview.GetBool()))) return false;
        if (role is CustomRoles.Oblivious && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeOblivious.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeOblivious.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeOblivious.GetBool()))) return false;
        if (role is CustomRoles.Brakar && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeTiebreaker.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeTiebreaker.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeTiebreaker.GetBool()))) return false;
        if (role is CustomRoles.Guesser && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeGuesser.GetBool() && pc.Is(CustomRoles.NiceGuesser)) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeGuesser.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeGuesser.GetBool() && pc.Is(CustomRoles.EvilGuesser)))) return false;
        if (role is CustomRoles.Onbound && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeOnbound.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeOnbound.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeOnbound.GetBool()))) return false;
  //      if (role is CustomRoles.Lovers && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeInLove.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeInLove.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeInLove.GetBool()))) return false;
    //    if (role is CustomRoles.Reflective && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeReflective.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeReflective.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeReflective.GetBool()))) return false;
        if (role is CustomRoles.Unreportable && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeUnreportable.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeUnreportable.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeUnreportable.GetBool()))) return false;
        if (role is CustomRoles.Lucky && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeLucky.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeLucky.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeLucky.GetBool()))) return false;
        if (role is CustomRoles.Unlucky && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeUnlucky.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeUnlucky.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeUnlucky.GetBool()))) return false;
        if (role is CustomRoles.Rogue && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeRogue.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeRogue.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeRogue.GetBool()))) return false;
        if (role is CustomRoles.Gravestone && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeGravestone.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeGravestone.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeGravestone.GetBool()))) return false;
        if (role is CustomRoles.Flashman && pc.Is(CustomRoles.Swooper)) return false;
    //    if (role is CustomRoles.Lovers && pc.Is(CustomRoles.Dictator)) return false;
     //   if (role is CustomRoles.Lovers && pc.Is(CustomRoles.Dictator)) return false;
        if (role is CustomRoles.Bait && pc.Is(CustomRoles.Unreportable)) return false;
    //    if (role is CustomRoles.Onbound && pc.Is(CustomRoles.Reflective)) return false;
    //    if (role is CustomRoles.Reflective && pc.Is(CustomRoles.Onbound)) return false;
        if (role is CustomRoles.Unreportable && pc.Is(CustomRoles.Bait)) return false;
     //   if (role is CustomRoles.Lovers && pc.Is(CustomRoles.Provocateur)) return false;
        if (role is CustomRoles.Oblivious && pc.Is(CustomRoles.Bloodhound)) return false;
        if (role is CustomRoles.Oblivious && pc.Is(CustomRoles.Vulture)) return false;
        if (role is CustomRoles.Vulture && pc.Is(CustomRoles.Oblivious)) return false;
        if (role is CustomRoles.Guesser && (pc.Is(CustomRoles.CopyCat) || pc.Is(CustomRoles.Doomsayer))) return false;
        if (role is CustomRoles.CopyCat && pc.Is(CustomRoles.Guesser)) return false;
        if (role is CustomRoles.DoubleShot && pc.Is(CustomRoles.CopyCat)) return false;
        if (role is CustomRoles.CopyCat && pc.Is(CustomRoles.DoubleShot)) return false;

        if (role is CustomRoles.Brakar && pc.Is(CustomRoles.Dictator)) return false;
        if (role is CustomRoles.Lucky && pc.Is(CustomRoles.Luckey)) return false;
        if (role is CustomRoles.Unlucky && pc.Is(CustomRoles.Luckey)) return false;
        if (role is CustomRoles.Unlucky && pc.Is(CustomRoles.Lucky)) return false;
        if (role is CustomRoles.Lucky && pc.Is(CustomRoles.Unlucky)) return false;
        if (role is CustomRoles.Fool && ((pc.GetCustomRole().IsCrewmate() && !Options.CrewCanBeFool.GetBool()) || (pc.GetCustomRole().IsNeutral() && !Options.NeutralCanBeFool.GetBool()) || (pc.GetCustomRole().IsImpostor() && !Options.ImpCanBeFool.GetBool()) || pc.Is(CustomRoles.SabotageMaster) || pc.Is(CustomRoles.GuardianAngelTOHE))) return false;
        if (role is CustomRoles.Bloodhound && pc.Is(CustomRoles.Oblivious)) return false;
        if (role is CustomRoles.DoubleShot && ((pc.Is(CustomRoleTypes.Impostor) && !Options.ImpCanBeDoubleShot.GetBool()) || (pc.Is(CustomRoleTypes.Crewmate) && !Options.CrewCanBeDoubleShot.GetBool()) || (pc.Is(CustomRoleTypes.Neutral) && !Options.NeutralCanBeDoubleShot.GetBool()))) return false;
        if (role is CustomRoles.DoubleShot && (!pc.Is(CustomRoles.EvilGuesser) && !pc.Is(CustomRoles.NiceGuesser) && !pc.Is(CustomRoles.Guesser) && !Options.GuesserMode.GetBool())) return false;
        if (role is CustomRoles.DoubleShot && Options.ImpCanBeDoubleShot.GetBool() && !pc.Is(CustomRoles.Guesser) && !pc.Is(CustomRoles.EvilGuesser) && (pc.Is(CustomRoleTypes.Impostor) && !Options.ImpostorsCanGuess.GetBool())) return false;
        if (role is CustomRoles.DoubleShot && Options.CrewCanBeDoubleShot.GetBool() && !pc.Is(CustomRoles.Guesser) && !pc.Is(CustomRoles.NiceGuesser) && (pc.Is(CustomRoleTypes.Crewmate) && !Options.CrewmatesCanGuess.GetBool())) return false;
        if (role is CustomRoles.DoubleShot && Options.NeutralCanBeDoubleShot.GetBool() && !pc.Is(CustomRoles.Guesser) && ((pc.GetCustomRole().IsNonNK() && !Options.PassiveNeutralsCanGuess.GetBool()) || (pc.GetCustomRole().IsNK() && !Options.NeutralKillersCanGuess.GetBool()))) return false;
        return true;
    }
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
    public static bool IsNeutralTeamV2(this CustomRoles role) => (role.IsConverted() || role.IsNeutral() && role != CustomRoles.Madmate);

    public static bool IsCrewmateTeamV2(this CustomRoles role) => ((!role.IsImpostorTeamV2() && !role.IsNeutralTeamV2()) || (role == CustomRoles.Trickster && !role.IsConverted()));

    public static bool IsConverted(this CustomRoles role)
    {

        return (role is CustomRoles.Charmed ||
                role is CustomRoles.Recruit ||
                role is CustomRoles.Infected ||
                role is CustomRoles.Contagious ||
                role is CustomRoles.Lovers ||
                ((role is CustomRoles.Egoist) && (ParityCop.ParityCheckEgoistInt() == 1)));
    }
    public static bool IsRevealingRole(this CustomRoles role, PlayerControl target)
    {
        return (((role is CustomRoles.Mayor) && (Options.MayorRevealWhenDoneTasks.GetBool()) && target.AllTasksCompleted()) ||
             ((role is CustomRoles.SuperStar) && (Options.EveryOneKnowSuperStar.GetBool())) ||
            ((role is CustomRoles.Marshall) && target.AllTasksCompleted()) ||
            ((role is CustomRoles.Workaholic) && (Options.WorkaholicVisibleToEveryone.GetBool())) ||
            ((role is CustomRoles.Doctor) && (Options.DoctorVisibleToEveryone.GetBool())) ||
            ((role is CustomRoles.Bait) && (Options.BaitNotification.GetBool()) && ParityCop.ParityCheckBaitCountType.GetBool()));
    }
    public static bool IsImpostorTeamV3(this CustomRoles role) => (role.IsImpostor() || role.IsMadmate());
    public static bool IsNeutralKillerTeam(this CustomRoles role) => (role.IsNK() || !role.IsMadmate());
    public static bool IsPassiveNeutralTeam(this CustomRoles role) => (role.IsNonNK() || !role.IsMadmate());
    public static bool IsNNK(this CustomRoles role) => role.IsNeutral() && !role.IsNK();
    public static bool IsVanilla(this CustomRoles role)
    {
        return role is
            CustomRoles.Crewmate or
            CustomRoles.Engineer or
            CustomRoles.Scientist or
            CustomRoles.GuardianAngel or
            CustomRoles.Impostor or
            CustomRoles.Shapeshifter;
    }
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
           CustomRoles.Pelican => CountTypes.Pelican,
           CustomRoles.Gamer => CountTypes.Gamer,
           CustomRoles.BloodKnight => CountTypes.BloodKnight,
           CustomRoles.Succubus => CountTypes.Succubus,
           CustomRoles.HexMaster => CountTypes.HexMaster,
           CustomRoles.NWitch => CountTypes.NWitch,
           CustomRoles.Wraith => CountTypes.Wraith,
           CustomRoles.Pestilence => CountTypes.Pestilence,
           CustomRoles.PlagueBearer => CountTypes.PlagueBearer,
           CustomRoles.Parasite => CountTypes.Impostor,
           CustomRoles.NSerialKiller => CountTypes.NSerialKiller,
           CustomRoles.Juggernaut => CountTypes.Juggernaut,
           CustomRoles.Jinx => CountTypes.Jinx,
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
    Juggernaut,
    Infectious,
    Virus,
    Rogue,
    DarkHide,
    Jinx,
    Ritualist,
    Pickpocket,
    Traitor,
    Medusa,
    Spiritcaller,
    RuthlessRomantic,
    Pestilence,
    PlagueBearer,
    Glitch,
    Arsonist
}