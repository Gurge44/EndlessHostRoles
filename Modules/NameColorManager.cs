using Hazel;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;

namespace TOHE;

public static class NameColorManager
{
    public static string ApplyNameColorData(this string name, PlayerControl seer, PlayerControl target, bool isMeeting)
    {
        if (!AmongUsClient.Instance.IsGameStarted) return name;

        if (!TryGetData(seer, target, out var colorCode))
        {
            if (KnowTargetRoleColor(seer, target, isMeeting, out var color))
                colorCode = color == "" ? target.GetRoleColorCode() : color;
        }
        string openTag = "", closeTag = "";
        if (colorCode != "")
        {
            if (!colorCode.StartsWith('#'))
                colorCode = "#" + colorCode;
            openTag = $"<{colorCode}>";
            closeTag = "</color>";
        }
        return openTag + name + closeTag;
    }
    private static bool KnowTargetRoleColor(PlayerControl seer, PlayerControl target, bool isMeeting, out string color)
    {
        color = "";

        if (Options.CurrentGameMode == CustomGameMode.MoveAndStop)
        {
            color = "#ffffff";
            return true;
        }

        if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoleTypes.Impostor)) color = (target.Is(CustomRoles.Egoist) && Options.ImpEgoistVisibalToAllies.GetBool() && seer != target) ? Main.roleColors[CustomRoles.Egoist] : Main.roleColors[CustomRoles.Impostor];
        if (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoleTypes.Impostor) && Options.MadmateKnowWhosImp.GetBool()) color = Main.roleColors[CustomRoles.Impostor];
        if (seer.Is(CustomRoles.Crewpostor) && target.Is(CustomRoleTypes.Impostor) && Options.CrewpostorKnowsAllies.GetBool()) color = Main.roleColors[CustomRoles.Impostor];
        if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Madmate) && Options.ImpKnowWhosMadmate.GetBool()) color = Main.roleColors[CustomRoles.Madmate];
        if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Crewpostor) && Options.AlliesKnowCrewpostor.GetBool()) color = Main.roleColors[CustomRoles.Madmate];
        if (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool()) color = Main.roleColors[CustomRoles.Madmate];
        if (seer.Is(CustomRoles.Gangster) && target.Is(CustomRoles.Madmate)) color = Main.roleColors[CustomRoles.Madmate];

        if (seer.Is(CustomRoles.Charmed) && target.Is(CustomRoles.Succubus)) color = Main.roleColors[CustomRoles.Succubus];
        if (seer.Is(CustomRoles.Succubus) && target.Is(CustomRoles.Charmed)) color = Main.roleColors[CustomRoles.Charmed];
        if (seer.Is(CustomRoles.Charmed) && target.Is(CustomRoles.Charmed) && Succubus.TargetKnowOtherTarget.GetBool()) color = Main.roleColors[CustomRoles.Charmed];

        if (seer.Is(CustomRoles.Undead))
        {
            if (target.Is(CustomRoles.Undead)) color = Main.roleColors[CustomRoles.Undead];
            if (target.Is(CustomRoles.Necromancer)) color = Main.roleColors[CustomRoles.Necromancer];
            if (target.Is(CustomRoles.Deathknight)) color = Main.roleColors[CustomRoles.Deathknight];
        }
        if (seer.Is(CustomRoles.Deathknight) && target.Is(CustomRoles.Undead)) color = Main.roleColors[CustomRoles.Undead];
        if (seer.Is(CustomRoles.Necromancer) && target.Is(CustomRoles.Undead)) color = Main.roleColors[CustomRoles.Undead];
        if ((seer.GetCustomRole() is CustomRoles.Necromancer or CustomRoles.Deathknight) && Necromancer.PartiallyRecruitedIds.Contains(target.PlayerId)) color = Main.roleColors[CustomRoles.Deathknight];

        color = (seer.GetCustomRole(), target.GetCustomRole()) switch
        {
            (CustomRoles.Jackal, CustomRoles.Jackal) => Main.roleColors[CustomRoles.Jackal],
            (CustomRoles.Juggernaut, CustomRoles.Juggernaut) => Main.roleColors[CustomRoles.Juggernaut],
            (CustomRoles.NSerialKiller, CustomRoles.NSerialKiller) => Main.roleColors[CustomRoles.NSerialKiller],
            (CustomRoles.SoulHunter, CustomRoles.SoulHunter) => Main.roleColors[CustomRoles.SoulHunter],
            (CustomRoles.Enderman, CustomRoles.Enderman) => Main.roleColors[CustomRoles.Enderman],
            (CustomRoles.Mycologist, CustomRoles.Mycologist) => Main.roleColors[CustomRoles.Mycologist],
            (CustomRoles.Bubble, CustomRoles.Bubble) => Main.roleColors[CustomRoles.Bubble],
            (CustomRoles.Hookshot, CustomRoles.Hookshot) => Main.roleColors[CustomRoles.Hookshot],
            (CustomRoles.Sprayer, CustomRoles.Sprayer) => Main.roleColors[CustomRoles.Sprayer],
            (CustomRoles.PlagueDoctor, CustomRoles.PlagueDoctor) => Main.roleColors[CustomRoles.PlagueDoctor],
            (CustomRoles.Postman, CustomRoles.Postman) => Main.roleColors[CustomRoles.Postman],
            (CustomRoles.WeaponMaster, CustomRoles.WeaponMaster) => Main.roleColors[CustomRoles.WeaponMaster],
            (CustomRoles.Magician, CustomRoles.Magician) => Main.roleColors[CustomRoles.Magician],
            (CustomRoles.Reckless, CustomRoles.Reckless) => Main.roleColors[CustomRoles.Reckless],
            (CustomRoles.Pyromaniac, CustomRoles.Pyromaniac) => Main.roleColors[CustomRoles.Pyromaniac],
            (CustomRoles.Eclipse, CustomRoles.Eclipse) => Main.roleColors[CustomRoles.Eclipse],
            (CustomRoles.Vengeance, CustomRoles.Vengeance) => Main.roleColors[CustomRoles.Vengeance],
            (CustomRoles.HeadHunter, CustomRoles.HeadHunter) => Main.roleColors[CustomRoles.HeadHunter],
            (CustomRoles.Imitator, CustomRoles.Imitator) => Main.roleColors[CustomRoles.Imitator],
            (CustomRoles.Werewolf, CustomRoles.Werewolf) => Main.roleColors[CustomRoles.Werewolf],
            (CustomRoles.Jinx, CustomRoles.Jinx) => Main.roleColors[CustomRoles.Jinx],
            (CustomRoles.Wraith, CustomRoles.Wraith) => Main.roleColors[CustomRoles.Wraith],
            (CustomRoles.HexMaster, CustomRoles.HexMaster) => Main.roleColors[CustomRoles.HexMaster],
            (CustomRoles.BloodKnight, CustomRoles.BloodKnight) => Main.roleColors[CustomRoles.BloodKnight],
            (CustomRoles.Pelican, CustomRoles.Pelican) => Main.roleColors[CustomRoles.Pelican],
            (CustomRoles.Poisoner, CustomRoles.Poisoner) => Main.roleColors[CustomRoles.Poisoner],
            (CustomRoles.Virus, CustomRoles.Virus) => Main.roleColors[CustomRoles.Virus],
            (CustomRoles.Parasite, CustomRoles.Parasite) => Main.roleColors[CustomRoles.Parasite],
            (CustomRoles.Traitor, CustomRoles.Traitor) => Main.roleColors[CustomRoles.Traitor],
            (CustomRoles.DarkHide, CustomRoles.DarkHide) => Main.roleColors[CustomRoles.DarkHide],
            (CustomRoles.Pickpocket, CustomRoles.Pickpocket) => Main.roleColors[CustomRoles.Pickpocket],
            (CustomRoles.Spiritcaller, CustomRoles.Spiritcaller) => Main.roleColors[CustomRoles.Spiritcaller],
            (CustomRoles.Medusa, CustomRoles.Medusa) => Main.roleColors[CustomRoles.Medusa],
            (CustomRoles.Ritualist, CustomRoles.Ritualist) => Main.roleColors[CustomRoles.Ritualist],
            (CustomRoles.Glitch, CustomRoles.Glitch) => Main.roleColors[CustomRoles.Glitch],
            (CustomRoles.Succubus, CustomRoles.Succubus) => Main.roleColors[CustomRoles.Succubus],
            (CustomRoles.Necromancer, CustomRoles.Deathknight) => Main.roleColors[CustomRoles.Deathknight],
            (CustomRoles.Deathknight, CustomRoles.Necromancer) => Main.roleColors[CustomRoles.Necromancer],
            (CustomRoles.Necromancer, CustomRoles.Necromancer) => Main.roleColors[CustomRoles.Necromancer],
            (CustomRoles.Deathknight, CustomRoles.Deathknight) => Main.roleColors[CustomRoles.Deathknight],
            _ => color,
        };

        color = seer.GetCustomRole() switch
        {
            CustomRoles.HeadHunter when HeadHunter.Targets.Contains(target.PlayerId) => "000000",
            CustomRoles.BountyHunter when BountyHunter.GetTarget(seer) == target.PlayerId => "000000",
            CustomRoles.Pyromaniac when Pyromaniac.DousedList.Contains(target.PlayerId) => "#BA4A00",
            CustomRoles.Glitch when Glitch.hackedIdList.ContainsKey(target.PlayerId) => Main.roleColors[CustomRoles.Glitch],
            CustomRoles.Escort when Glitch.hackedIdList.ContainsKey(target.PlayerId) => Main.roleColors[CustomRoles.Escort],
            CustomRoles.Consort when Glitch.hackedIdList.ContainsKey(target.PlayerId) => Main.roleColors[CustomRoles.Glitch],
            CustomRoles.Aid when Aid.ShieldedPlayers.ContainsKey(target.PlayerId) => Main.roleColors[CustomRoles.Aid],
            CustomRoles.Spy when Spy.SpyRedNameList.ContainsKey(target.PlayerId) => "#BA4A00",
            CustomRoles.Mastermind when Mastermind.ManipulateDelays.ContainsKey(target.PlayerId) => "#00ffa5",
            CustomRoles.Mastermind when Mastermind.ManipulatedPlayers.ContainsKey(target.PlayerId) => Main.roleColors[CustomRoles.Arsonist],
            CustomRoles.Hitman when Hitman.targetId == target.PlayerId => "000000",
            CustomRoles.Postman when Postman.Target == target.PlayerId => Main.roleColors[CustomRoles.Postman],
            CustomRoles.Mycologist when Mycologist.InfectedPlayers.Contains(target.PlayerId) => Main.roleColors[CustomRoles.Mycologist],
            CustomRoles.Bubble when Bubble.EncasedPlayers.ContainsKey(target.PlayerId) => Main.roleColors[CustomRoles.Bubble],
            CustomRoles.Hookshot when Hookshot.MarkedPlayerId == target.PlayerId => Main.roleColors[CustomRoles.Hookshot],
            CustomRoles.SoulHunter when SoulHunter.CurrentTarget.ID == target.PlayerId => Main.roleColors[CustomRoles.SoulHunter],
            CustomRoles.Kamikaze when Kamikaze.MarkedPlayers.TryGetValue(seer.PlayerId, out var targets) && targets.Contains(target.PlayerId) => Main.roleColors[CustomRoles.Electric],
            _ => color,
        };

        if (SoulHunter.CurrentTarget.ID == seer.PlayerId && target.Is(CustomRoles.SoulHunter)) color = Main.roleColors[CustomRoles.SoulHunter];

        if (Bubble.EncasedPlayers.TryGetValue(target.PlayerId, out var ts) && ts + Bubble.NotifyDelay.GetInt() < Utils.GetTimeStamp()) color = Main.roleColors[CustomRoles.Bubble];

        if (target.Is(CustomRoles.Speedrunner) && !seer.Is(Team.Crewmate) && target.GetTaskState().CompletedTasksCount >= Options.SpeedrunnerNotifyAtXTasksLeft.GetInt() && Options.SpeedrunnerNotifyKillers.GetBool()) color = Main.roleColors[CustomRoles.Speedrunner];

        if (seer.Is(CustomRoles.Refugee) && target.Is(CustomRoleTypes.Impostor)) color = Main.roleColors[CustomRoles.ImpostorTOHE];
        if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Refugee)) color = Main.roleColors[CustomRoles.Refugee];

        if (seer.Is(CustomRoles.Necroview) && seer.IsAlive())
        {
            if (target.Data.IsDead && !target.IsAlive())
            {
                if (target.Is(CustomRoleTypes.Impostor)) color = Main.roleColors[CustomRoles.Impostor];
                if (target.Is(CustomRoles.Madmate)) color = Main.roleColors[CustomRoles.Impostor];
                if (target.Is(CustomRoles.Parasite)) color = Main.roleColors[CustomRoles.Impostor];
                if (target.Is(CustomRoles.Crewpostor)) color = Main.roleColors[CustomRoles.Impostor];
                if (target.Is(CustomRoles.Convict)) color = Main.roleColors[CustomRoles.Impostor];
                if (target.Is(CustomRoles.Refugee)) color = Main.roleColors[CustomRoles.Impostor];
                if (target.Is(CustomRoles.Rascal)) color = Main.roleColors[CustomRoles.Impostor];
                if (target.Is(CustomRoleTypes.Crewmate)) color = Main.roleColors[CustomRoles.Bait];
                if (target.Is(CustomRoleTypes.Neutral)) color = Main.roleColors[CustomRoles.SwordsMan];
                if (target.Is(CustomRoles.Charmed)) color = Main.roleColors[CustomRoles.SwordsMan];
                if (target.Is(CustomRoles.Contagious)) color = Main.roleColors[CustomRoles.SwordsMan];
                if (target.Is(CustomRoles.Egoist)) color = Main.roleColors[CustomRoles.SwordsMan];
                if (target.Is(CustomRoles.Recruit)) color = Main.roleColors[CustomRoles.SwordsMan];
            }
        }

        if (seer.Is(CustomRoles.Visionary) && seer.IsAlive())
        {
            if (target.IsAlive() && !target.Data.IsDead)
            {
                if (target.Is(CustomRoleTypes.Impostor)) color = Main.roleColors[CustomRoles.Impostor];
                if (target.Is(CustomRoles.Madmate)) color = Main.roleColors[CustomRoles.Impostor];
                if (target.Is(CustomRoles.Parasite)) color = Main.roleColors[CustomRoles.Impostor];
                if (target.Is(CustomRoles.Crewpostor)) color = Main.roleColors[CustomRoles.Impostor];
                if (target.Is(CustomRoles.Convict)) color = Main.roleColors[CustomRoles.Impostor];
                if (target.Is(CustomRoles.Refugee)) color = Main.roleColors[CustomRoles.Impostor];
                if (target.Is(CustomRoles.Rascal)) color = Main.roleColors[CustomRoles.Impostor];
                if (target.Is(CustomRoleTypes.Crewmate)) color = Main.roleColors[CustomRoles.Bait];
                if (target.Is(CustomRoleTypes.Neutral)) color = Main.roleColors[CustomRoles.SwordsMan];
                if (target.Is(CustomRoles.Charmed)) color = Main.roleColors[CustomRoles.SwordsMan];
                if (target.Is(CustomRoles.Contagious)) color = Main.roleColors[CustomRoles.SwordsMan];
                if (target.Is(CustomRoles.Egoist)) color = Main.roleColors[CustomRoles.SwordsMan];
                if (target.Is(CustomRoles.Recruit)) color = Main.roleColors[CustomRoles.SwordsMan];
            }
        }


        // Rogue
        if (seer.Is(CustomRoles.Rogue) && target.Is(CustomRoles.Rogue) && Options.RogueKnowEachOther.GetBool()) color = Main.roleColors[CustomRoles.Rogue];

        // Jackal recruit
        if (seer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Recruit)) color = Main.roleColors[CustomRoles.Jackal];
        if (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Recruit) && Options.SidekickKnowOtherSidekick.GetBool()) color = Main.roleColors[CustomRoles.Jackal];
        if (seer.Is(CustomRoles.Recruit) && target.Is(CustomRoles.Jackal)) color = Main.roleColors[CustomRoles.Jackal];
        if (seer.Is(CustomRoles.Recruit) && target.Is(CustomRoles.Sidekick) && Options.SidekickKnowOtherSidekick.GetBool()) color = Main.roleColors[CustomRoles.Jackal];
        if (seer.Is(CustomRoles.Recruit) && target.Is(CustomRoles.Recruit) && Options.SidekickKnowOtherSidekick.GetBool()) color = Main.roleColors[CustomRoles.Jackal];
        if (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Sidekick) && Options.SidekickKnowOtherSidekick.GetBool()) color = Main.roleColors[CustomRoles.Jackal];

        // Spiritcaller can see Evil Spirits in meetings
        if (seer.Is(CustomRoles.Spiritcaller) && target.Is(CustomRoles.EvilSpirit)) color = Main.roleColors[CustomRoles.EvilSpirit];

        // Monarch seeing knighted players
        if (seer.Is(CustomRoles.Monarch) && target.Is(CustomRoles.Knighted)) color = Main.roleColors[CustomRoles.Knighted];

        // GLOW SQUID IS 
        // BEST MOB IN MINECRAFT
        if (target.Is(CustomRoles.Glow) && Utils.IsActive(SystemTypes.Electrical)) color = Main.roleColors[CustomRoles.Glow];


        if (target.Is(CustomRoles.Mare) && Utils.IsActive(SystemTypes.Electrical) && !isMeeting) color = Main.roleColors[CustomRoles.Mare];

        // Virus
        if (seer.Is(CustomRoles.Contagious) && target.Is(CustomRoles.Virus)) color = Main.roleColors[CustomRoles.Virus];
        if (seer.Is(CustomRoles.Virus) && target.Is(CustomRoles.Contagious)) color = Main.roleColors[CustomRoles.Contagious];
        if (seer.Is(CustomRoles.Contagious) && target.Is(CustomRoles.Contagious) && Virus.TargetKnowOtherTarget.GetBool()) color = Main.roleColors[CustomRoles.Virus];

        if (color != "") return true;
        else return seer == target
            || (Main.GodMode.Value && seer.AmOwner)
            || (Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.MoveAndStop)
            || (Main.PlayerStates[seer.Data.PlayerId].IsDead && seer.Data.IsDead && !seer.IsAlive() && Options.GhostCanSeeOtherRoles.GetBool())
            || (seer.Is(CustomRoles.Mimic) && Main.PlayerStates[target.Data.PlayerId].IsDead && target.Data.IsDead && !target.IsAlive() && Options.MimicCanSeeDeadRoles.GetBool())
            || target.Is(CustomRoles.GM)
            || seer.Is(CustomRoles.GM)
            || seer.Is(CustomRoles.God)
            || (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoleTypes.Impostor))
            || (seer.Is(CustomRoles.Traitor) && target.Is(CustomRoleTypes.Impostor))
            || (seer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Sidekick))
            || (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Sidekick))
            || (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Jackal))
            || (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoleTypes.Impostor) && Options.MadmateKnowWhosImp.GetBool())
            || (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Madmate) && Options.ImpKnowWhosMadmate.GetBool())
            || (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool())
            || (seer.Is(CustomRoles.Rogue) && target.Is(CustomRoles.Rogue) && Options.RogueKnowEachOther.GetBool())
            || (target.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
            || (target.Is(CustomRoles.Workaholic) && Options.WorkaholicVisibleToEveryone.GetBool())
            || (target.Is(CustomRoles.Doctor) && !target.GetCustomRole().IsEvilAddons() && Options.DoctorVisibleToEveryone.GetBool())
            || (target.Is(CustomRoles.Gravestone) && Main.PlayerStates[target.Data.PlayerId].IsDead)
            || (target.Is(CustomRoles.Mayor) && Options.MayorRevealWhenDoneTasks.GetBool() && target.GetTaskState().IsTaskFinished)
            || (seer.Is(CustomRoleTypes.Crewmate) && target.Is(CustomRoles.Marshall) && target.GetTaskState().IsTaskFinished)
            //   || Mare.KnowTargetRoleColor(target, isMeeting)
            || EvilDiviner.IsShowTargetRole(seer, target)
            || Ritualist.IsShowTargetRole(seer, target);
    }
    public static bool TryGetData(PlayerControl seer, PlayerControl target, out string colorCode)
    {
        colorCode = "";
        var state = Main.PlayerStates[seer.PlayerId];
        if (!state.TargetColorData.TryGetValue(target.PlayerId, out var value)) return false;
        colorCode = value;
        return true;
    }

    public static void Add(byte seerId, byte targetId, string colorCode = "")
    {
        if (colorCode == "")
        {
            var target = Utils.GetPlayerById(targetId);
            if (target == null) return;
            colorCode = target.GetRoleColorCode();
        }

        var state = Main.PlayerStates[seerId];
        if (state.TargetColorData.TryGetValue(targetId, out var value) && colorCode == value) return;
        state.TargetColorData.Add(targetId, colorCode);

        SendRPC(seerId, targetId, colorCode);
    }
    public static void Remove(byte seerId, byte targetId)
    {
        var state = Main.PlayerStates[seerId];
        if (!state.TargetColorData.ContainsKey(targetId)) return;
        state.TargetColorData.Remove(targetId);

        SendRPC(seerId, targetId);
    }
    public static void RemoveAll(byte seerId)
    {
        Main.PlayerStates[seerId].TargetColorData.Clear();

        SendRPC(seerId);
    }
    private static void SendRPC(byte seerId, byte targetId = byte.MaxValue, string colorCode = "")
    {
        if (!AmongUsClient.Instance.AmHost) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetNameColorData, SendOption.Reliable, -1);
        writer.Write(seerId);
        writer.Write(targetId);
        writer.Write(colorCode);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte seerId = reader.ReadByte();
        byte targetId = reader.ReadByte();
        string colorCode = reader.ReadString();

        if (targetId == byte.MaxValue)
            RemoveAll(seerId);
        else if (colorCode == "")
            Remove(seerId, targetId);
        else
            Add(seerId, targetId, colorCode);
    }
}