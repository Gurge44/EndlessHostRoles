using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TOHE.Modules;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Neutral
{
    internal class Mycologist : RoleBase
    {
        private static int Id => 643210;

        private PlayerControl Mycologist_ => GetPlayerById(MycologistId);
        private byte MycologistId = byte.MaxValue;

        private static readonly string[] SpreadMode =
        [
            "VentButtonText", // 0
            "SabotageButtonText", // 1
            "PetButtonText" // 2
        ];

        private static OptionItem KillCooldown;
        private static OptionItem HasImpostorVision;
        public static OptionItem SpreadAction;
        private static OptionItem CD;
        private static OptionItem InfectRadius;
        private static OptionItem InfectTime;

        public readonly List<byte> InfectedPlayers = [];

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Mycologist);
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
            InfectTime = IntegerOptionItem.Create(Id + 6, "InfectDelay", new(0, 60, 1), 5, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Mycologist])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            MycologistId = byte.MaxValue;
            InfectedPlayers.Clear();
        }

        public override void Add(byte playerId)
        {
            MycologistId = playerId;

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override bool IsEnable => MycologistId != byte.MaxValue;
        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());

        void SendRPC()
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncMycologist, SendOption.Reliable);
            writer.Write(MycologistId);
            writer.Write(InfectedPlayers.Count);
            if (InfectedPlayers.Count > 0)
                foreach (var x in InfectedPlayers)
                    writer.Write(x);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            var playerId = reader.ReadByte();
            if (Main.PlayerStates[playerId].Role is not Mycologist mg) return;
            mg.InfectedPlayers.Clear();
            var length = reader.ReadInt32();
            for (int i = 0; i < length; i++)
            {
                mg.InfectedPlayers.Add(reader.ReadByte());
            }
        }

        public override void OnPet(PlayerControl pc)
        {
            if (SpreadAction.GetValue() == 2)
            {
                SpreadSpores();
            }
        }

        public override void OnSabotage(PlayerControl pc)
        {
            if (SpreadAction.GetValue() == 1)
            {
                SpreadSpores();
            }
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            if (SpreadAction.GetValue() == 0 || (SpreadAction.GetValue() == 2 && !UsePets.GetBool()))
            {
                SpreadSpores();
            }
        }

        void SpreadSpores()
        {
            if (!IsEnable || Mycologist_.HasAbilityCD()) return;
            Mycologist_.AddAbilityCD(CD.GetInt());
            _ = new LateTask(() =>
            {
                InfectedPlayers.AddRange(GetPlayersInRadius(InfectRadius.GetFloat(), Mycologist_.Pos()).Select(x => x.PlayerId));
                SendRPC();
                NotifyRoles(SpecifySeer: Mycologist_);
            }, InfectTime.GetFloat(), "Mycologist Infect Time");
            Mycologist_.Notify(GetString("MycologistNotify"));
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target) => IsEnable && target != null && InfectedPlayers.Contains(target.PlayerId);
        public override void AfterMeetingTasks() => Mycologist_.AddAbilityCD(CD.GetInt());
        public override bool CanUseImpostorVentButton(PlayerControl pc) => true;
        public override bool CanUseSabotage(PlayerControl pc) => SpreadAction.GetValue() == 1;
    }
}