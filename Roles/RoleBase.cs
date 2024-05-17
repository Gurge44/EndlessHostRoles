﻿using System;
using System.Text;
using AmongUs.GameOptions;
using EHR.Roles.AddOns.Crewmate;
using EHR.Roles.AddOns.Impostor;

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
            return pc.IsAlive() && (pc.Is(CustomRoleTypes.Impostor) || (pc.Is(CustomRoles.Bloodlust) && Bloodlust.CanVent.GetBool())) && Circumvent.CanUseImpostorVentButton(pc) && pc.Data.Role.Role is not RoleTypes.Crewmate and not RoleTypes.Engineer;
        }

        public virtual bool CanUseSabotage(PlayerControl pc)
        {
            return pc.Is(Team.Impostor) || pc.Is(CustomRoles.Mischievous) || (pc.Is(CustomRoles.Bloodlust) && Bloodlust.HasImpVision.GetBool()) && pc.IsAlive();
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

        public virtual void OnReportDeadBody()
        {
        }

        public virtual bool CheckReportDeadBody(PlayerControl reporter, GameData.PlayerInfo target, PlayerControl killer)
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
            hud.KillButton?.OverrideText(Translator.GetString("KillButtonText"));
            hud.ReportButton?.OverrideText(Translator.GetString("ReportButtonText"));
            hud.PetButton?.OverrideText(Translator.GetString("PetButtonText"));
            hud.ImpostorVentButton?.OverrideText(Translator.GetString("VentButtonText"));
            hud.SabotageButton?.OverrideText(Translator.GetString("SabotageButtonText"));
            if(PlayerControl.LocalPlayer.GetCustomRole().UsesPetInsteadOfKill())
            {
            hud.PetButton?.OverrideText(Translator.GetString("KillButtonText"));
            }
            if (PlayerControl.LocalPlayer.Is(RoleTypes.Shapeshifter))
            {
                hud.AbilityButton?.OverrideText(Translator.GetString("AbilityButtonText.Shapeshifter"));
            }
            if (PlayerControl.LocalPlayer.Is(RoleTypes.Engineer))
            {
                hud.AbilityButton?.OverrideText(Translator.GetString("AbilityButtonText.Engineer"));
            }
            if (PlayerControl.LocalPlayer.Is(RoleTypes.Scientist))
            {
                hud.AbilityButton?.OverrideText(Translator.GetString("AbilityButtonText.Scientist"));
            }
            if (PlayerControl.LocalPlayer.Is(RoleTypes.GuardianAngel))
            {
                hud.AbilityButton?.OverrideText(Translator.GetString("AbilityButtonText.GuardianAngel"));
            }
        }

        public virtual string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            return string.Empty;
        }

        public virtual bool KnowRole(PlayerControl seer, PlayerControl target)
        {
            return false;
        }
    }
}
