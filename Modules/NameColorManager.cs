using EHR.Modules;
using EHR.Roles.AddOns.Common;
using EHR.Roles.AddOns.GhostRoles;
using EHR.Roles.Crewmate;
using EHR.Roles.Impostor;
using EHR.Roles.Neutral;
using Hazel;
using System.Linq;

namespace EHR;

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

        switch (Options.CurrentGameMode)
        {
            case CustomGameMode.MoveAndStop:
                color = "#ffffff";
                return true;
            case CustomGameMode.HotPotato:
                (byte HolderID, byte LastHolderID, _, _) = HotPotatoManager.GetState();
                if (target.PlayerId == HolderID) color = "#000000";
                else if (target.PlayerId == LastHolderID) color = "#00ffff";
                else color = "#ffffff";
                return true;
            case CustomGameMode.HideAndSeek:
                return CustomHideAndSeekManager.KnowTargetRoleColor(seer, target, ref color);
        }

        // Global (low priority)
        if (Stained.VioletNameList.Contains(target.PlayerId)) color = "#ff00ff";

        // Impostors and Madmates
        if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoleTypes.Impostor)) color = (target.Is(CustomRoles.Egoist) && Options.ImpEgoistVisibalToAllies.GetBool() && seer != target) ? Main.roleColors[CustomRoles.Egoist] : Main.ImpostorColor;
        if (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoleTypes.Impostor) && Options.MadmateKnowWhosImp.GetBool()) color = Main.ImpostorColor;
        if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Madmate) && Options.ImpKnowWhosMadmate.GetBool()) color = Main.roleColors[CustomRoles.Madmate];
        if (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool()) color = Main.roleColors[CustomRoles.Madmate];
        if (Blackmailer.On && Main.PlayerStates[seer.PlayerId].Role is Blackmailer { IsEnable: true } bm && bm.BlackmailedPlayerId == target.PlayerId) color = Main.roleColors[CustomRoles.Electric];
        if (Commander.On && seer.Is(Team.Impostor))
        {
            if (Commander.PlayerList.Any(x => x.MarkedPlayer == target.PlayerId)) color = Main.roleColors[CustomRoles.Sprayer];
            if (Commander.PlayerList.Any(x => x.DontKillMarks.Contains(target.PlayerId))) color = "#0daeff";
        }

        // Add-ons
        if (seer.Is(CustomRoles.Rogue) && target.Is(CustomRoles.Rogue) && Options.RogueKnowEachOther.GetBool()) color = Main.roleColors[CustomRoles.Rogue];
        if (seer.Is(CustomRoles.Recruit) && target.Is(CustomRoles.Recruit) && Options.SidekickKnowOtherSidekick.GetBool()) color = Main.roleColors[CustomRoles.Jackal];
        if (target.Is(CustomRoles.Glow) && Utils.IsActive(SystemTypes.Electrical)) color = Main.roleColors[CustomRoles.Glow];
        if (target.Is(CustomRoles.Mare) && Utils.IsActive(SystemTypes.Electrical) && !isMeeting) color = Main.roleColors[CustomRoles.Mare];
        if (seer.Is(CustomRoles.Contagious) && target.Is(CustomRoles.Contagious) && Virus.TargetKnowOtherTarget.GetBool()) color = Main.roleColors[CustomRoles.Virus];
        if (seer.Is(CustomRoles.Charmed) && target.Is(CustomRoles.Charmed) && Succubus.TargetKnowOtherTarget.GetBool()) color = Main.roleColors[CustomRoles.Charmed];
        if (seer.Is(CustomRoles.Undead) && target.Is(CustomRoles.Undead)) color = Main.roleColors[CustomRoles.Undead];

        // Ghost roles
        if (GhostRolesManager.AssignedGhostRoles.TryGetValue(target.PlayerId, out var ghostRole))
        {
            if (seer.GetTeam() == ghostRole.Instance.Team)
            {
                color = ghostRole.Instance.Team switch
                {
                    Team.Impostor => Main.ImpostorColor,
                    Team.Crewmate => Main.CrewmateColor,
                    Team.Neutral => Main.NeutralColor,
                    _ => color
                };
            }
        }

        if (isMeeting && Haunter.AllHauntedPlayers.Contains(target.PlayerId)) color = Main.ImpostorColor;

        var seerRole = seer.GetCustomRole();
        var targetRole = target.GetCustomRole();

        // If 2 players have the same role and that role is a NK role, they can see each other's name color
        if (seerRole.IsNK() && seerRole == targetRole)
        {
            color = Main.roleColors[seerRole];
        }

        // Specific seer-target role combinations (excluding NK roles)
        color = (seerRole, targetRole) switch
        {
            (CustomRoles.Necromancer, CustomRoles.Deathknight) => Main.roleColors[CustomRoles.Deathknight],
            (CustomRoles.Deathknight, CustomRoles.Necromancer) => Main.roleColors[CustomRoles.Necromancer],
            (CustomRoles.Deathknight, CustomRoles.Deathknight) => Main.roleColors[CustomRoles.Deathknight],
            _ => color,
        };

        // Check if the seer can see the target's role color
        color = seerRole switch
        {
            CustomRoles.Gangster when target.Is(CustomRoles.Madmate) => Main.roleColors[CustomRoles.Madmate],
            CustomRoles.Crewpostor when target.Is(CustomRoleTypes.Impostor) && Options.CrewpostorKnowsAllies.GetBool() => Main.ImpostorColor,
            CustomRoles.Succubus when target.Is(CustomRoles.Charmed) => Main.roleColors[CustomRoles.Charmed],
            CustomRoles.Necromancer or CustomRoles.Deathknight when target.Is(CustomRoles.Undead) => Main.roleColors[CustomRoles.Undead],
            CustomRoles.Necromancer or CustomRoles.Deathknight when Necromancer.PartiallyRecruitedIds.Contains(target.PlayerId) => Main.roleColors[CustomRoles.Deathknight],
            CustomRoles.Virus when target.Is(CustomRoles.Contagious) => Main.roleColors[CustomRoles.Contagious],
            CustomRoles.Monarch when target.Is(CustomRoles.Knighted) => Main.roleColors[CustomRoles.Knighted],
            CustomRoles.Spiritcaller when target.Is(CustomRoles.EvilSpirit) => Main.roleColors[CustomRoles.EvilSpirit],
            CustomRoles.Sidekick when target.Is(CustomRoles.Sidekick) && Options.SidekickKnowOtherSidekick.GetBool() => Main.roleColors[CustomRoles.Jackal],
            CustomRoles.Sidekick when target.Is(CustomRoles.Recruit) && Options.SidekickKnowOtherSidekick.GetBool() => Main.roleColors[CustomRoles.Jackal],
            CustomRoles.Jackal when target.Is(CustomRoles.Recruit) => Main.roleColors[CustomRoles.Jackal],
            CustomRoles.Refugee when target.Is(CustomRoleTypes.Impostor) => Main.roleColors[CustomRoles.ImpostorTOHE],
            CustomRoles.HeadHunter when ((HeadHunter)Main.PlayerStates[seer.PlayerId].Role).Targets.Contains(target.PlayerId) => "000000",
            CustomRoles.BountyHunter when (Main.PlayerStates[seer.PlayerId].Role as BountyHunter)?.GetTarget(seer) == target.PlayerId => "000000",
            CustomRoles.Pyromaniac when ((Pyromaniac)Main.PlayerStates[seer.PlayerId].Role).DousedList.Contains(target.PlayerId) => "#BA4A00",
            CustomRoles.Glitch when Glitch.hackedIdList.ContainsKey(target.PlayerId) => Main.roleColors[CustomRoles.Glitch],
            CustomRoles.Escort when Glitch.hackedIdList.ContainsKey(target.PlayerId) => Main.roleColors[CustomRoles.Escort],
            CustomRoles.Consort when Glitch.hackedIdList.ContainsKey(target.PlayerId) => Main.roleColors[CustomRoles.Glitch],
            CustomRoles.Aid when Aid.ShieldedPlayers.ContainsKey(target.PlayerId) => Main.roleColors[CustomRoles.Aid],
            CustomRoles.Spy when Spy.SpyRedNameList.ContainsKey(target.PlayerId) => "#BA4A00",
            CustomRoles.Mastermind when Mastermind.ManipulateDelays.ContainsKey(target.PlayerId) => "#00ffa5",
            CustomRoles.Mastermind when Mastermind.ManipulatedPlayers.ContainsKey(target.PlayerId) => Main.roleColors[CustomRoles.Arsonist],
            CustomRoles.Hitman when (Main.PlayerStates[seer.PlayerId].Role as Hitman)?.TargetId == target.PlayerId => "000000",
            CustomRoles.Postman when (Main.PlayerStates[seer.PlayerId].Role as Postman)?.Target == target.PlayerId => Main.roleColors[CustomRoles.Postman],
            CustomRoles.Mycologist when ((Mycologist)Main.PlayerStates[seer.PlayerId].Role).InfectedPlayers.Contains(target.PlayerId) => Main.roleColors[CustomRoles.Mycologist],
            CustomRoles.Bubble when Bubble.EncasedPlayers.ContainsKey(target.PlayerId) => Main.roleColors[CustomRoles.Bubble],
            CustomRoles.Hookshot when (Main.PlayerStates[seer.PlayerId].Role as Hookshot)?.MarkedPlayerId == target.PlayerId => Main.roleColors[CustomRoles.Hookshot],
            CustomRoles.SoulHunter when SoulHunter.IsSoulHunterTarget(target.PlayerId) => Main.roleColors[CustomRoles.SoulHunter],
            CustomRoles.Kamikaze when ((Kamikaze)Main.PlayerStates[seer.PlayerId].Role).MarkedPlayers.Contains(target.PlayerId) => Main.roleColors[CustomRoles.Electric],
            _ => color,
        };

        // Check if the target can see the seer's role color
        color = targetRole switch
        {
            CustomRoles.Sidekick when seer.Is(CustomRoles.Recruit) && Options.SidekickKnowOtherSidekick.GetBool() => Main.roleColors[CustomRoles.Jackal],
            CustomRoles.Jackal when seer.Is(CustomRoles.Recruit) => Main.roleColors[CustomRoles.Jackal],
            CustomRoles.Virus when seer.Is(CustomRoles.Contagious) => Main.roleColors[CustomRoles.Virus],
            CustomRoles.Refugee when seer.Is(CustomRoleTypes.Impostor) => Main.roleColors[CustomRoles.Refugee],
            CustomRoles.Speedrunner when !seer.Is(Team.Crewmate) && target.GetTaskState().CompletedTasksCount >= Options.SpeedrunnerNotifyAtXTasksLeft.GetInt() && Options.SpeedrunnerNotifyKillers.GetBool() => Main.roleColors[CustomRoles.Speedrunner],
            CustomRoles.SoulHunter when SoulHunter.IsSoulHunterTarget(seer.PlayerId) => Main.roleColors[CustomRoles.SoulHunter],
            CustomRoles.Necromancer or CustomRoles.Deathknight when seer.Is(CustomRoles.Undead) => Main.roleColors[targetRole],
            CustomRoles.Succubus when seer.Is(CustomRoles.Charmed) => Main.roleColors[CustomRoles.Succubus],
            CustomRoles.Crewpostor when seer.Is(CustomRoleTypes.Impostor) && Options.AlliesKnowCrewpostor.GetBool() => Main.roleColors[CustomRoles.Madmate],
            _ => color
        };

        // Visionary and Necroview
        if (((seer.Is(CustomRoles.Necroview) && target.Data.IsDead && !target.IsAlive()) ||
             (Main.PlayerStates[seer.PlayerId].Role is Visionary { IsEnable: true } vn && vn.RevealedPlayerIds.Contains(target.PlayerId) && target.IsAlive() && !target.Data.IsDead))
            && seer.IsAlive())
        {
            color = target.GetCustomRoleTypes() switch
            {
                CustomRoleTypes.Impostor => Main.ImpostorColor,
                CustomRoleTypes.Crewmate => Main.CrewmateColor,
                CustomRoleTypes.Neutral => Main.NeutralColor,
                _ => color
            };

            if (target.GetCustomRole() is CustomRoles.Parasite or CustomRoles.Crewpostor or CustomRoles.Convict or CustomRoles.Refugee) color = Main.ImpostorColor;
            if (target.Is(CustomRoles.Madmate)) color = Main.ImpostorColor;
            if (target.Is(CustomRoles.Rascal)) color = Main.ImpostorColor;

            if (target.Is(CustomRoles.Charmed)) color = Main.NeutralColor;
            if (target.Is(CustomRoles.Contagious)) color = Main.NeutralColor;
            if (target.Is(CustomRoles.Egoist)) color = Main.NeutralColor;
            if (target.Is(CustomRoles.Recruit)) color = Main.NeutralColor;
        }

        // Global (important)
        if (Bubble.EncasedPlayers.TryGetValue(target.PlayerId, out var ts) && ((ts + Bubble.NotifyDelay.GetInt() < Utils.TimeStamp) || seer.Is(CustomRoles.Bubble))) color = Main.roleColors[CustomRoles.Bubble];

        // If the color was determined, return true, else, check if the seer can see the target's role color without knowing the color
        if (color != "") return true;
        return seer == target
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
               || (target.Is(CustomRoles.Doctor) && !target.HasEvilAddon() && Options.DoctorVisibleToEveryone.GetBool())
               || (target.Is(CustomRoles.Gravestone) && Main.PlayerStates[target.Data.PlayerId].IsDead)
               || (target.Is(CustomRoles.Mayor) && Options.MayorRevealWhenDoneTasks.GetBool() && target.GetTaskState().IsTaskFinished)
               || (seer.Is(CustomRoleTypes.Crewmate) && target.Is(CustomRoles.Marshall) && target.GetTaskState().IsTaskFinished)
               || EvilDiviner.IsShowTargetRole(seer, target);
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

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetNameColorData, SendOption.Reliable);
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