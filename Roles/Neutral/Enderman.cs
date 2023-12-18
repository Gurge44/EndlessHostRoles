using AmongUs.GameOptions;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Neutral
{
    internal class Enderman
    {
        private static int Id => 643200;

        private static PlayerControl Enderman_ => GetPlayerById(EndermanId);
        private static byte EndermanId = byte.MaxValue;

        private static OptionItem KillCooldown;
        public static OptionItem CanVent;
        private static OptionItem Time;

        private static (Vector2 POSITION, long MARK_TIMESTAMP, bool TP) MarkedPosition = (Vector2.zero, 0, false);

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Enderman, 1, zeroOne: false);
            KillCooldown = FloatOptionItem.Create(Id + 2, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Enderman])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = BooleanOptionItem.Create(Id + 3, "CanVent", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Enderman]);
            Time = IntegerOptionItem.Create(Id + 4, "EndermanSecondsBeforeTP", new(1, 60, 1), 7, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Enderman])
                .SetValueFormat(OptionFormat.Seconds);
        }
        public static void Init()
        {
            EndermanId = byte.MaxValue;
            MarkedPosition.TP = false;
        }
        public static void Add(byte playerId)
        {
            EndermanId = playerId;

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }
        public static bool IsEnable => EndermanId != byte.MaxValue;
        public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(true);
        public static void MarkPosition()
        {
            if (!IsEnable || Enderman_.HasAbilityCD()) return;
            Enderman_.AddAbilityCD(Time.GetInt() + 2);
            MarkedPosition.MARK_TIMESTAMP = GetTimeStamp();
            MarkedPosition.POSITION = Enderman_.Pos();
            MarkedPosition.TP = true;
            Enderman_.Notify(GetString("MarkDone"));
        }
        public static void OnFixedUpdate()
        {
            if (!IsEnable || !GameStates.IsInTask || !MarkedPosition.TP || !Enderman_.IsAlive() || MarkedPosition.MARK_TIMESTAMP + Time.GetInt() >= GetTimeStamp()) return;
            Enderman_.TP(MarkedPosition.POSITION);
        }
        public static void OnReportDeadBody()
        {
            if (!IsEnable) return;
            MarkedPosition.TP = false;
        }
    }
}
