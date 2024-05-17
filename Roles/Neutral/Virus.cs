﻿using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Patches;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles.Neutral
{
    public class Virus : RoleBase
    {
        private const int Id = 13200;
        private static List<byte> playerIdList = [];
        public static List<byte> InfectedPlayer = [];
        public static Dictionary<byte, string> VirusNotify = [];
        public static List<byte> InfectedBodies = [];

        private static OptionItem KillCooldown;
        private static OptionItem InfectMax;
        public static OptionItem CanVent;
        public static OptionItem ImpostorVision;
        public static OptionItem KnowTargetRole;
        public static OptionItem TargetKnowOtherTarget;
        public static OptionItem KillInfectedPlayerAfterMeeting;
        public static OptionItem ContagiousPlayersCanKillEachOther;
        public static OptionItem ContagiousCountMode;

        public static readonly string[] ContagiousCountModeStrings =
        [
            "ContagiousCountMode.None",
            "ContagiousCountMode.Virus",
            "ContagiousCountMode.Original",
        ];

        public override bool IsEnable => playerIdList.Count > 0;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Virus);
            KillCooldown = FloatOptionItem.Create(Id + 10, "VirusKillCooldown", new(0f, 60f, 0.5f), 30f, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Virus])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = BooleanOptionItem.Create(Id + 11, "VirusCanVent", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Virus]);
            ImpostorVision = BooleanOptionItem.Create(Id + 16, "ImpostorVision", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Virus]);
            InfectMax = IntegerOptionItem.Create(Id + 12, "VirusInfectMax", new(1, 15, 1), 2, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Virus])
                .SetValueFormat(OptionFormat.Times);
            KnowTargetRole = BooleanOptionItem.Create(Id + 13, "VirusKnowTargetRole", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Virus]);
            TargetKnowOtherTarget = BooleanOptionItem.Create(Id + 14, "VirusTargetKnowOtherTarget", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Virus]);
            KillInfectedPlayerAfterMeeting = BooleanOptionItem.Create(Id + 15, "VirusKillInfectedPlayerAfterMeeting", false, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Virus]);
            ContagiousPlayersCanKillEachOther = BooleanOptionItem.Create(Id + 18, "ContagiousPlayersCanKillEachOther", true, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Virus]);
            ContagiousCountMode = StringOptionItem.Create(Id + 17, "ContagiousCountMode", ContagiousCountModeStrings, 0, TabGroup.NeutralRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Virus]);
        }

        public override void Init()
        {
            playerIdList = [];
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(InfectMax.GetInt());

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();

        public override bool CanUseKillButton(PlayerControl pc)
        {
            return pc.IsAlive();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetVision(ImpostorVision.GetBool());
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer.GetAbilityUseLimit() < 1) return true;
            InfectedBodies.Add(target.PlayerId);
            return true;
        }

        public static void OnKilledBodyReport(PlayerControl target)
        {
            if (!CanBeInfected(target)) return;

            Utils.GetPlayerById(playerIdList[0]).RpcRemoveAbilityUse();

            if (KillInfectedPlayerAfterMeeting.GetBool())
            {
                InfectedPlayer.Add(target.PlayerId);

                VirusNotify.Add(target.PlayerId, GetString("VirusNoticeMessage2"));
            }
            else
            {
                target.RpcSetCustomRole(CustomRoles.Contagious);

                Utils.NotifyRoles(ForceLoop: true);

                VirusNotify.Add(target.PlayerId, GetString("VirusNoticeMessage"));
            }

            Logger.Info("Add-on assigned:" + target.Data?.PlayerName + " = " + target.GetCustomRole() + " + " + CustomRoles.Contagious, "Assign " + CustomRoles.Contagious);
        }

        public static void OnCheckForEndVoting(PlayerState.DeathReason deathReason, params byte[] exileIds)
        {
            if (!KillInfectedPlayerAfterMeeting.GetBool()) return;

            PlayerControl virus =
                Main.AllAlivePlayerControls.FirstOrDefault(a => a.GetCustomRole() == CustomRoles.Virus);
            if (virus == null || deathReason != PlayerState.DeathReason.Vote) return;

            if (exileIds.Contains(virus.PlayerId))
            {
                InfectedPlayer.Clear();
                return;
            }

            var infectedIdList = new List<byte>();
            foreach (PlayerControl pc in Main.AllAlivePlayerControls)
            {
                bool isInfected = InfectedPlayer.Contains(pc.PlayerId);
                if (!isInfected) continue;

                if (virus.IsAlive())
                {
                    if (!Main.AfterMeetingDeathPlayers.ContainsKey(pc.PlayerId))
                    {
                        pc.SetRealKiller(virus);
                        infectedIdList.Add(pc.PlayerId);
                    }
                }
                else
                {
                    Main.AfterMeetingDeathPlayers.Remove(pc.PlayerId);
                }
            }

            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Infected, [.. infectedIdList]);
            RemoveInfectedPlayer(virus);
        }

        public static void RemoveInfectedPlayer(PlayerControl virus)
        {
            InfectedPlayer.Clear();
        }

        public override bool KnowRole(PlayerControl player, PlayerControl target)
        {
            if (player.Is(CustomRoles.Contagious) && target.Is(CustomRoles.Virus)) return true;
            if (KnowTargetRole.GetBool() && player.Is(CustomRoles.Virus) && target.Is(CustomRoles.Contagious)) return true;
            return TargetKnowOtherTarget.GetBool() && player.Is(CustomRoles.Contagious) && target.Is(CustomRoles.Contagious);
        }

        public static bool CanBeInfected(PlayerControl pc)
        {
            return !pc.Is(CustomRoles.Virus) && !pc.Is(CustomRoles.Contagious) && !pc.Is(CustomRoles.Loyal);
        }
    }
}