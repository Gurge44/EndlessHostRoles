namespace TOHE.Roles.Crewmate
{
    using HarmonyLib;
    using Hazel;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using TOHE.Modules;
    using TOHE.Roles.Neutral;
    using static TOHE.Options;
    using static TOHE.Translator;

    public static class Alchemist
    {
        private static readonly int Id = 5250;
        public static bool IsProtected;
        private static List<byte> playerIdList = new();
        private static Dictionary<byte, int> ventedId = new();
        public static byte PotionID = 10;
        public static string PlayerName = string.Empty;
        private static Dictionary<byte, long> InvisTime = new();
        public static bool VisionPotionActive;
        public static bool FixNextSabo;

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
            PlayerName = string.Empty;
            ventedId = new();
            InvisTime = new();
            FixNextSabo = false;
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
            PotionID = (byte)HashRandom.Next(1, 8);

            switch (PotionID)
            {
                case 1: // Shield
                    pc.Notify(GetString("AlchemistGotShieldPotion"), 15f);
                    break;
                case 2: // Suicide
                    pc.Notify(GetString("AlchemistGotSuicidePotion"), 15f);
                    break;
                case 3: // TP to random player
                    pc.Notify(GetString("AlchemistGotTPPotion"), 15f);
                    break;
                case 4: // Increased speed
                    pc.Notify(GetString("AlchemistGotSpeedPotion"), 15f);
                    break;
                case 5: // Quick fix next sabo
                    FixNextSabo = true;
                    PotionID = 10;
                    pc.Notify(GetString("AlchemistGotQFPotion"), 15f);
                    break;
                case 6: // Invisibility
                    pc.Notify(GetString("AlchemistGotInvisPotion"), 15f);
                    break;
                case 7: // Increased vision
                    pc.Notify(GetString("AlchemistGotSightPotion"), 15f);
                    break;
                default: // just in case
                    break;
            }
        }

        public static void OnEnterVent(PlayerControl player, int ventId, bool isPet = false)
        {
            if (!player.Is(CustomRoles.Alchemist)) return;

            if (Main.AlchemistCD.ContainsKey(player.PlayerId))
            {
                //if (!NameNotifyManager.Notice.ContainsKey(player.PlayerId)) player.Notify(GetString("AbilityOnCooldown"));
                return;
            }

            NameNotifyManager.Notice.Remove(player.PlayerId);

            switch (PotionID)
            {
                case 1: // Shield
                    IsProtected = true;
                    player.Notify(GetString("AlchemistShielded"), ShieldDuration.GetInt());
                    _ = new LateTask(() => { IsProtected = false; player.Notify(GetString("AlchemistShieldOut")); }, ShieldDuration.GetInt());
                    break;
                case 2: // Suicide
                    if (!isPet) player.MyPhysics.RpcBootFromVent(ventId);
                    _ = new LateTask(() =>
                    {
                        player.SetRealKiller(player);
                        player.Kill(player);
                        Main.PlayerStates[player.PlayerId].deathReason = PlayerState.DeathReason.Poison;
                    }, !isPet ? 1f : 0.1f);
                    break;
                case 3: // TP to random player
                    _ = new LateTask(() =>
                    {
                        var rd = IRandom.Instance;
                        List<PlayerControl> AllAlivePlayer = new();
                        foreach (var pc in Main.AllAlivePlayerControls.Where(x => !Pelican.IsEaten(x.PlayerId) && !x.inVent && !x.onLadder)) AllAlivePlayer.Add(pc);
                        var tar1 = AllAlivePlayer[player.PlayerId];
                        AllAlivePlayer.Remove(tar1);
                        var tar2 = AllAlivePlayer[rd.Next(0, AllAlivePlayer.Count)];
                        Utils.TP(tar1.NetTransform, tar2.GetTruePosition());
                        tar1.RPCPlayCustomSound("Teleport");
                    }, !isPet ? 2f : 0.1f);
                    break;
                case 4: // Increased speed
                    player.Notify(GetString("AlchemistHasSpeed"));
                    player.MarkDirtySettings();
                    var tmpSpeed = Main.AllPlayerSpeed[player.PlayerId];
                    Main.AllPlayerSpeed[player.PlayerId] = Speed.GetFloat();
                    _ = new LateTask(() =>
                    {
                        Main.AllPlayerSpeed[player.PlayerId] = Main.AllPlayerSpeed[player.PlayerId] - Speed.GetFloat() + tmpSpeed;
                        player.MarkDirtySettings();
                        player.Notify(GetString("AlchemistSpeedOut"));
                    }, SpeedDuration.GetInt());
                    break;
                case 5: // Quick fix next sabo
                    // Done when making the potion
                    break;
                case 6: // Invisibility
                    // Handled by OnCoEnterVent
                    break;
                case 7: // Increased vision
                    VisionPotionActive = true;
                    player.MarkDirtySettings();
                    player.Notify(GetString("AlchemistHasVision"), VisionDuration.GetFloat());
                    _ = new LateTask(() => { VisionPotionActive = false; player.MarkDirtySettings(); player.Notify(GetString("AlchemistVisionOut")); }, VisionDuration.GetFloat());
                    break;
                case 10:
                    if (!isPet) player.MyPhysics.RpcBootFromVent(ventId);
                    player.Notify(GetString("AlchemistNoPotion"));
                    break;
                default: // just in case
                    break;
            }

            Main.AlchemistCD.TryAdd(player.PlayerId, Utils.GetTimeStamp());

            PotionID = 10;
        }
        private static long lastFixedTime;
        public static bool IsInvis(byte id) => InvisTime.ContainsKey(id);
        private static void SendRPC(PlayerControl pc)
        {
            if (pc.AmOwner) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetAlchemistTimer, SendOption.Reliable, pc.GetClientId());
            writer.Write((InvisTime.TryGetValue(pc.PlayerId, out var x) ? x : -1).ToString());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }
        public static void ReceiveRPC(MessageReader reader)
        {
            InvisTime = new();
            long invis = long.Parse(reader.ReadString());
            _ = long.Parse(reader.ReadString());
            if (invis > 0) InvisTime.Add(PlayerControl.LocalPlayer.PlayerId, invis);
        }
        public static void OnCoEnterVent(PlayerPhysics __instance, int ventId)
        {
            PotionID = 10;
            var pc = __instance.myPlayer;
            NameNotifyManager.Notice.Remove(pc.PlayerId);
            if (!AmongUsClient.Instance.AmHost) return;
            _ = new LateTask(() =>
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
            if (!GameStates.IsInTask || !IsEnable) return;

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
        public static string GetHudText(PlayerControl pc)
        {
            if (pc == null || !GameStates.IsInTask || !PlayerControl.LocalPlayer.IsAlive()) return string.Empty;
            var str = new StringBuilder();
            if (IsInvis(pc.PlayerId))
            {
                var remainTime = InvisTime[pc.PlayerId] + (long)InvisDuration.GetFloat() - Utils.GetTimeStamp();
                str.Append(string.Format(GetString("ChameleonInvisStateCountdown"), remainTime + 1));
            }
            else
            {
                switch (PotionID)
                {
                    case 1: // Shield
                        str.Append("<color=#00ffa5>Potion in store:</color> <b><color=#00ff97>Shield Potion</color></b>");
                        break;
                    case 2: // Suicide
                        str.Append("<color=#00ffa5>Potion in store:</color> <b><color=#ff0000>Awkward Potion</color></b>");
                        break;
                    case 3: // TP to random player
                        str.Append("<color=#00ffa5>Potion in store:</color> <b><color=#42d1ff>Teleport Potion</color></b>");
                        break;
                    case 4: // Increased speed
                        str.Append("<color=#00ffa5>Potion in store:</color> <b><color=#ff8400>Speed Potion</color></b>");
                        break;
                    case 5: // Quick fix next sabo
                        str.Append("<color=#00ffa5>Potion in store:</color> <b><color=#3333ff>Quick Fix Potion</color></b>");
                        break;
                    case 6: // Invisibility
                        str.Append("<color=#00ffa5>Potion in store:</color> <b><color=#01c834>Invisibility Potion</color></b>");
                        break;
                    case 7: // Increased vision
                        str.Append("<color=#00ffa5>Potion in store:</color> <b><color=#eee5be>Sight Potion</color></b>");
                        break;
                    case 10:
                        str.Append("<color=#00ffa5>Potion in store:</color> <color=#888888>None</color>");
                        break;
                    default: // just in case
                        break;
                }
                if (FixNextSabo) str.Append("\n<b><color=#3333ff>Quick Fix Potion</color></b> waiting for use");
            }
            if (UsePets.GetBool() && Main.AlchemistCD.TryGetValue(pc.PlayerId, out var cd))
            {
                str.Append($"\n<color=#00ffa5>CD:</color> <b>{cd}</b>s");
            }
            return str.ToString();
        }
        public static string GetProgressText(int playerId)
        {
            if (Utils.GetPlayerById(playerId) == null || !GameStates.IsInTask || !PlayerControl.LocalPlayer.IsAlive() || Utils.GetPlayerById(playerId).IsModClient()) return string.Empty;
            var str = new StringBuilder();
            switch (PotionID)
            {
                case 1: // Shield
                    str.Append(" <color=#00ffa5>Stored:</color> <color=#00ff97>Shield Potion</color>");
                    break;
                case 2: // Suicide
                    str.Append(" <color=#00ffa5>Stored:</color> <color=#ff0000>Awkward Potion</color>");
                    break;
                case 3: // TP to random player
                    str.Append(" <color=#00ffa5>Stored:</color> <color=#42d1ff>Teleport Potion</color>");
                    break;
                case 4: // Increased speed
                    str.Append(" <color=#00ffa5>Stored:</color> <color=#ff8400>Speed Potion</color>");
                    break;
                case 5: // Quick fix next sabo
                    str.Append(" <color=#00ffa5>Stored:</color> <color=#3333ff>Quick Fix Potion</color>");
                    break;
                case 6: // Invisibility
                    str.Append(" <color=#00ffa5>Stored:</color> <color=#01c834>Invisibility Potion</color>");
                    break;
                case 7: // Increased vision
                    str.Append(" <color=#00ffa5>Stored:</color> <color=#eee5be>Sight Potion</color>");
                    break;
                default:
                    break;
            }
            if (FixNextSabo) str.Append(" <color=#777777>(<color=#3333ff>Quick Fix</color>)</color>");
            return str.ToString();
        }
        public static void RepairSystem(SystemTypes systemType, byte amount)
        {
            FixNextSabo = false;
            switch (systemType)
            {
                case SystemTypes.Reactor:
                    if (amount is 64 or 65)
                    {
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Reactor, 16);
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Reactor, 17);
                    }
                    break;
                case SystemTypes.Laboratory:
                    if (amount is 64 or 65)
                    {
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Laboratory, 67);
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Laboratory, 66);
                    }
                    break;
                case SystemTypes.LifeSupp:
                    if (amount is 64 or 65)
                    {
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.LifeSupp, 67);
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.LifeSupp, 66);
                    }
                    break;
                case SystemTypes.Comms:
                    if (amount is 64 or 65)
                    {
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 16);
                        ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 17);
                    }
                    break;
            }
        }
    }
}