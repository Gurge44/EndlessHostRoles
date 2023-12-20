using AmongUs.GameOptions;
using Hazel;
using static TOHE.Options;
using static TOHE.Utils;

namespace TOHE.Roles.Neutral
{
    internal class Hookshot
    {
        private static int Id => 643230;

        private static PlayerControl Hookshot_ => GetPlayerById(HookshotId);
        private static byte HookshotId = byte.MaxValue;

        private static OptionItem KillCooldown;
        private static OptionItem HasImpostorVision;
        public static OptionItem CanVent;

        private static bool ToTargetTP = false;
        public static byte MarkedPlayerId = byte.MaxValue;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Hookshot, 1, zeroOne: false);
            KillCooldown = FloatOptionItem.Create(Id + 2, "KillCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Hookshot])
                .SetValueFormat(OptionFormat.Seconds);
            HasImpostorVision = BooleanOptionItem.Create(Id + 3, "ImpostorVision", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Hookshot]);
            CanVent = BooleanOptionItem.Create(Id + 4, "CanVent", true, TabGroup.NeutralRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Hookshot]);
        }
        public static void Init()
        {
            HookshotId = byte.MaxValue;
            MarkedPlayerId = byte.MaxValue;
        }
        public static void Add(byte playerId)
        {
            HookshotId = playerId;

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }
        public static bool IsEnable => HookshotId != byte.MaxValue;
        public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
        private static void SendRPC()
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncHookshot, SendOption.Reliable, -1);
            writer.Write(ToTargetTP);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader) => ToTargetTP = reader.ReadBoolean();
        public static void ExecuteAction()
        {
            if (MarkedPlayerId == byte.MaxValue) return;

            var markedPlayer = GetPlayerById(MarkedPlayerId);
            if (markedPlayer == null)
            {
                MarkedPlayerId = byte.MaxValue;
                return;
            }

            if (ToTargetTP)
            {
                Hookshot_.TP(markedPlayer);
            }
            else
            {
                markedPlayer.TP(Hookshot_);
            }

            MarkedPlayerId = byte.MaxValue;
        }
        public static void SwitchActionMode()
        {
            ToTargetTP = !ToTargetTP;
            SendRPC();
            NotifyRoles(SpecifySeer: Hookshot_, SpecifyTarget: Hookshot_);
        }
        public static bool OnCheckMurder(PlayerControl target)
        {
            if (target == null) return false;

            return Hookshot_.CheckDoubleTrigger(target, () =>
            {
                MarkedPlayerId = target.PlayerId;
                Hookshot_.SetKillCooldown(5f);
            });
        }
        public static void OnReportDeadBody() => MarkedPlayerId = byte.MaxValue;
        public static string SuffixText => $"<#00ffa5>Mode:</color> <#ffffff>{(ToTargetTP ? "TP to Target" : "Pull Target")}</color>";
    }
}
