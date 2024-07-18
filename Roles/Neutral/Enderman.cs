using AmongUs.GameOptions;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Neutral
{
    internal class Enderman : RoleBase
    {
        private static OptionItem KillCooldown;
        public static OptionItem CanVent;
        private static OptionItem Time;
        private byte EndermanId = byte.MaxValue;

        private (Vector2 POSITION, long MARK_TIMESTAMP, bool TP) MarkedPosition = (Vector2.zero, 0, false);
        private static int Id => 643200;

        private PlayerControl Enderman_ => GetPlayerById(EndermanId);

        public override bool IsEnable => EndermanId != byte.MaxValue;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Enderman);
            KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Enderman])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = new BooleanOptionItem(Id + 3, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Enderman]);
            Time = new IntegerOptionItem(Id + 4, "EndermanSecondsBeforeTP", new(1, 60, 1), 7, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Enderman])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            EndermanId = byte.MaxValue;
            MarkedPosition.TP = false;
        }

        public override void Add(byte playerId)
        {
            EndermanId = playerId;
            MarkedPosition = (Vector2.zero, 0, false);
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
        public override bool CanUseSabotage(PlayerControl pc) => pc.IsAlive() && !(UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool());

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            opt.SetVision(true);
            if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool())
                AURoleOptions.PhantomCooldown = Time.GetInt() + 2f;
            if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool())
                AURoleOptions.ShapeshifterCooldown = Time.GetInt() + 2f;
        }


        public override void OnPet(PlayerControl pc)
        {
            MarkPosition();
        }

        public override bool OnSabotage(PlayerControl pc)
        {
            MarkPosition();
            return false;
        }

        public override bool OnVanish(PlayerControl pc)
        {
            MarkPosition();
            return false;
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting && !UseUnshiftTrigger.GetBool()) return true;
            MarkPosition();
            return false;
        }

        void MarkPosition()
        {
            if (!IsEnable || Enderman_.HasAbilityCD()) return;
            Enderman_.AddAbilityCD(Time.GetInt() + 2);
            MarkedPosition.MARK_TIMESTAMP = TimeStamp;
            MarkedPosition.POSITION = Enderman_.Pos();
            MarkedPosition.TP = true;
            Enderman_.Notify(GetString("MarkDone"));
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!IsEnable || !GameStates.IsInTask || !MarkedPosition.TP || !Enderman_.IsAlive() || MarkedPosition.MARK_TIMESTAMP + Time.GetInt() >= TimeStamp) return;
            Enderman_.TP(MarkedPosition.POSITION);
            MarkedPosition.TP = false;
        }

        public override void OnReportDeadBody()
        {
            if (!IsEnable) return;
            MarkedPosition.TP = false;
        }
    }
}