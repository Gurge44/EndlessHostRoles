namespace TOHE.Roles.Crewmate
{
    using AmongUs.GameOptions;
    using HarmonyLib;
    using Hazel;
    using InnerNet;
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;
    using System.Linq;
    using TMPro;
    using TOHE.Modules;
    using TOHE.Roles.Neutral;
    using static TOHE.Options;
    using static TOHE.Translator;

    public static class Alchemist
    {
        private static readonly int Id = 5250;
        public static bool IsProtected = false;
        private static List<byte> playerIdList = new();
        private static Dictionary<byte, int> ventedId = new();
        public static int PotionID = 10;
        public static string PlayerName = "";
        public static bool FixNextSabo = false;
        private static Dictionary<byte, long> InvisTime = new();
        private static Dictionary<byte, long> lastTime = new();
        public static bool VisionPotionActive = false;

        public static OptionItem VentCooldown;
        public static OptionItem ShieldDuration;
        public static OptionItem Speed;
        public static OptionItem Vision;
        public static OptionItem VisionOnLightsOut;
        public static OptionItem SpeedDuration;
        public static OptionItem VisionDuration;
        public static OptionItem InvisDuration;

        public static void SetupCustomOption()
        {
            SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Alchemist, 1);
            VentCooldown = FloatOptionItem.Create(Id + 11, "VentCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Seconds);
            ShieldDuration = FloatOptionItem.Create(Id + 12, "AlchemistShieldDur", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Seconds);
            InvisDuration = FloatOptionItem.Create(Id + 13, "AlchemistInvisDur", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Seconds);
            Speed = FloatOptionItem.Create(Id + 14, "AlchemistSpeed", new(0.1f, 5f, 0.1f), 1.5f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                 .SetValueFormat(OptionFormat.Multiplier);
            SpeedDuration = FloatOptionItem.Create(Id + 15, "AlchemistSpeedDur", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Seconds);
            Vision = FloatOptionItem.Create(Id + 16, "AlchemistVision", new(0f, 1f, 0.05f), 0.85f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Multiplier);
            VisionOnLightsOut = FloatOptionItem.Create(Id + 17, "AlchemistVisionOnLightsOut", new(0f, 1f, 0.05f), 0.4f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Multiplier);
            VisionDuration = FloatOptionItem.Create(Id + 18, "AlchemistVisionDur", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Seconds);
            OverrideTasksData.Create(Id + 20, TabGroup.CrewmateRoles, CustomRoles.Alchemist);
        }
        public static void Init()
        {
            playerIdList = new();
            PotionID = 10;
            PlayerName = "";
            FixNextSabo = false;
            ventedId = new();
            InvisTime = new();
            lastTime = new();
            VisionPotionActive = false;
        }
        public static void Add(byte playerId)
        {
            playerIdList.Add(playerId);
            PlayerName = Utils.GetPlayerById(playerId).GetRealName();
        }
        public static bool IsEnable => playerIdList.Any();

        public static void OnTaskComplete(PlayerControl pc)
        {
            PotionID = HashRandom.Next(1, 7);

            switch (PotionID)
            {
                case 1: // Shield
                    pc.RpcSetName(GetString("AlchemistGotShieldPotion"));
                    break;
                case 2: // Suicide
                    pc.RpcSetName(GetString("AlchemistGotSuicidePotion"));
                    break;
                case 3: // TP to random player
                    pc.RpcSetName(GetString("AlchemistGotTPPotion"));
                    break;
                case 4: // Increased speed
                    pc.RpcSetName(GetString("AlchemistGotSpeedPotion"));
                    break;
                case 5: // Quick fix next sabo
                    FixNextSabo = true;
                    pc.Notify(GetString("AlchemistGotQFPotion"), 10f);
                    break;
                case 6: // Invisibility
                    pc.RpcSetName(GetString("AlchemistGotInvisPotion"));
                    break;
                case 7: // Increased vision
                    pc.RpcSetName(GetString("AlchemistGotSightPotion"));
                    break;
                default: // just in case
                    break;
            }
        }

        public static void OnEnterVent(PlayerControl player, int ventId)
        {
            if (!player.Is(CustomRoles.Alchemist)) return;

            switch (PotionID)
            {
                case 1: // Shield
                    IsProtected = true;
                    new LateTask(() => { IsProtected = false; }, ShieldDuration.GetInt());
                    break;
                case 2: // Suicide
                    player.MyPhysics.RpcBootFromVent(ventId);
                    new LateTask(() => {
                        player.SetRealKiller(player);
                        player.RpcMurderPlayerV3(player);
                    }, 1f);
                    break;
                case 3: // TP to random player
                    new LateTask(() =>
                    {
                        var rd = IRandom.Instance;
                        List<PlayerControl> AllAlivePlayer = new();
                        foreach (var pc in Main.AllAlivePlayerControls.Where(x => !Pelican.IsEaten(x.PlayerId) && !x.inVent && !x.onLadder)) AllAlivePlayer.Add(pc);
                        var tar1 = AllAlivePlayer[player.PlayerId];
                        AllAlivePlayer.Remove(tar1);
                        var tar2 = AllAlivePlayer[rd.Next(0, AllAlivePlayer.Count)];
                        Utils.TP(tar1.NetTransform, tar2.GetTruePosition());
                        tar1.RPCPlayCustomSound("Teleport");
                    }, 2f);
                    break;
                case 4: // Increased speed
                    Main.AllPlayerSpeed[player.PlayerId] = Speed.GetFloat();
                    new LateTask(() => { Main.AllPlayerSpeed[player.PlayerId] = AURoleOptions.PlayerSpeedMod; }, SpeedDuration.GetInt());
                    break;
                case 5: // Quick fix next sabo
                    // Done when making the potion
                    break;
                case 6: // Invisibility
                    // Handled by OnCoEnterVent
                    break;
                case 7: // Increased vision
                    VisionPotionActive = true;
                    new LateTask(() => { VisionPotionActive = false; }, VisionDuration.GetFloat());
                    break;
                case 10:
                    break;
                default: // just in case
                    break;
            }

            PotionID = 10;

            player.RpcSetName(PlayerName);
            player.MarkDirtySettings();
        }
        private static long lastFixedTime = 0;
        private static void SendRPC(PlayerControl pc)
        {
            if (pc.AmOwner) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetAlchemistTimer, SendOption.Reliable, pc.GetClientId());
            writer.Write((InvisTime.TryGetValue(pc.PlayerId, out var x) ? x : -1).ToString());
            writer.Write((lastTime.TryGetValue(pc.PlayerId, out var y) ? y : -1).ToString());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            InvisTime = new();
            lastTime = new();
            long invis = long.Parse(reader.ReadString());
            long last = long.Parse(reader.ReadString());
            if (invis > 0) InvisTime.Add(PlayerControl.LocalPlayer.PlayerId, invis);
            if (last > 0) lastTime.Add(PlayerControl.LocalPlayer.PlayerId, last);
        }
        public static void OnCoEnterVent(PlayerPhysics __instance, int ventId)
        {
            PotionID = 10;
            var pc = __instance.myPlayer;
            if (!AmongUsClient.Instance.AmHost) return;
            new LateTask(() =>
            {
                ventedId.Remove(pc.PlayerId);
                ventedId.Add(pc.PlayerId, ventId);

                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(__instance.NetId, 34, SendOption.Reliable, pc.GetClientId());
                writer.WritePacked(ventId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);

                InvisTime.Add(pc.PlayerId, Utils.GetTimeStamp());
                SendRPC(pc);
                NameNotifyManager.Notify(pc, GetString("ChameleonInvisState"), InvisDuration.GetFloat());
            }, 0.5f, "Alchemist Invis");
        }
        public static void OnFixedUpdate(PlayerControl player)
        {
            if (!TOHE.GameStates.IsInTask || !IsEnable) return;

            var now = Utils.GetTimeStamp();

            if (lastFixedTime != now)
            {
                lastFixedTime = now;
                Dictionary<byte, long> newList = new();
                List<byte> refreshList = new();
                foreach (var it in InvisTime)
                {
                    var pc = Utils.GetPlayerById(it.Key);
                    if (pc == null) continue;
                    var remainTime = it.Value + (long)InvisDuration.GetFloat() - now;
                    if (remainTime < 0)
                    {
                        lastTime.Add(pc.PlayerId, now);
                        pc?.MyPhysics?.RpcBootFromVent(ventedId.TryGetValue(pc.PlayerId, out var id) ? id : Main.LastEnteredVent[pc.PlayerId].Id);
                        NameNotifyManager.Notify(pc, GetString("ChameleonInvisStateOut"));
                        pc.RpcResetAbilityCooldown();
                        SendRPC(pc);
                        continue;
                    }
                    else if (remainTime <= 10)
                    {
                        if (!pc.IsModClient()) pc.Notify(string.Format(GetString("ChameleonInvisStateCountdown"), remainTime + 1));
                    }
                    newList.Add(it.Key, it.Value);
                }
                InvisTime.Where(x => !newList.ContainsKey(x.Key)).Do(x => refreshList.Add(x.Key));
                InvisTime = newList;
                refreshList.Do(x => SendRPC(Utils.GetPlayerById(x)));
            }
        }
        public static void RepairSystem(SystemTypes systemType, byte amount)
        {
            FixNextSabo = false;
            PotionID = 10;
            switch (systemType)
            {
                case SystemTypes.Reactor:
                    if (amount is 64 or 65)
                    {
                        ShipStatus.Instance.RpcRepairSystem(SystemTypes.Reactor, 16);
                        ShipStatus.Instance.RpcRepairSystem(SystemTypes.Reactor, 17);
                    }
                    break;
                case SystemTypes.Laboratory:
                    if (amount is 64 or 65)
                    {
                        ShipStatus.Instance.RpcRepairSystem(SystemTypes.Laboratory, 67);
                        ShipStatus.Instance.RpcRepairSystem(SystemTypes.Laboratory, 66);
                    }
                    break;
                case SystemTypes.LifeSupp:
                    if (amount is 64 or 65)
                    {
                        ShipStatus.Instance.RpcRepairSystem(SystemTypes.LifeSupp, 67);
                        ShipStatus.Instance.RpcRepairSystem(SystemTypes.LifeSupp, 66);
                    }
                    break;
                case SystemTypes.Comms:
                    if (amount is 64 or 65)
                    {
                        ShipStatus.Instance.RpcRepairSystem(SystemTypes.Comms, 16);
                        ShipStatus.Instance.RpcRepairSystem(SystemTypes.Comms, 17);
                    }
                    break;
            }
        }
        public static void SwitchSystemRepair(SwitchSystem __instance, byte amount)
        {
            FixNextSabo = false;
            PotionID = 10;
            if (amount is >= 0 and <= 4)
            {
                __instance.ActualSwitches = 0;
                __instance.ExpectedSwitches = 0;
            }
        }
    }
}