using AmongUs.GameOptions;
using UnityEngine;

// ReSharper disable ConvertIfStatementToReturnStatement

namespace EHR.Crewmate
{
    public class Altruist : RoleBase
    {
        public static bool On;

        private static OptionItem ReviveTime;
        private byte AlturistId;
        private long ReviveStartTS;
        private byte ReviveTarget;
        private Vector2 ReviveTargetPos;

        private bool RevivingMode;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            StartSetup(645850).AutoSetupOption(ref ReviveTime, 5, new IntegerValueRule(0, 30, 1), OptionFormat.Seconds);
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
            RevivingMode = true;
            ReviveTarget = byte.MaxValue;
            ReviveStartTS = 0;
            AlturistId = playerId;
            ReviveTargetPos = Vector2.zero;
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.EngineerCooldown = 1f;
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override bool CheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, PlayerControl killer)
        {
            if (!RevivingMode) return true;

            var state = Main.PlayerStates[reporter.PlayerId];
            state.deathReason = PlayerState.DeathReason.Sacrifice;
            state.SetDead();
            reporter.RpcExileV2();

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

            ReviveTarget = byte.MaxValue;
            ReviveStartTS = 0;
            ReviveTargetPos = Vector2.zero;
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
            if (seer.PlayerId != target.PlayerId || seer.PlayerId != AlturistId || meeting || (seer.IsModClient() && !hud)) return string.Empty;
            if (ReviveStartTS != 0) return string.Format(Translator.GetString("AltruistSuffixRevive"), ReviveTime.GetInt() - (Utils.TimeStamp - ReviveStartTS));
            return string.Format(Translator.GetString("AltruistSuffix"), Translator.GetString(RevivingMode ? "AltruistReviveMode" : "AltruistReportMode"));
        }
    }
}