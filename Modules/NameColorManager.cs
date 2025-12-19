using System.Collections.Generic;
using System.Linq;
using EHR.AddOns.Common;
using EHR.AddOns.GhostRoles;
using EHR.Coven;
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

        if (!TryGetData(seer, target, out string colorCode))
        {
            if (KnowTargetRoleColor(seer, target, isMeeting, out string color))
                colorCode = color == "" ? target.GetRoleColorCode() : color;
        }

        string openTag = "", closeTag = "";

        if (colorCode != "")
        {
            if (!colorCode.StartsWith('#')) colorCode = "#" + colorCode;

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
            case CustomGameMode.FFA when FreeForAll.FFATeamMode.GetBool():
                if (FreeForAll.PlayerTeams.TryGetValue(target.PlayerId, out int team))
                    color = FreeForAll.TeamColors.GetValueOrDefault(team, "#00ffff");
                return true;
            case CustomGameMode.Snowdown:
            case CustomGameMode.Mingle:
            case CustomGameMode.RoomRush:
            case CustomGameMode.NaturalDisasters:
            case CustomGameMode.StopAndGo:
            case CustomGameMode.TheMindGame:
                color = "#ffffff";
                return true;
            case CustomGameMode.HotPotato:
                (byte holderID, byte lastHolderID) = HotPotato.GetState();

                if (target.PlayerId == holderID)
                    color = "#000000";
                else if (target.PlayerId == lastHolderID)
                    color = "#00ffff";
                else
                    color = "#ffffff";

                return true;
            case CustomGameMode.HideAndSeek:
                return CustomHnS.KnowTargetRoleColor(seer, target, ref color);
            case CustomGameMode.Speedrun when Speedrun.CanKill.Contains(target.PlayerId):
                color = Main.ImpostorColor;
                return true;
            case CustomGameMode.CaptureTheFlag:
                return CaptureTheFlag.KnowTargetRoleColor(target, ref color);
            case CustomGameMode.KingOfTheZones:
                return KingOfTheZones.GetNameColor(target, ref color);
            case CustomGameMode.Quiz:
                return Quiz.KnowTargetRoleColor(target, ref color);
            case CustomGameMode.BedWars:
                color = BedWars.GetNameColor(target);
                return true;
            case CustomGameMode.Deathrace:
                return Deathrace.KnowRoleColor(seer, target, out color);
        }

        RoleBase seerRoleClass = Main.PlayerStates[seer.PlayerId].Role;
        RoleBase targetRoleClass = Main.PlayerStates[target.PlayerId].Role;

        // Global (low priority)
        if (Stained.VioletNameList.Contains(target.PlayerId) && !isMeeting) color = "#ff00ff";

        // Coven
        if (seer.Is(CustomRoleTypes.Coven) && target.Is(CustomRoleTypes.Coven)) color = Main.CovenColor;

        // Impostors and Madmates
        if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoleTypes.Impostor) && CustomTeamManager.ArentInCustomTeam(seer.PlayerId, target.PlayerId)) color = target.Is(CustomRoles.Egoist) && Options.ImpEgoistVisibalToAllies.GetBool() && seer != target ? Main.RoleColors[CustomRoles.Egoist] : Main.ImpostorColor;
        if (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.DoubleAgent)) color = Main.ImpostorColor;
        if (seer.IsMadmate() && target.Is(CustomRoleTypes.Impostor) && Options.MadmateKnowWhosImp.GetBool()) color = Main.ImpostorColor;
        if (seer.Is(CustomRoleTypes.Impostor) && target.IsMadmate() && Options.ImpKnowWhosMadmate.GetBool()) color = Main.RoleColors[CustomRoles.Madmate];
        if (seer.IsMadmate() && target.IsMadmate() && Options.MadmateKnowWhosMadmate.GetBool()) color = Main.RoleColors[CustomRoles.Madmate];
        if (Blackmailer.On && seerRoleClass is Blackmailer { IsEnable: true } bm && bm.BlackmailedPlayerIds.Contains(target.PlayerId)) color = Main.RoleColors[CustomRoles.BloodKnight];

        if (Commander.On && seer.Is(Team.Impostor))
        {
            if (Commander.PlayerList.Any(x => x.MarkedPlayer == target.PlayerId)) color = Main.RoleColors[CustomRoles.Sprayer];
            if (Commander.PlayerList.Any(x => x.DontKillMarks.Contains(target.PlayerId))) color = "#0daeff";
        }

        // Custom Teams
        if (CustomTeamManager.AreInSameCustomTeam(seer.PlayerId, target.PlayerId) && CustomTeamManager.IsSettingEnabledForPlayerTeam(seer.PlayerId, CTAOption.KnowRoles))
            color = Main.RoleColors[target.GetCustomRole()];

        // Add-ons
        if (target.Is(CustomRoles.Glow) && Utils.IsActive(SystemTypes.Electrical)) color = Main.RoleColors[CustomRoles.Glow];
        if (target.Is(CustomRoles.Mare) && Utils.IsActive(SystemTypes.Electrical) && !isMeeting) color = Main.RoleColors[CustomRoles.Mare];
        if (seer.Is(CustomRoles.Contagious) && target.Is(CustomRoles.Contagious) && Virus.TargetKnowOtherTarget.GetBool()) color = Main.RoleColors[CustomRoles.Virus];
        if (seer.Is(CustomRoles.Charmed) && target.Is(CustomRoles.Charmed) && Cultist.TargetKnowOtherTarget.GetBool()) color = Main.RoleColors[CustomRoles.Charmed];
        if (seer.Is(CustomRoles.Entranced) && target.Is(CustomRoles.Entranced) && Siren.EntrancedKnowEntranced.GetBool()) color = Main.RoleColors[CustomRoles.Siren];
        if (seer.Is(CustomRoleTypes.Coven) && target.Is(CustomRoles.Entranced) && Siren.CovenKnowEntranced.GetValue() == 1) color = Main.RoleColors[CustomRoles.Entranced];
        if (target.Is(CustomRoleTypes.Coven) && seer.Is(CustomRoles.Entranced) && Siren.EntrancedKnowCoven.GetValue() == 1) color = Main.RoleColors[CustomRoles.Entranced];
        if (seer.Is(CustomRoles.Undead) && target.Is(CustomRoles.Undead)) color = Main.RoleColors[CustomRoles.Undead];

        // Ghost roles
        if (GhostRolesManager.AssignedGhostRoles.TryGetValue(target.PlayerId, out (CustomRoles Role, IGhostRole Instance) ghostRole) && seer.GetTeam() == ghostRole.Instance.Team)
            color = ghostRole.Instance.Team.GetTextColor();

        if (isMeeting && Haunter.AllHauntedPlayers.Contains(target.PlayerId)) color = Main.ImpostorColor;

        CustomRoles seerRole = seer.GetCustomRole();
        CustomRoles targetRole = target.GetCustomRole();

        // If 2 players have the same role and that role is a NK role, they can see each other's name color
        if (seerRole.IsNK() && seerRole == targetRole) color = Main.RoleColors[seerRole];

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
            CustomRoles.Transporter when target.IsImpostor() => "ffffff",
            CustomRoles.Silencer when Silencer.ForSilencer.Contains(target.PlayerId) => "000000",
            CustomRoles.PlagueBearer when PlagueBearer.IsPlagued(seer.PlayerId, target.PlayerId) => "000000",
            CustomRoles.Executioner when Executioner.Target.TryGetValue(seer.PlayerId, out byte exeTarget) && exeTarget == target.PlayerId => "000000",
            CustomRoles.Lawyer when Lawyer.Target.TryGetValue(seer.PlayerId, out byte lawyerTarget) && lawyerTarget == target.PlayerId => "000000",
            CustomRoles.Gangster when target.Is(CustomRoles.Madmate) => Main.RoleColors[CustomRoles.Madmate],
            CustomRoles.Crewpostor when target.Is(CustomRoleTypes.Impostor) && Options.CrewpostorKnowsAllies.GetBool() => Main.ImpostorColor,
            CustomRoles.Hypocrite when target.Is(CustomRoleTypes.Impostor) && Hypocrite.KnowsAllies.GetBool() => Main.ImpostorColor,
            CustomRoles.Cultist when target.Is(CustomRoles.Charmed) => Main.RoleColors[CustomRoles.Charmed],
            CustomRoles.Siren when target.Is(CustomRoles.Entranced) => Main.RoleColors[CustomRoles.Siren],
            CustomRoles.Necromancer or CustomRoles.Deathknight when target.Is(CustomRoles.Undead) => Main.RoleColors[CustomRoles.Undead],
            CustomRoles.Necromancer or CustomRoles.Deathknight when Necromancer.PartiallyRecruitedIds.Contains(target.PlayerId) => Main.RoleColors[CustomRoles.Deathknight],
            CustomRoles.Virus when target.Is(CustomRoles.Contagious) => Main.RoleColors[CustomRoles.Contagious],
            CustomRoles.Monarch when target.Is(CustomRoles.Knighted) => Main.RoleColors[CustomRoles.Knighted],
            CustomRoles.Spiritcaller when target.Is(CustomRoles.EvilSpirit) => Main.RoleColors[CustomRoles.EvilSpirit],
            CustomRoles.Renegade when target.Is(CustomRoleTypes.Impostor) => Main.RoleColors[CustomRoles.ImpostorEHR],
            CustomRoles.HeadHunter when ((HeadHunter)seerRoleClass).Targets.Contains(target.PlayerId) => "000000",
            CustomRoles.BountyHunter when (seerRoleClass as BountyHunter)?.GetTarget(seer) == target.PlayerId => "000000",
            CustomRoles.Pyromaniac when ((Pyromaniac)seerRoleClass).DousedList.Contains(target.PlayerId) => "#BA4A00",
            CustomRoles.Glitch when target.IsRoleBlocked() => Main.RoleColors[seerRole],
            CustomRoles.Slenderman when Slenderman.IsBlinded(target.PlayerId) => "000000",
            CustomRoles.Aid when Aid.ShieldedPlayers.ContainsKey(target.PlayerId) => Main.RoleColors[CustomRoles.Aid],
            CustomRoles.Spy when Spy.SpyRedNameList.ContainsKey(target.PlayerId) => "#BA4A00",
            CustomRoles.Mastermind when Mastermind.ManipulateDelays.ContainsKey(target.PlayerId) => "#00ffa5",
            CustomRoles.Mastermind when Mastermind.ManipulatedPlayers.ContainsKey(target.PlayerId) => Main.RoleColors[CustomRoles.Arsonist],
            CustomRoles.Hitman when (seerRoleClass as Hitman)?.TargetId == target.PlayerId => "000000",
            CustomRoles.Postman when (seerRoleClass as Postman)?.Target == target.PlayerId => Main.RoleColors[CustomRoles.Postman],
            CustomRoles.Mycologist when ((Mycologist)seerRoleClass).InfectedPlayers.Contains(target.PlayerId) => Main.RoleColors[CustomRoles.Mycologist],
            CustomRoles.Bubble when Bubble.EncasedPlayers.ContainsKey(target.PlayerId) => Main.RoleColors[CustomRoles.Bubble],
            CustomRoles.Hookshot when (seerRoleClass as Hookshot)?.MarkedPlayerId == target.PlayerId => Main.RoleColors[CustomRoles.Hookshot],
            CustomRoles.SoulHunter when SoulHunter.IsSoulHunterTarget(target.PlayerId) => Main.RoleColors[CustomRoles.SoulHunter],
            CustomRoles.Kamikaze when ((Kamikaze)seerRoleClass).MarkedPlayers.Contains(target.PlayerId) => Main.RoleColors[CustomRoles.Electric],
            CustomRoles.QuizMaster when ((QuizMaster)seerRoleClass).Target == target.PlayerId => "000000",
            CustomRoles.Augmenter when ((Augmenter)seerRoleClass).Target == target.PlayerId => "000000",
            CustomRoles.Socialite when ((Socialite)seerRoleClass).GuestList.Contains(target.PlayerId) => "000000",
            CustomRoles.Socialite when ((Socialite)seerRoleClass).MarkedPlayerId == target.PlayerId => Main.RoleColors[seerRole],
            CustomRoles.Beehive when ((Beehive)seerRoleClass).StungPlayers.ContainsKey(target.PlayerId) => "000000",
            CustomRoles.Dad when ((Dad)seerRoleClass).DrunkPlayers.Contains(target.PlayerId) => "000000",
            CustomRoles.Wasp when seerRoleClass is Wasp wasp && (wasp.DelayedKills.ContainsKey(target.PlayerId) || wasp.MeetingKills.Contains(target.PlayerId)) => "000000",
            CustomRoles.God when God.KnowInfo.GetValue() == 1 => target.GetTeam().GetTextColor(),
            CustomRoles.Curser when ((Curser)seerRoleClass).KnownFactionPlayers.Contains(target.PlayerId) => target.GetTeam().GetTextColor(),
            CustomRoles.Poache when Poache.PoachedPlayers.Contains(target.PlayerId) => "000000",
            CustomRoles.Reaper when ((Reaper)seerRoleClass).CursedPlayers.Contains(target.PlayerId) => "000000",
            CustomRoles.Dreamweaver when ((Dreamweaver)seerRoleClass).InsanePlayers.Contains(target.PlayerId) || target.Is(CustomRoles.Insane) => "000000",
            CustomRoles.Banshee when ((Banshee)seerRoleClass).ScreechedPlayers.Contains(target.PlayerId) => "000000",
            CustomRoles.Illusionist when ((Illusionist)seerRoleClass).SampledPlayerId == target.PlayerId => "000000",
            CustomRoles.Retributionist when ((Retributionist)seerRoleClass).Camping == target.PlayerId => "000000",
            CustomRoles.Seamstress when ((Seamstress)seerRoleClass).SewedPlayers.Item1 == target.PlayerId || ((Seamstress)seerRoleClass).SewedPlayers.Item2 == target.PlayerId => "000000",
            CustomRoles.Spirit when ((Spirit)seerRoleClass).Targets.Item1 == target.PlayerId || ((Spirit)seerRoleClass).Targets.Item2 == target.PlayerId => "000000",
            CustomRoles.Starspawn when ((Starspawn)seerRoleClass).IsolatedPlayers.Contains(target.PlayerId) => "000000",
            CustomRoles.Wyrd when ((Wyrd)seerRoleClass).MarkedPlayers.Contains(target.PlayerId) => "000000",
            CustomRoles.Investor when ((Investor)seerRoleClass).MarkedPlayers.Contains(target.PlayerId) => "000000",
            CustomRoles.Stealth when ((Stealth)seerRoleClass).darkenedPlayers?.Any(x => x == target) ?? false => "000000",
            _ => color
        };

        // Check if the role color can be seen based on the target's role
        color = targetRole switch
        {
            CustomRoles.Virus when seer.Is(CustomRoles.Contagious) => Main.RoleColors[CustomRoles.Virus],
            CustomRoles.Renegade when seer.Is(CustomRoleTypes.Impostor) => Main.RoleColors[CustomRoles.Renegade],
            CustomRoles.Speedrunner when !seer.Is(Team.Crewmate) && target.GetTaskState().CompletedTasksCount >= Speedrunner.SpeedrunnerNotifyAtXTasksLeft.GetInt() && Speedrunner.SpeedrunnerNotifyKillers.GetBool() => Main.RoleColors[CustomRoles.Speedrunner],
            CustomRoles.SoulHunter when SoulHunter.IsSoulHunterTarget(seer.PlayerId) => Main.RoleColors[CustomRoles.SoulHunter],
            CustomRoles.Necromancer or CustomRoles.Deathknight when seer.Is(CustomRoles.Undead) => Main.RoleColors[targetRole],
            CustomRoles.Cultist when seer.Is(CustomRoles.Charmed) => Main.RoleColors[CustomRoles.Cultist],
            CustomRoles.Siren when seer.Is(CustomRoles.Entranced) => Main.RoleColors[CustomRoles.Siren],
            CustomRoles.Crewpostor when seer.Is(CustomRoleTypes.Impostor) && Options.AlliesKnowCrewpostor.GetBool() => Main.RoleColors[CustomRoles.Madmate],
            CustomRoles.Hypocrite when seer.Is(CustomRoleTypes.Impostor) && Hypocrite.AlliesKnowHypocrite.GetBool() => Main.RoleColors[CustomRoles.Madmate],
            CustomRoles.President when ((President)targetRoleClass).IsRevealed => Main.RoleColors[CustomRoles.President],
            _ => color
        };

        // Visionary and Necroview
        if (((seer.Is(CustomRoles.Necroview) && target.Data.IsDead && !target.IsAlive()) ||
             (seerRoleClass is Visionary { IsEnable: true } vn && vn.RevealedPlayerIds.Contains(target.PlayerId) && target.IsAlive() && !target.Data.IsDead))
            && seer.IsAlive())
        {
            color = target.GetTeam().GetTextColor();

            if (target.IsMadmate()) color = Main.ImpostorColor;

            if (target.Is(CustomRoles.Madmate)) color = Main.ImpostorColor;
            if (target.Is(CustomRoles.Rascal)) color = Main.ImpostorColor;
            if (target.Is(CustomRoles.Charmed)) color = Main.NeutralColor;
            if (target.Is(CustomRoles.Contagious)) color = Main.NeutralColor;
            if (target.Is(CustomRoles.Egoist)) color = Main.NeutralColor;
            if (target.Is(CustomRoles.Entranced)) color = Main.CovenColor;
        }

        // Global (important)
        if (Bubble.EncasedPlayers.TryGetValue(target.PlayerId, out long ts) && (ts + Bubble.NotifyDelay.GetInt() < Utils.TimeStamp || seer.Is(CustomRoles.Bubble))) color = Main.RoleColors[CustomRoles.Bubble];

        // If the color was determined, return true, else, check if the seer can see the target's role color without knowing the color
        if (color != "") return true;

        if (seer == target
            || (Main.GodMode.Value && seer.AmOwner)
            || Options.CurrentGameMode is CustomGameMode.FFA or CustomGameMode.StopAndGo
            || (seer.Data.IsDead && !seer.IsAlive() && Options.GhostCanSeeOtherRoles.GetBool() && (!Utils.IsRevivingRoleAlive() || !Main.DiedThisRound.Contains(seer.PlayerId)))
            || (seer.Is(CustomRoles.Mimic) && target.Data.IsDead && !target.IsAlive() && Options.MimicCanSeeDeadRoles.GetBool())
            || target.Is(CustomRoles.GM)
            || seer.Is(CustomRoles.GM)
            || (seer.Is(CustomRoles.God) && God.KnowInfo.GetValue() == 2)
            || (seer.Is(CustomRoleTypes.Coven) && target.Is(CustomRoleTypes.Coven))
            || (seer.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoleTypes.Impostor) && CustomTeamManager.ArentInCustomTeam(seer.PlayerId, target.PlayerId))
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
            || Main.PlayerStates.Values.Any(x => x.Role.KnowRole(seer, target)))
            return true;
        
        color = "#ffffff";
        return true;
    }

    private static bool TryGetData(PlayerControl seer, PlayerControl target, out string colorCode)
    {
        colorCode = "";
        PlayerState state = Main.PlayerStates[seer.PlayerId];
        if (!state.TargetColorData.TryGetValue(target.PlayerId, out string value)) return false;

        colorCode = value;
        return true;
    }

    public static void Add(byte seerId, byte targetId, string colorCode = "")
    {
        if (colorCode == "")
        {
            PlayerControl target = Utils.GetPlayerById(targetId);
            if (target == null) return;

            colorCode = target.GetRoleColorCode();
        }

        PlayerState state = Main.PlayerStates[seerId];
        if (state.TargetColorData.TryGetValue(targetId, out string value) && colorCode == value) return;

        state.TargetColorData[targetId] = colorCode;

        SendRPC(seerId, targetId, colorCode);
    }

    private static void Remove(byte seerId, byte targetId)
    {
        PlayerState state = Main.PlayerStates[seerId];
        if (!state.TargetColorData.ContainsKey(targetId)) return;

        state.TargetColorData.Remove(targetId);

        SendRPC(seerId, targetId);
    }

    private static void RemoveAll(byte seerId)
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