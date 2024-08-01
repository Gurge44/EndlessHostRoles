global using Object = UnityEngine.Object;
using System;
using System.Text;
using AmongUs.GameOptions;
using EHR.AddOns.Crewmate;
using EHR.AddOns.Impostor;


namespace EHR
{
    public abstract class RoleBase : IComparable<RoleBase>
    {
        public abstract bool IsEnable { get; }

        public int CompareTo(RoleBase other)
        {
            var thisName = GetType().Name;
            var translatedName = Translator.GetString(thisName);
            if (translatedName != string.Empty && !translatedName.StartsWith("*") && !translatedName.StartsWith("<INVALID"))
            {
                thisName = translatedName;
            }

            var otherName = other.GetType().Name;
            var translatedOtherName = Translator.GetString(otherName);
            if (translatedOtherName != string.Empty && !translatedOtherName.StartsWith("*") && !translatedOtherName.StartsWith("<INVALID"))
            {
                otherName = translatedOtherName;
            }

            return string.Compare(thisName, otherName, StringComparison.Ordinal);
        }

        // This is a base class for all roles. It contains some common methods and properties that are used by all roles.
        public abstract void Init();
        public abstract void Add(byte playerId);
        public abstract void SetupCustomOption();

        // Some virtual methods that trigger actions, like venting, petting, CheckMurder, etc. These are not abstract because they have a default implementation. These should also have the same name as the methods in the derived classes.
        public virtual void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = Options.DefaultKillCooldown;
        }

        public virtual bool CanUseKillButton(PlayerControl pc)
        {
            return pc.IsAlive() && (pc.Is(CustomRoleTypes.Impostor) || pc.IsNeutralKiller());
        }

        public virtual bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return pc.IsAlive() && (pc.Is(CustomRoleTypes.Impostor) || (pc.Is(CustomRoles.Bloodlust) && Bloodlust.CanVent.GetBool())) && Circumvent.CanUseImpostorVentButton(pc) && pc.Data.Role.Role is not RoleTypes.Engineer;
        }

        public virtual bool CanUseSabotage(PlayerControl pc)
        {
            return pc.Is(Team.Impostor) || pc.Is(CustomRoles.Trickster) || pc.Is(CustomRoles.Mischievous) || (pc.Is(CustomRoles.Bloodlust) && Bloodlust.HasImpVision.GetBool()) && pc.IsAlive();
        }

        public virtual void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
        }

        public virtual void OnFixedUpdate(PlayerControl pc)
        {
        }

        public virtual void OnCheckPlayerPosition(PlayerControl pc)
        {
        }

        public virtual void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
        {
        }

        public virtual void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
        {
            if (Options.CurrentGameMode == CustomGameMode.HideAndSeek && HnSManager.PlayerRoles[pc.PlayerId].Interface.Team == Team.Crewmate)
            {
                int time = GameMode.HideAndSeekRoles.Hider.TimeDecreaseOnTaskComplete.GetInt();
                HnSManager.TimeLeft -= time;
                pc.Notify(Translator.GetString("TimeDecreased"));
                if (60 - (HnSManager.TimeLeft % 60) <= time) Utils.NotifyRoles();
            }
        }

        public virtual void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
        }

        public virtual void OnEnterVent(PlayerControl pc, Vent vent)
        {
        }

        public virtual void OnExitVent(PlayerControl pc, Vent vent)
        {
        }

        public virtual void OnPet(PlayerControl pc)
        {
            if (!AmongUsClient.Instance.AmHost) return;

            int x = IRandom.Instance.Next(1, 16);
            string suffix;
            if (x >= 14)
            {
                x -= 13;
                suffix = pc.GetCustomRoleTypes() switch
                {
                    CustomRoleTypes.Impostor => $"Imp{x}",
                    CustomRoleTypes.Neutral => $"Neutral{x}",
                    CustomRoleTypes.Crewmate => x == 1 ? "Crew" : pc.GetTaskState().hasTasks && pc.GetTaskState().IsTaskFinished ? "CrewTaskDone" : "CrewWithTasksLeft",
                    _ => x.ToString()
                };
            }
            else suffix = x.ToString();

            pc.Notify(Translator.GetString($"NoPetActionMsg{suffix}"));
        }

        public virtual bool OnSabotage(PlayerControl pc)
        {
            return CanUseSabotage(pc);
        }

        public virtual bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            return target != null && killer != null;
        }

        public virtual bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            return target != null && killer != null;
        }

        public virtual void OnMurder(PlayerControl killer, PlayerControl target)
        {
        }

        public virtual bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            return true;
        }

        public virtual bool OnVanish(PlayerControl pc)
        {
            return true;
        }

        public virtual bool OnVote(PlayerControl voter, PlayerControl target)
        {
            return false;
        }

        public virtual void OnReportDeadBody()
        {
        }

        public virtual bool CheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, PlayerControl killer)
        {
            return true;
        }

        public virtual void AfterMeetingTasks()
        {
        }

        public virtual string GetProgressText(byte playerId, bool comms)
        {
            var sb = new StringBuilder();
            sb.Append(Utils.GetAbilityUseLimitDisplay(playerId));
            sb.Append(Utils.GetTaskCount(playerId, comms));
            return sb.ToString();
        }

        public virtual void SetButtonTexts(HudManager hud, byte id)
        {
        }

        public virtual string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            return string.Empty;
        }

        public virtual bool KnowRole(PlayerControl seer, PlayerControl target)
        {
            var seerRole = seer.GetCustomRole();
            return seerRole.IsNK() && seerRole == target.GetCustomRole() && seer.GetTeam() == target.GetTeam();
        }
    }
}