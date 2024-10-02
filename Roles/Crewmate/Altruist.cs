﻿using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

// ReSharper disable ConvertIfStatementToReturnStatement

namespace EHR.Crewmate
{
    public class Altruist : RoleBase
    {
        public static bool On;
        private static List<Altruist> Instances = [];

        private static OptionItem ReviveTime;
        private static OptionItem ReviveTargetCanReportTheirOwnBody;
        private static OptionItem ReviveTargetsKillerGetsAlert;
        private static OptionItem ReviveTargetsKillerGetsArrow;

        private static HashSet<byte> RevivedPlayers = [];

        private byte AlturistId;

        public long ReviveStartTS;
        private byte ReviveTarget;
        private Vector2 ReviveTargetPos;

        private bool RevivingMode;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            StartSetup(645850)
                .AutoSetupOption(ref ReviveTime, 5, new IntegerValueRule(0, 30, 1), OptionFormat.Seconds)
                .AutoSetupOption(ref ReviveTargetCanReportTheirOwnBody, false)
                .AutoSetupOption(ref ReviveTargetsKillerGetsAlert, true)
                .AutoSetupOption(ref ReviveTargetsKillerGetsArrow, true, overrideParent: ReviveTargetsKillerGetsAlert);
        }

        public override void Init()
        {
            On = false;
            Instances = [];
            RevivedPlayers = [];
        }

        public override void Add(byte playerId)
        {
            On = true;
            Instances.Add(this);
            RevivingMode = true;
            ReviveTarget = byte.MaxValue;
            ReviveStartTS = 0;
            AlturistId = playerId;
            ReviveTargetPos = Vector2.zero;
            RevivedPlayers = [];
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerCooldown = 1f;
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public static bool OnAnyoneCheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
        {
            if (ReviveTargetCanReportTheirOwnBody.GetBool()) return true;
            return !RevivedPlayers.Contains(reporter.PlayerId) || target.PlayerId != reporter.PlayerId;
        }

        public override bool CheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, PlayerControl killer)
        {
            if (!RevivingMode || target.Disconnected) return true;

            var state = Main.PlayerStates[reporter.PlayerId];
            state.deathReason = PlayerState.DeathReason.Sacrifice;
            state.RealKiller = (DateTime.Now, target.PlayerId);
            state.SetDead();
            reporter.RpcExileV2();
            FixedUpdatePatch.LoversSuicide(reporter.PlayerId);

            RevivingMode = false;
            ReviveTarget = target.PlayerId;
            ReviveStartTS = Utils.TimeStamp;
            ReviveTargetPos = reporter.Pos();

            return false;
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (pc.IsAlive() || !GameStates.IsInTask || ReviveStartTS == 0 || ReviveTarget == byte.MaxValue) return;
            if (Utils.TimeStamp - ReviveStartTS < ReviveTime.GetInt())
            {
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                return;
            }

            var rtg = ReviveTarget.GetPlayer();
            rtg?.RpcRevive();
            rtg?.TP(ReviveTargetPos);
            rtg?.Notify(Translator.GetString("RevivedByAltruist"), 15f);

            RevivedPlayers.Add(ReviveTarget);

            var killer = rtg?.GetRealKiller();
            if (killer != null && ReviveTargetsKillerGetsAlert.GetBool())
            {
                if (ReviveTargetsKillerGetsArrow.GetBool()) TargetArrow.Add(killer.PlayerId, ReviveTarget);
                killer.KillFlash();
                killer.Notify(string.Format(Translator.GetString("AltruistKillerAlert"), ReviveTarget.ColoredPlayerName()), 10f);
            }

            ReviveTarget = byte.MaxValue;
            ReviveStartTS = 0;
            ReviveTargetPos = Vector2.zero;
        }

        public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
        {
            if (lowLoad || !ReviveTargetsKillerGetsArrow.GetBool() || GameStates.IsMeeting || ExileController.Instance) return;
            if (RevivedPlayers.FindFirst(x => x.GetPlayer()?.GetRealKiller()?.PlayerId == pc.PlayerId, out var revived))
            {
                var revivedPlayer = revived.GetPlayer();
                if (revivedPlayer == null || !revivedPlayer.IsAlive())
                {
                    TargetArrow.Remove(pc.PlayerId, revived);
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                }
            }
        }

        public override void OnPet(PlayerControl pc)
        {
            RevivingMode = !RevivingMode;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
            OnPet(physics.myPlayer);
        }

        public override void OnReportDeadBody()
        {
            ReviveTarget = byte.MaxValue;
            ReviveStartTS = 0;
            ReviveTargetPos = Vector2.zero;
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
        {
            if (seer.PlayerId != target.PlayerId || seer.PlayerId != AlturistId || meeting || hud) return string.Empty;
            if (ReviveStartTS != 0) return string.Format(Translator.GetString("AltruistSuffixRevive"), ReviveTime.GetInt() - (Utils.TimeStamp - ReviveStartTS));
            return string.Format(Translator.GetString("AltruistSuffix"), Translator.GetString(RevivingMode ? "AltruistReviveMode" : "AltruistReportMode"));
        }
    }
}