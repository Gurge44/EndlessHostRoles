using System.Collections.Generic;
using Hazel;

using static TOHE.Translator;
using static TOHE.Options;

namespace TOHE.Roles.Neutral
{
    public static class Baker
    {
        private static readonly int Id = 11000;
        public static List<byte> playerIdList = new();
        public static List<byte> NplayerIdList = new();

    //    public static Dictionary<byte, PlayerControl> PoisonPlayer = new();
     //   public static OptionItem BakerChangeChances;
     public static OverrideTasksData BakerTasks;

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Baker);
            BakerTasks = OverrideTasksData.Create(Id + 12, TabGroup.NeutralRoles, CustomRoles.Baker);

          /*  BakerChangeChances = IntegerOptionItem.Create(Id + 10, "BakerChangeChances", new(0, 50, 1), 10, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Baker])
                .SetValueFormat(OptionFormat.Percent); */
        }
        public static void Init()
        {
            playerIdList = new();
            NplayerIdList = new();
     //       PoisonPlayer = new();
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
       //     PoisonPlayer.Add(playerId, null);
        }
        public static bool IsEnable()
        {
            return playerIdList.Count > 0;
        }
        public static bool IsNEnable()
        {
            return NplayerIdList.Count > 0;
        }
        public static bool IsNAlive()
        {
            foreach (var BakerId in NplayerIdList)
            {
                if (Utils.GetPlayerById(BakerId).IsAlive())
                    return true;
            }
            return false;
        }
        private static void SendRPC(byte BakerId, byte targetId = byte.MaxValue)
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.DoPoison, SendOption.Reliable, -1);
            writer.Write(BakerId);
            writer.Write(targetId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            var BakerId = reader.ReadByte();
            var targetId = reader.ReadByte();
      /*      if (targetId != byte.MaxValue)
            {
                PoisonPlayer[BakerId].PlayerId = targetId;
            }
            else
            {
                PoisonPlayer[BakerId] = null;
            } */
        }

    /*    public static bool HavePoisonedPlayer()
        {
            foreach (var BakerId in NplayerIdList)
            {
                if (PoisonPlayer[BakerId] != null)
                {
                    return true;
                }
            }
            return false;
        } */
        public static bool IsPoisoned(PlayerControl target)
        {
            foreach (var BakerId in NplayerIdList)
            {
              //  if (PoisonPlayer[BakerId] == target)
                {
                    return true;
                }
            }
            return false;
        }

   /*     public static void OnCheckForEndVoting(byte exiled)
        {
            foreach (var BakerId in NplayerIdList)
            {
                var BakerPc = Utils.GetPlayerById(BakerId);
                var target = PoisonPlayer[BakerId];
                var targetId = target.PlayerId;
                if (BakerId != exiled)
                {
                    if (!Main.PlayerStates[targetId].IsDead)
                    {
                        target.SetRealKiller(BakerPc);
                        CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Poison, targetId);
                    }
                }
                target = null;
                SendRPC(BakerId);

                if (!BakerPc.IsAlive()) NplayerIdList.Remove(BakerId);
            }
        } */
        public static void AfterMeetingTasks()
        {
            if (!IsNAlive()) return;

            //次のターゲットを決めておく
            List<PlayerControl> targetList = new();
            var rand = IRandom.Instance;
            foreach (var p in Main.AllAlivePlayerControls)
            {
                if (p.Is(CustomRoles.Famine)) continue;
                targetList.Add(p);
            }
            foreach (var BakerId in NplayerIdList)
            {
                var PoisonedPlayer = targetList[rand.Next(targetList.Count)];
          //      PoisonPlayer[BakerId] = PoisonedPlayer;
                SendRPC(BakerId, PoisonedPlayer.PlayerId);
           //     Logger.Info($"{Utils.GetPlayerById(BakerId).GetNameWithRole()}の次ターン配布先：{PoisonedPlayer.GetNameWithRole()}", "Famine");
            }
        }

        public static void FamineKilledTasks(byte BakerId)
        {
       //     PoisonPlayer[BakerId] = null;
            SendRPC(BakerId);
            Logger.Info($"{Utils.GetPlayerById(BakerId).GetNameWithRole()}の配布毒パン回収", "Famine");
        }

   /*     public static string GetPoisonMark(PlayerControl target, bool isMeeting)
        {
            if (isMeeting && IsNAlive() && IsPoisoned(target))
            {
                if(target.IsAlive())
                    return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Famine), "θ");
            }
            return "";
        } */
        public static void SendAliveMessage(PlayerControl pc)
        {
            if (pc.Is(CustomRoles.Famine) && !pc.Data.IsDead && !pc.Data.Disconnected)
            {
             //   if (PoisonPlayer[pc.PlayerId].IsAlive())
                {
                    Utils.SendMessage(GetString("BakerChangeNow"), title: $"<color={Utils.GetRoleColorCode(CustomRoles.Baker)}>{GetString("PanAliveMessageTitle")}</color>");
                }
            }
            if (pc.Is(CustomRoles.Baker) && !pc.Data.IsDead && !pc.Data.Disconnected)
            {
                string panMessage = "";
                int chance = UnityEngine.Random.Range(1, 101);
                if (pc.AllTasksCompleted())
                {
                    panMessage = GetString("BakerChange");
                    pc.RpcSetCustomRole(CustomRoles.Famine);
                    playerIdList.Remove(pc.PlayerId);
                    NplayerIdList.Add(pc.PlayerId);
                }
                else if (chance <= 77) panMessage = GetString("PanAlive");
                else if (chance <= 79) panMessage = GetString("PanAlive");
                else if (chance <= 81) panMessage = GetString("PanAlive");
                else if (chance <= 82) panMessage = GetString("PanAlive");
                else if (chance <= 84) panMessage = GetString("PanAlive");
                else if (chance <= 86) panMessage = GetString("PanAlive");
                else if (chance <= 87) panMessage = GetString("PanAlive");
                else if (chance <= 88) panMessage = GetString("PanAlive");
                else if (chance <= 90) panMessage = GetString("PanAlive");
                else if (chance <= 92) panMessage = GetString("PanAlive");
                else if (chance <= 94) panMessage = GetString("PanAlive");
                else if (chance <= 96) panMessage = GetString("PanAlive");
                else if (chance <= 98)
                {
                    List<PlayerControl> targetList = new();
                    var rand = IRandom.Instance;
                    foreach (var p in Main.AllAlivePlayerControls)
                    {
                        if (p.Is(CustomRoles.Baker)) continue;
                        targetList.Add(p);
                    }
                    var TargetPlayer = targetList[rand.Next(targetList.Count)];
                    panMessage = string.Format(Translator.GetString("PanAlive"), TargetPlayer.GetRealName());
                }
                else if (chance <= 100)
                {
                    List<PlayerControl> targetList = new();
                    var rand = IRandom.Instance;
                    foreach (var p in Main.AllAlivePlayerControls)
                    {
                        if (p.Is(CustomRoles.Baker)) continue;
                        targetList.Add(p);
                    }
                    var TargetPlayer = targetList[rand.Next(targetList.Count)];
                    panMessage = string.Format(Translator.GetString("PanAlive"), TargetPlayer.GetRealName());
                }

                Utils.SendMessage(panMessage, title: $"<color={Utils.GetRoleColorCode(CustomRoles.Baker)}>{GetString("PanAliveMessageTitle")}</color>");
            }
        }
    }
}
