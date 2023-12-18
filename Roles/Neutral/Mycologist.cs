using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Utils;

namespace TOHE.Roles.Neutral
{
    internal class Mycologist
    {
        private static int Id => 643210;

        private static PlayerControl Mycologist_ => GetPlayerById(MycologistId);
        private static byte MycologistId = byte.MaxValue;

        private static readonly string[] SpreadMode = [
            "VentButtonText",     // 0
            "SabotageButtonText", // 1
            "PetButtonText"       // 2
        ];

        private static OptionItem KillCooldown;
        private static OptionItem HasImpostorVision;
        public static OptionItem SpreadAction;
        private static OptionItem CD;
        private static OptionItem InfectRadius;
        private static OptionItem InfectTime;

        public static List<byte> InfectedPlayers = [];

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Mycologist, 1, zeroOne: false);
            KillCooldown = FloatOptionItem.Create(Id + 2, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mycologist])
                .SetValueFormat(OptionFormat.Seconds);
            HasImpostorVision = BooleanOptionItem.Create(Id + 7, "ImpostorVision", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mycologist]);
            SpreadAction = StringOptionItem.Create(Id + 3, "MycologistAction", SpreadMode, 1, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mycologist]);
            CD = IntegerOptionItem.Create(Id + 4, "AbilityCooldown", new(1, 90, 1), 15, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mycologist])
                .SetValueFormat(OptionFormat.Seconds);
            InfectRadius = FloatOptionItem.Create(Id + 5, "InfectRadius", new(0.1f, 5f, 0.1f), 3f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mycologist])
                .SetValueFormat(OptionFormat.Multiplier);
            InfectTime = IntegerOptionItem.Create(Id + 6, "PlagueDoctorInfectTime", new(0, 60, 1), 5, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mycologist])
                .SetValueFormat(OptionFormat.Seconds);
        }
        public static void Init()
        {
            MycologistId = byte.MaxValue;
            InfectedPlayers.Clear();
        }
        public static void Add(byte playerId)
        {
            MycologistId = playerId;

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }
        public static bool IsEnable => MycologistId != byte.MaxValue;
        public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
        public static void SpreadSpores()
        {
            if (!IsEnable || Mycologist_.HasAbilityCD()) return;
            Mycologist_.AddAbilityCD(CD.GetInt());
            _ = new LateTask(() =>
            {
                InfectedPlayers.AddRange(GetPlayersInRadius(InfectRadius.GetFloat(), Mycologist_.Pos()).Select(x => x.PlayerId));
            }, InfectTime.GetFloat(), "Mycologist Infect Time");
        }
        public static bool OnCheckMurder(PlayerControl target) => IsEnable && target != null && InfectedPlayers.Contains(target.PlayerId);
        public static void AfterMeetingTasks()
        {
            Mycologist_.AddAbilityCD(CD.GetInt());
        }
    }
}
