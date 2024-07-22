using System.Linq;
using EHR.AddOns.Common;
using EHR.AddOns.GhostRoles;
using EHR.Crewmate;
using EHR.Impostor;
using EHR.Modules;
using EHR.Neutral;
using Hazel;

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
            case CustomGameMode.FFA when FFAManager.FFATeamMode.GetBool():
                color = FFAManager.TeamColors[FFAManager.PlayerTeams[target.PlayerId]];
                return true;
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
                return HnSManager.KnowTargetRoleColor(seer, target, ref color);
            case CustomGameMode.Speedrun when SpeedrunManager.CanKill.Contains(target.PlayerId):
                color = Main.ImpostorColor;
                return true;
        }

        // Global (low priority)
        if (Stained.VioletNameList.Contains(target.PlayerId)) color = "#ff00ff";

        // Impostors and Madmates
        if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoleTypes.Impostor)) color = (target.Is(CustomRoles.Egoist) && Options.ImpEgoistVisibalToAllies.GetBool() && seer != target) ? Main.RoleColors[CustomRoles.Egoist] : Main.ImpostorColor;
        if (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoleTypes.Impostor) && Options.MadmateKnowWhosImp.GetBool()) color = Main.ImpostorColor;
        if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Madmate) && Options.ImpKnowWhosMadmate.GetBool()) color = Main.RoleColors[CustomRoles.Madmate];
        if (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool()) color = Main.RoleColors[CustomRoles.Madmate];
        if (Blackmailer.On && Main.PlayerStates[seer.PlayerId].Role is Blackmailer { IsEnable: true } bm && bm.BlackmailedPlayerId == target.PlayerId) color = Main.RoleColors[CustomRoles.Electric];
        if (Commander.On && seer.Is(Team.Impostor))
        {
            if (Commander.PlayerList.Any(x => x.MarkedPlayer == target.PlayerId)) color = Main.RoleColors[CustomRoles.Sprayer];
            if (Commander.PlayerList.Any(x => x.DontKillMarks.Contains(target.PlayerId))) color = "#0daeff";
        }

        // Custom Teams
        if (CustomTeamManager.AreInSameCustomTeam(seer.PlayerId, target.PlayerId) && CustomTeamManager.IsSettingEnabledForPlayerTeam(seer.PlayerId, CTAOption.KnowRoles)) color = Main.RoleColors[target.GetCustomRole()];

        // Add-ons
        if (target.Is(CustomRoles.Glow) && Utils.IsActive(SystemTypes.Electrical)) color = Main.RoleColors[CustomRoles.Glow];
        if (target.Is(CustomRoles.Mare) && Utils.IsActive(SystemTypes.Electrical) && !isMeeting) color = Main.RoleColors[CustomRoles.Mare];
        if (seer.Is(CustomRoles.Contagious) && target.Is(CustomRoles.Contagious) && Virus.TargetKnowOtherTarget.GetBool()) color = Main.RoleColors[CustomRoles.Virus];
        if (seer.Is(CustomRoles.Charmed) && target.Is(CustomRoles.Charmed) && Succubus.TargetKnowOtherTarget.GetBool()) color = Main.RoleColors[CustomRoles.Charmed];
        if (seer.Is(CustomRoles.Undead) && target.Is(CustomRoles.Undead)) color = Main.RoleColors[CustomRoles.Undead];

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
            color = Main.RoleColors[seerRole];
        }

        // Specific seer-target role combinations (excluding NK roles)
        color = (seerRole, targetRole) switch
        {
            (CustomRoles.Necromancer, CustomRoles.Deathknight) => Main.RoleColors[CustomRoles.Deathknight],
            (CustomRoles.Deathknight, CustomRoles.Necromancer) => Main.RoleColors[CustomRoles.Necromancer],
            (CustomRoles.Deathknight, CustomRoles.Deathknight) => Main.RoleColors[CustomRoles.Deathknight],
            _ => color
        };

        // Check if the seer can see the target's role color
        color = seerRole switch
        {
            CustomRoles.Executioner when Executioner.Target.TryGetValue(seer.PlayerId, out var exeTarget) && exeTarget == target.PlayerId => "000000",
            CustomRoles.Gangster when target.Is(CustomRoles.Madmate) => Main.RoleColors[CustomRoles.Madmate],
            CustomRoles.Crewpostor when target.Is(CustomRoleTypes.Impostor) && Options.CrewpostorKnowsAllies.GetBool() => Main.ImpostorColor,
            CustomRoles.Succubus when target.Is(CustomRoles.Charmed) => Main.RoleColors[CustomRoles.Charmed],
            CustomRoles.Necromancer or CustomRoles.Deathknight when target.Is(CustomRoles.Undead) => Main.RoleColors[CustomRoles.Undead],
            CustomRoles.Necromancer or CustomRoles.Deathknight when Necromancer.PartiallyRecruitedIds.Contains(target.PlayerId) => Main.RoleColors[CustomRoles.Deathknight],
            CustomRoles.Virus when target.Is(CustomRoles.Contagious) => Main.RoleColors[CustomRoles.Contagious],
            CustomRoles.Monarch when target.Is(CustomRoles.Knighted) => Main.RoleColors[CustomRoles.Knighted],
            CustomRoles.Spiritcaller when target.Is(CustomRoles.EvilSpirit) => Main.RoleColors[CustomRoles.EvilSpirit],
            CustomRoles.Jackal when target.Is(CustomRoles.Recruit) => Main.RoleColors[CustomRoles.Jackal],
            CustomRoles.Refugee when target.Is(CustomRoleTypes.Impostor) => Main.RoleColors[CustomRoles.ImpostorEHR],
            CustomRoles.HeadHunter when ((HeadHunter)Main.PlayerStates[seer.PlayerId].Role).Targets.Contains(target.PlayerId) => "000000",
            CustomRoles.BountyHunter when (Main.PlayerStates[seer.PlayerId].Role as BountyHunter)?.GetTarget(seer) == target.PlayerId => "000000",
            CustomRoles.Pyromaniac when ((Pyromaniac)Main.PlayerStates[seer.PlayerId].Role).DousedList.Contains(target.PlayerId) => "#BA4A00",
            CustomRoles.Glitch when target.IsRoleBlocked() => Main.RoleColors[seerRole],
            CustomRoles.Aid when Aid.ShieldedPlayers.ContainsKey(target.PlayerId) => Main.RoleColors[CustomRoles.Aid],
            CustomRoles.Spy when Spy.SpyRedNameList.ContainsKey(target.PlayerId) => "#BA4A00",
            CustomRoles.Mastermind when Mastermind.ManipulateDelays.ContainsKey(target.PlayerId) => "#00ffa5",
            CustomRoles.Mastermind when Mastermind.ManipulatedPlayers.ContainsKey(target.PlayerId) => Main.RoleColors[CustomRoles.Arsonist],
            CustomRoles.Hitman when (Main.PlayerStates[seer.PlayerId].Role as Hitman)?.TargetId == target.PlayerId => "000000",
            CustomRoles.Postman when (Main.PlayerStates[seer.PlayerId].Role as Postman)?.Target == target.PlayerId => Main.RoleColors[CustomRoles.Postman],
            CustomRoles.Mycologist when ((Mycologist)Main.PlayerStates[seer.PlayerId].Role).InfectedPlayers.Contains(target.PlayerId) => Main.RoleColors[CustomRoles.Mycologist],
            CustomRoles.Bubble when Bubble.EncasedPlayers.ContainsKey(target.PlayerId) => Main.RoleColors[CustomRoles.Bubble],
            CustomRoles.Hookshot when (Main.PlayerStates[seer.PlayerId].Role as Hookshot)?.MarkedPlayerId == target.PlayerId => Main.RoleColors[CustomRoles.Hookshot],
            CustomRoles.SoulHunter when SoulHunter.IsSoulHunterTarget(target.PlayerId) => Main.RoleColors[CustomRoles.SoulHunter],
            CustomRoles.Kamikaze when ((Kamikaze)Main.PlayerStates[seer.PlayerId].Role).MarkedPlayers.Contains(target.PlayerId) => Main.RoleColors[CustomRoles.Electric],
            CustomRoles.QuizMaster when ((QuizMaster)Main.PlayerStates[seer.PlayerId].Role).Target == target.PlayerId => "000000",
            CustomRoles.Augmenter when ((Augmenter)Main.PlayerStates[seer.PlayerId].Role).Target == target.PlayerId => "000000",
            CustomRoles.Socialite when ((Socialite)Main.PlayerStates[seer.PlayerId].Role).GuestList.Contains(target.PlayerId) => "000000",
            CustomRoles.Socialite when ((Socialite)Main.PlayerStates[seer.PlayerId].Role).MarkedPlayerId == target.PlayerId => Main.RoleColors[seerRole],
            CustomRoles.Beehive when ((Beehive)Main.PlayerStates[seer.PlayerId].Role).StungPlayers.ContainsKey(target.PlayerId) => "000000",
            _ => color
        };

        // Check if the target can see the seer's role color
        color = targetRole switch
        {
            CustomRoles.Jackal when seer.Is(CustomRoles.Recruit) => Main.RoleColors[CustomRoles.Jackal],
            CustomRoles.Virus when seer.Is(CustomRoles.Contagious) => Main.RoleColors[CustomRoles.Virus],
            CustomRoles.Refugee when seer.Is(CustomRoleTypes.Impostor) => Main.RoleColors[CustomRoles.Refugee],
            CustomRoles.Speedrunner when !seer.Is(Team.Crewmate) && target.GetTaskState().CompletedTasksCount >= Speedrunner.SpeedrunnerNotifyAtXTasksLeft.GetInt() && Speedrunner.SpeedrunnerNotifyKillers.GetBool() => Main.RoleColors[CustomRoles.Speedrunner],
            CustomRoles.SoulHunter when SoulHunter.IsSoulHunterTarget(seer.PlayerId) => Main.RoleColors[CustomRoles.SoulHunter],
            CustomRoles.Necromancer or CustomRoles.Deathknight when seer.Is(CustomRoles.Undead) => Main.RoleColors[targetRole],
            CustomRoles.Succubus when seer.Is(CustomRoles.Charmed) => Main.RoleColors[CustomRoles.Succubus],
            CustomRoles.Crewpostor when seer.Is(CustomRoleTypes.Impostor) && Options.AlliesKnowCrewpostor.GetBool() => Main.RoleColors[CustomRoles.Madmate],
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
        if (Bubble.EncasedPlayers.TryGetValue(target.PlayerId, out var ts) && ((ts + Bubble.NotifyDelay.GetInt() < Utils.TimeStamp) || seer.Is(CustomRoles.Bubble))) color = Main.RoleColors[CustomRoles.Bubble];

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
               || (seer.Is(CustomRoles.Traitor) && target.Is(Team.Impostor))
               || (seer.Is(CustomRoles.Jackal) && target.Is(CustomRoles.Sidekick))
               || (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Sidekick))
               || (seer.Is(CustomRoles.Sidekick) && target.Is(CustomRoles.Jackal))
               || (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoleTypes.Impostor) && Options.MadmateKnowWhosImp.GetBool())
               || (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Madmate) && Options.ImpKnowWhosMadmate.GetBool())
               || (seer.Is(CustomRoles.Madmate) && target.Is(CustomRoles.Madmate) && Options.MadmateKnowWhosMadmate.GetBool())
               || (target.Is(CustomRoles.SuperStar) && Options.EveryOneKnowSuperStar.GetBool())
               || (target.Is(CustomRoles.Workaholic) && Workaholic.WorkaholicVisibleToEveryone.GetBool())
               || (target.Is(CustomRoles.Doctor) && !target.HasEvilAddon() && Options.DoctorVisibleToEveryone.GetBool())
               || (target.Is(CustomRoles.Gravestone) && Main.PlayerStates[target.Data.PlayerId].IsDead)
               || (target.Is(CustomRoles.Mayor) && Mayor.MayorRevealWhenDoneTasks.GetBool() && target.GetTaskState().IsTaskFinished)
               || (seer.Is(CustomRoleTypes.Crewmate) && target.Is(CustomRoles.Marshall) && target.GetTaskState().IsTaskFinished)
               || Main.PlayerStates.Values.Any(x => x.Role.KnowRole(seer, target));
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