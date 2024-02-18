using HarmonyLib;
using Hazel;
using System.Collections.Generic;
using static TOHE.Options;

namespace TOHE.Roles.Impostor
{
    public static class EvilDiviner
    {
        private static readonly int Id = 2700;
        public static List<byte> playerIdList = [];

        private static OptionItem KillCooldown;
        private static OptionItem DivinationMaxCount;
        public static OptionItem EDAbilityUseGainWithEachKill;

        public static Dictionary<byte, List<byte>> DivinationTarget = [];


        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.EvilDiviner);
            KillCooldown = FloatOptionItem.Create(Id + 10, "KillCooldown", new(0f, 180f, 2.5f), 25f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.EvilDiviner])
                .SetValueFormat(OptionFormat.Seconds);
            DivinationMaxCount = IntegerOptionItem.Create(Id + 11, "DivinationMaxCount", new(0, 15, 1), 1, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.EvilDiviner])
                .SetValueFormat(OptionFormat.Times);
            EDAbilityUseGainWithEachKill = FloatOptionItem.Create(Id + 12, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.3f, TabGroup.ImpostorRoles, false)
                .SetParent(CustomRoleSpawnChances[CustomRoles.EvilDiviner])
                .SetValueFormat(OptionFormat.Times);
        }
        public static void Init()
        {
            playerIdList = [];
            DivinationTarget = [];
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            playerId.SetAbilityUseLimit(DivinationMaxCount.GetInt());
            DivinationTarget.TryAdd(playerId, []);
            var pc = Utils.GetPlayerById(playerId);
            pc.AddDoubleTrigger();
        }

        private static void SendRPC(byte playerId, byte targetId)
        {
            if (!IsEnable || !Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetEvilDiviner, SendOption.Reliable, -1);
            writer.Write(playerId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            byte playerId = reader.ReadByte();
            {
                if (DivinationTarget.ContainsKey(playerId))
                    DivinationTarget[playerId].Add(reader.ReadByte());
                else
                    DivinationTarget.Add(playerId, [reader.ReadByte()]);
            }
        }

        public static bool IsEnable => playerIdList.Count > 0;
        public static void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }
        public static bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer.GetAbilityUseLimit() >= 1)
            {
                return killer.CheckDoubleTrigger(target, () => { SetDivination(killer, target); });
            }
            else return true;
        }

        public static bool IsDivination(byte seer, byte target)
        {
            if (DivinationTarget[seer].Contains(target))
            {
                return true;
            }
            return false;
        }
        public static void SetDivination(PlayerControl killer, PlayerControl target)
        {
            if (!IsDivination(killer.PlayerId, target.PlayerId))
            {
                killer.RpcRemoveAbilityUse();
                DivinationTarget[killer.PlayerId].Add(target.PlayerId);
                Logger.Info($"{killer.GetNameWithRole().RemoveHtmlTags()}: Divination target → {target.GetNameWithRole().RemoveHtmlTags()} || Remaining: {killer.GetAbilityUseLimit()} uses", "EvilDiviner");
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);

                SendRPC(killer.PlayerId, target.PlayerId);
                //キルクールの適正化
                killer.SetKillCooldown();
                //killer.RpcGuardAndKill(target);
            }
        }
        public static bool IsShowTargetRole(PlayerControl seer, PlayerControl target)
        {
            var IsWatch = false;
            DivinationTarget.Do(x =>
            {
                if (x.Value != null && seer.PlayerId == x.Key && x.Value.Contains(target.PlayerId) && Utils.GetPlayerById(x.Key).IsAlive())
                    IsWatch = true;
            });
            return IsWatch;
        }
        //public static string GetDivinationCount(byte playerId) => Utils.ColorString(DivinationCount[playerId] > 0 ? Utils.GetRoleColor(CustomRoles.EvilDiviner).ShadeColor(0.25f) : Color.gray, DivinationCount.TryGetValue(playerId, out var shotLimit) ? $"({shotLimit})" : "Invalid");
    }
}