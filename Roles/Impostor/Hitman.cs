using Hazel;
using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    public static class Hitman
    {
        private static readonly int Id = 640800;
        public static List<byte> playerIdList = [];

        public static OptionItem KillCooldown;
        public static OptionItem SuccessKCD;
        public static OptionItem ShapeshiftCooldown;

        public static byte targetId = byte.MaxValue;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Hitman, 1);
            KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hitman])
                .SetValueFormat(OptionFormat.Seconds);
            SuccessKCD = FloatOptionItem.Create(Id + 11, "HitmanLowKCD", new(0f, 180f, 2.5f), 10f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hitman])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftCooldown = FloatOptionItem.Create(Id + 12, "ShapeshiftCooldown", new(0f, 180f, 2.5f), 10f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Hitman])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public static void Init()
        {
            playerIdList = [];
            targetId = byte.MaxValue;
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }

        public static bool IsEnable => playerIdList.Any();

        public static void SendRPC(byte targetId)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetHitmanTarget, SendOption.Reliable, -1);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            if (AmongUsClient.Instance.AmHost) return;

            targetId = reader.ReadByte();
        }

        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;

            if (target.PlayerId == targetId)
            {
                targetId = byte.MaxValue;
                SendRPC(targetId);
                _ = new LateTask(() =>
                {
                    killer.SetKillCooldown(time: SuccessKCD.GetFloat());
                }, 0.1f, "Hitman Killed Target - SetKillCooldown Task");
            }

            return true;
        }

        public static void OnReportDeadBody()
        {
            if (!GetPlayerById(targetId).IsAlive() || GetPlayerById(targetId).Data.Disconnected)
            {
                targetId = byte.MaxValue;
                SendRPC(targetId);
            }
        }

        public static void OnShapeshift(PlayerControl hitman, PlayerControl target, bool shapeshifting)
        {
            if (target == null || hitman == null || !shapeshifting || targetId != byte.MaxValue || !target.IsAlive()) return;

            targetId = target.PlayerId;
            SendRPC(targetId);
            NotifyRoles(SpecifySeer: hitman);

            //_ = new LateTask(() => { hitman.CmdCheckRevertShapeshift(false); }, 1.5f, "Hitman RpcRevertShapeshift");
        }

        public static string GetTargetText()
        {
            if (targetId == byte.MaxValue) return string.Empty;

            return $"<color=#00ffa5>Target:</color> <color=#ffffff>{GetPlayerById(targetId).GetRealName().RemoveHtmlTags()}</color>";
        }
    }
}
