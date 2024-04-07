using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Utils;

namespace EHR.Roles.Impostor
{
    public class Hitman : RoleBase
    {
        private const int Id = 640800;
        public static List<byte> playerIdList = [];

        public static OptionItem KillCooldown;
        public static OptionItem SuccessKCD;
        public static OptionItem ShapeshiftCooldown;

        public byte TargetId = byte.MaxValue;
        private byte HitmanId = byte.MaxValue;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Hitman);
            KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Hitman])
                .SetValueFormat(OptionFormat.Seconds);
            SuccessKCD = FloatOptionItem.Create(Id + 11, "HitmanLowKCD", new(0f, 180f, 2.5f), 10f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Hitman])
                .SetValueFormat(OptionFormat.Seconds);
            ShapeshiftCooldown = FloatOptionItem.Create(Id + 12, "ShapeshiftCooldown", new(0f, 180f, 2.5f), 10f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Hitman])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            playerIdList = [];
            TargetId = byte.MaxValue;
            HitmanId = byte.MaxValue;
        }

        public override void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            HitmanId = playerId;
            TargetId = byte.MaxValue;
        }

        public override bool IsEnable => playerIdList.Count > 0;

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }

        void SendRPC()
        {
            if (!DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetHitmanTarget, SendOption.Reliable);
            writer.Write(HitmanId);
            writer.Write(TargetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public void ReceiveRPC(byte id)
        {
            TargetId = id;
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null) return false;
            if (target == null) return false;

            if (target.PlayerId == TargetId)
            {
                TargetId = byte.MaxValue;
                SendRPC();
                _ = new LateTask(() => { killer.SetKillCooldown(time: SuccessKCD.GetFloat()); }, 0.1f, "Hitman Killed Target - SetKillCooldown Task");
            }

            return true;
        }

        public static void CheckAndResetTargets()
        {
            foreach (var id in playerIdList)
            {
                if (Main.PlayerStates[id].Role is Hitman { IsEnable: true } hm)
                {
                    hm.OnReportDeadBody();
                }
            }
        }

        public override void OnReportDeadBody()
        {
            var target = GetPlayerById(TargetId);
            if (!target.IsAlive() || target.Data.Disconnected)
            {
                TargetId = byte.MaxValue;
                SendRPC();
            }
        }

        public override bool OnShapeshift(PlayerControl hitman, PlayerControl target, bool shapeshifting)
        {
            if (target == null || hitman == null || !shapeshifting || TargetId != byte.MaxValue || !target.IsAlive()) return false;

            TargetId = target.PlayerId;
            SendRPC();
            NotifyRoles(SpecifySeer: hitman, SpecifyTarget: target);

            return false;
        }

        public static string GetTargetText(byte hitman)
        {
            var id = (Main.PlayerStates[hitman].Role as Hitman)?.TargetId ?? byte.MaxValue;
            return id == byte.MaxValue ? string.Empty : $"<color=#00ffa5>Target:</color> <color=#ffffff>{GetPlayerById(id).GetRealName().RemoveHtmlTags()}</color>";
        }
    }
}