using System.Collections.Generic;
using System.Linq;
using static TOHE.Options;
using static TOHE.Utils;

namespace TOHE.Roles.Impostor
{
    public static class Hitman
    {
        private static readonly int Id = 640800;
        public static List<byte> playerIdList = new();

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
            playerIdList = new();
            targetId = byte.MaxValue;
        }

        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
        }

        public static bool IsEnable => playerIdList.Any();

        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;

            if (target.PlayerId == targetId)
            {
                targetId = byte.MaxValue;
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
            }
        }

        public static void OnShapeshift(PlayerControl hitman, PlayerControl target, bool shapeshifting)
        {
            if (target == null || hitman == null || !shapeshifting || targetId != byte.MaxValue || !target.IsAlive()) return;

            targetId = target.PlayerId;

            _ = new LateTask(() => { hitman.RpcShapeshift(hitman, false); }, 1.5f, "Hitman RpcRevertShapeshift");
        }

        public static string GetProgressText()
        {
            if (targetId == byte.MaxValue) return string.Empty;

            return $"  <color=#00ffa5>Target:</color> <color=#ffffff>{GetPlayerById(targetId).GetRealName().RemoveHtmlTags()}</color>";
        }
    }
}
