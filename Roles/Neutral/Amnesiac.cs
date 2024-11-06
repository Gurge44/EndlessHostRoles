using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Neutral
{
    public class Amnesiac : RoleBase
    {
        private const int Id = 35000;
        private static List<Amnesiac> Instances = [];
        public static HashSet<byte> WasAmnesiac = [];

        private static OptionItem RememberCooldown;
        private static OptionItem CanRememberCrewPower;
        private static OptionItem IncompatibleNeutralMode;
        public static OptionItem RememberMode;
        public static OptionItem CanVent;
        private static OptionItem VentCooldown;
        private static OptionItem VentDuration;

        private static readonly CustomRoles[] AmnesiacIncompatibleNeutralMode =
        [
            CustomRoles.Amnesiac,
            CustomRoles.Pursuer,
            CustomRoles.Totocalcio,
            CustomRoles.Maverick
        ];

        private static readonly string[] RememberModes =
        [
            "AmnesiacRM.ByReportingBody",
            "AmnesiacRM.ByKillButton"
        ];

        private byte AmnesiacId;

        public override bool IsEnable => Instances.Count > 0;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Amnesiac);

            RememberMode = new StringOptionItem(Id + 9, "RememberMode", RememberModes, 0, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);

            RememberCooldown = new FloatOptionItem(Id + 10, "RememberCooldown", new(0f, 180f, 0.5f), 5f, TabGroup.NeutralRoles)
                .SetParent(RememberMode)
                .SetValueFormat(OptionFormat.Seconds);

            CanRememberCrewPower = new BooleanOptionItem(Id + 11, "CanRememberCrewPower", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);

            IncompatibleNeutralMode = new StringOptionItem(Id + 12, "IncompatibleNeutralMode", AmnesiacIncompatibleNeutralMode.Select(x => x.ToColoredString()).ToArray(), 0, TabGroup.NeutralRoles, noTranslation: true)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);

            CanVent = new BooleanOptionItem(Id + 13, "CanVent", false, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Amnesiac]);

            VentCooldown = new FloatOptionItem(Id + 14, "VentCooldown", new(0f, 180f, 0.5f), 5f, TabGroup.NeutralRoles)
                .SetParent(CanVent)
                .SetValueFormat(OptionFormat.Seconds);

            VentDuration = new FloatOptionItem(Id + 15, "MaxInVentTime", new(0f, 180f, 0.5f), 5f, TabGroup.NeutralRoles)
                .SetParent(CanVent)
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            Instances = [];
            WasAmnesiac = [];
        }

        public override void Add(byte playerId)
        {
            Instances.Add(this);
            AmnesiacId = playerId;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = RememberCooldown.GetFloat();
        }

        public override bool CanUseKillButton(PlayerControl player)
        {
            return !player.Data.IsDead && RememberMode.GetValue() == 1;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(false);

            if (CanVent.GetBool())
            {
                AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
                AURoleOptions.EngineerInVentMaxTime = VentDuration.GetFloat();
            }
        }

        public static void OnAnyoneDeath(PlayerControl target)
        {
            if (RememberMode.GetValue() == 0)
            {
                foreach (Amnesiac instance in Instances)
                {
                    LocateArrow.Add(instance.AmnesiacId, target.Pos());
                    PlayerControl amne = Utils.GetPlayerById(instance.AmnesiacId);
                    Utils.NotifyRoles(SpecifySeer: amne, SpecifyTarget: amne);
                }
            }
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (RememberMode.GetValue() == 1)
                RememberRole(killer, target);

            return false;
        }

        public override bool CheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, PlayerControl killer)
        {
            if (target.Object.Is(CustomRoles.Unreportable)) return true;
            
            if (RememberMode.GetValue() == 0)
            {
                RememberRole(reporter, target.Object);
                return false;
            }

            return true;
        }

        private static void RememberRole(PlayerControl amnesiac, PlayerControl target)
        {
            CustomRoles? RememberedRole = null;

            var amneNotifyString = string.Empty;
            CustomRoles targetRole = target.GetCustomRole();
            int loversAlive = Main.LoversPlayers.Count(x => x.IsAlive());

            switch (targetRole)
            {
                case CustomRoles.Jackal:
                    RememberedRole = CustomRoles.Sidekick;
                    amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedNeutralKiller"));
                    break;
                case CustomRoles.Necromancer:
                    RememberedRole = CustomRoles.Deathknight;
                    amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedNeutralKiller"));
                    break;
                case CustomRoles.LovingCrewmate when loversAlive > 0:
                    target.RpcSetCustomRole(CustomRoles.CrewmateEHR);
                    RememberedRole = CustomRoles.LovingCrewmate;
                    Main.LoversPlayers.RemoveAll(x => x.PlayerId == target.PlayerId);
                    Main.LoversPlayers.Add(amnesiac);
                    amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedLover"));
                    break;
                case CustomRoles.LovingImpostor when loversAlive > 0:
                    target.RpcSetCustomRole(CustomRoles.ImpostorEHR);
                    RememberedRole = CustomRoles.LovingImpostor;
                    Main.LoversPlayers.RemoveAll(x => x.PlayerId == target.PlayerId);
                    Main.LoversPlayers.Add(amnesiac);
                    amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedLover"));
                    break;
                case CustomRoles.LovingCrewmate:
                    RememberedRole = CustomRoles.Sheriff;
                    amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedCrewmate"));
                    break;
                case CustomRoles.LovingImpostor:
                    RememberedRole = CustomRoles.Refugee;
                    amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedImpostor"));
                    break;
                default:
                    switch (target.GetTeam())
                    {
                        case Team.Impostor:
                            RememberedRole = CustomRoles.Refugee;
                            amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedImpostor"));
                            break;
                        case Team.Crewmate:
                            RememberedRole = CanRememberCrewPower.GetBool() || targetRole.GetCrewmateRoleCategory() != RoleOptionType.Crewmate_Power ? targetRole : CustomRoles.Sheriff;
                            amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedCrewmate"));
                            break;
                        case Team.Neutral:
                            if (!SingleRoles.Contains(targetRole))
                            {
                                RememberedRole = targetRole;
                                amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("RememberedNeutralKiller"));
                            }
                            else
                            {
                                RememberedRole = AmnesiacIncompatibleNeutralMode[IncompatibleNeutralMode.GetValue()];
                                amneNotifyString = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString($"Remembered{RememberedRole}"));
                            }

                            break;
                    }

                    break;
            }


            if (!RememberedRole.HasValue)
            {
                amnesiac.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacInvalidTarget")));
                return;
            }

            CustomRoles role = RememberedRole.Value;

            amnesiac.RpcSetCustomRole(role);
            amnesiac.RpcChangeRoleBasis(role);

            amnesiac.Notify(amneNotifyString);
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Amnesiac), GetString("AmnesiacRemembered")));

            amnesiac.SetKillCooldown(3f);

            target.RpcGuardAndKill(amnesiac);
            target.RpcGuardAndKill(target);

            if (role.IsRecruitingRole()) amnesiac.SetAbilityUseLimit(0);

            if (role.GetRoleTypes() == RoleTypes.Engineer)
                WasAmnesiac.Add(amnesiac.PlayerId);

            if (!amnesiac.IsLocalPlayer()) return;

            switch (role)
            {
                case CustomRoles.Virus:
                case CustomRoles.Succubus:
                    Achievements.Type.UnderNewManagement.Complete();
                    break;
                case CustomRoles.Deathknight:
                case CustomRoles.Sidekick:
                    Achievements.Type.FirstDayOnTheJob.Complete();
                    break;
            }
        }

        public override void OnReportDeadBody()
        {
            LocateArrow.RemoveAllTarget(AmnesiacId);
        }

        public override bool KnowRole(PlayerControl player, PlayerControl target)
        {
            if (base.KnowRole(player, target)) return true;

            if (player.Is(CustomRoles.Refugee) && target.Is(CustomRoleTypes.Impostor)) return true;

            return player.Is(CustomRoleTypes.Impostor) && target.Is(CustomRoles.Refugee);
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            ActionButton amneButton = RememberMode.GetValue() == 1 ? hud.KillButton : hud.ReportButton;
            amneButton?.OverrideText(GetString("RememberButtonText"));
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
        {
            if (seer.PlayerId != target.PlayerId || seer.PlayerId != AmnesiacId || meeting || hud) return string.Empty;

            return LocateArrow.GetArrows(seer);
        }
    }
}