using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using Hazel;

namespace EHR.Crewmate
{
    using static Options;
    using static Translator;

    public class Alchemist : RoleBase
    {
        private const int Id = 5250;
        private static List<byte> PlayerIdList = [];

        public static OptionItem VentCooldown;
        public static OptionItem ShieldDuration;
        public static OptionItem Speed;
        public static OptionItem Vision;
        public static OptionItem VisionOnLightsOut;
        public static OptionItem SpeedDuration;
        public static OptionItem VisionDuration;
        public static OptionItem InvisDuration;

        private byte AlchemistId;
        public bool FixNextSabo;
        private long InvisTime = -10;

        public bool IsProtected;

        private long lastFixedTime;
        public string PlayerName = string.Empty;
        public byte PotionID = 10;
        private int ventedId = -10;
        public bool VisionPotionActive;

        public override bool IsEnable => PlayerIdList.Count > 0;
        bool IsInvis => InvisTime != -10;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Alchemist);
            VentCooldown = new FloatOptionItem(Id + 11, "AlchemistAbilityCooldown", new(0f, 70f, 1f), 15f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Seconds);
            ShieldDuration = new FloatOptionItem(Id + 12, "AlchemistShieldDur", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Seconds);
            InvisDuration = new FloatOptionItem(Id + 13, "AlchemistInvisDur", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Seconds);
            Speed = new FloatOptionItem(Id + 14, "AlchemistSpeed", new(0.1f, 5f, 0.1f), 1.5f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Multiplier);
            SpeedDuration = new FloatOptionItem(Id + 15, "AlchemistSpeedDur", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Seconds);
            Vision = new FloatOptionItem(Id + 16, "AlchemistVision", new(0f, 1f, 0.05f), 0.85f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Multiplier);
            VisionOnLightsOut = new FloatOptionItem(Id + 17, "AlchemistVisionOnLightsOut", new(0f, 1f, 0.05f), 0.4f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Multiplier);
            VisionDuration = new FloatOptionItem(Id + 18, "AlchemistVisionDur", new(5f, 70f, 1f), 20f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Alchemist])
                .SetValueFormat(OptionFormat.Seconds);
            OverrideTasksData.Create(Id + 20, TabGroup.CrewmateRoles, CustomRoles.Alchemist);
        }

        public override void Init()
        {
            PlayerIdList = [];
            PotionID = 10;
            PlayerName = string.Empty;
            ventedId = -10;
            InvisTime = -10;
            FixNextSabo = false;
            VisionPotionActive = false;
            AlchemistId = byte.MaxValue;
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            PlayerName = Utils.GetPlayerById(playerId).GetRealName();
            AlchemistId = playerId;
            PotionID = 10;
            ventedId = -10;
            InvisTime = -10;
            FixNextSabo = false;
            VisionPotionActive = false;
        }

        void SendRPCData()
        {
            if (!IsEnable || !Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetAlchemistPotion, SendOption.Reliable);
            writer.Write(AlchemistId);
            writer.Write(IsProtected);
            writer.Write(PotionID);
            writer.Write(PlayerName);
            writer.Write(VisionPotionActive);
            writer.Write(FixNextSabo);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPCData(MessageReader reader)
        {
            byte id = reader.ReadByte();
            if (Main.PlayerStates[id].Role is not Alchemist am) return;
            am.IsProtected = reader.ReadBoolean();
            am.PotionID = reader.ReadByte();
            am.PlayerName = reader.ReadString();
            am.VisionPotionActive = reader.ReadBoolean();
            am.FixNextSabo = reader.ReadBoolean();
        }

        public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
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
            }
        }

        public override void OnPet(PlayerControl pc)
        {
            DrinkPotion(pc, 0, true);
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            DrinkPotion(pc, vent.Id);
        }

        public static void DrinkPotion(PlayerControl player, int ventId, bool isPet = false)
        {
            if (Main.PlayerStates[player.PlayerId].Role is not Alchemist am) return;

            switch (am.PotionID)
            {
                case 1: // Shield
                    am.IsProtected = true;
                    player.Notify(GetString("AlchemistShielded"), ShieldDuration.GetInt());
                    LateTask.New(() =>
                    {
                        am.IsProtected = false;
                        player.Notify(GetString("AlchemistShieldOut"));
                    }, ShieldDuration.GetInt(), "Alchemist Shield");
                    break;
                case 2: // Suicide
                    if (!isPet) player.MyPhysics.RpcBootFromVent(ventId);
                    LateTask.New(() => { player.Suicide(PlayerState.DeathReason.Poison); }, !isPet ? 1f : 0.1f, "Alchemist Suicide");
                    break;
                case 3: // TP to random player
                    LateTask.New(() =>
                    {
                        List<PlayerControl> allAlivePlayer = [.. Main.AllAlivePlayerControls.Where(x => !Pelican.IsEaten(x.PlayerId) && !x.inVent && !x.onLadder).ToArray()];
                        var tar1 = allAlivePlayer[player.PlayerId];
                        allAlivePlayer.Remove(tar1);
                        var tar2 = allAlivePlayer.RandomElement();
                        tar1.TP(tar2);
                        tar1.RPCPlayCustomSound("Teleport");
                    }, !isPet ? 2f : 0.1f, "AlchemistTPToRandomPlayer");
                    break;
                case 4: // Increased speed
                    player.Notify(GetString("AlchemistHasSpeed"));
                    player.MarkDirtySettings();
                    var tmpSpeed = Main.AllPlayerSpeed[player.PlayerId];
                    Main.AllPlayerSpeed[player.PlayerId] = Speed.GetFloat();
                    LateTask.New(() =>
                    {
                        Main.AllPlayerSpeed[player.PlayerId] = Main.AllPlayerSpeed[player.PlayerId] - Speed.GetFloat() + tmpSpeed;
                        player.MarkDirtySettings();
                        player.Notify(GetString("AlchemistSpeedOut"));
                    }, SpeedDuration.GetInt(), "Alchemist Speed");
                    break;
                case 5: // Quick fix next sabo
                    // Done when making the potion
                    break;
                case 6: // Invisibility
                    // Handled by OnCoEnterVent
                    break;
                case 7: // Increased vision
                    am.VisionPotionActive = true;
                    player.MarkDirtySettings();
                    player.Notify(GetString("AlchemistHasVision"), VisionDuration.GetFloat());
                    LateTask.New(() =>
                    {
                        am.VisionPotionActive = false;
                        player.MarkDirtySettings();
                        player.Notify(GetString("AlchemistVisionOut"));
                    }, VisionDuration.GetFloat(), "Alchemist Vision");
                    break;
                case 10 when !player.Is(CustomRoles.Nimble):
                    if (!isPet) player.MyPhysics.RpcBootFromVent(ventId);
                    player.Notify(GetString("AlchemistNoPotion"));
                    break;
            }

            am.SendRPCData();

            am.PotionID = 10;
        }

        void SendRPC()
        {
            if (!IsEnable || !Utils.DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetAlchemistTimer, SendOption.Reliable);
            writer.Write(AlchemistId);
            writer.Write(InvisTime.ToString());
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            byte id = reader.ReadByte();
            if (Main.PlayerStates[id].Role is not Alchemist am) return;
            am.InvisTime = long.Parse(reader.ReadString());
        }

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            if (UsePets.GetBool()) return;
            AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
            AURoleOptions.EngineerInVentMaxTime = 1f;
        }

        public override void OnCoEnterVent(PlayerPhysics instance, int ventId)
        {
            if (PotionID != 6) return;
            PotionID = 10;
            var pc = instance.myPlayer;
            if (!AmongUsClient.Instance.AmHost) return;
            LateTask.New(() =>
            {
                ventedId = ventId;

                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(instance.NetId, 34, SendOption.Reliable, pc.GetClientId());
                writer.WritePacked(ventId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);

                InvisTime = Utils.TimeStamp;
                SendRPC();
                pc.Notify(GetString("ChameleonInvisState"), InvisDuration.GetFloat());
            }, 0.5f, "Alchemist Invis");
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (!GameStates.IsInTask || !IsEnable) return;

            var now = Utils.TimeStamp;

            if (lastFixedTime != now && InvisTime != -10)
            {
                lastFixedTime = now;
                bool refresh = false;
                var remainTime = InvisTime + (long)InvisDuration.GetFloat() - now;
                switch (remainTime)
                {
                    case < 0:
                        player.MyPhysics?.RpcBootFromVent(ventedId == -10 ? Main.LastEnteredVent.TryGetValue(player.PlayerId, out Vent vent) ? vent.Id : player.PlayerId : ventedId);
                        player.Notify(GetString("SwooperInvisStateOut"));
                        SendRPC();
                        refresh = true;
                        InvisTime = -10;
                        break;
                    case <= 10 when !player.IsModClient():
                        player.Notify(string.Format(GetString("SwooperInvisStateCountdown"), remainTime + 1), overrideAll: true);
                        break;
                }

                if (refresh) SendRPC();
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
        {
            if (!hud || seer == null || seer.PlayerId != target.PlayerId || !GameStates.IsInTask || seer.PlayerId != AlchemistId) return string.Empty;
            var str = new StringBuilder();
            if (IsInvis)
            {
                var remainTime = InvisTime + (long)InvisDuration.GetFloat() - Utils.TimeStamp;
                str.Append(string.Format(GetString("ChameleonInvisStateCountdown"), remainTime + 1));
            }
            else
            {
                var preText = $"<color=#00ffa5>{GetString("PotionInStore")}:</color>";
                switch (PotionID)
                {
                    case 1: // Shield
                        str.Append($"{preText} <b><color=#00ff97>{GetString("ShieldPotion")}</color></b>");
                        break;
                    case 2: // Suicide
                        str.Append($"{preText} <b><color=#ff0000>{GetString("AwkwardPotion")}</color></b>");
                        break;
                    case 3: // TP to random player
                        str.Append($"{preText} <b><color=#42d1ff>{GetString("TeleportPotion")}</color></b>");
                        break;
                    case 4: // Increased speed
                        str.Append($"{preText} <b><color=#ff8400>{GetString("SpeedPotion")}</color></b>");
                        break;
                    case 5: // Quick fix next sabo
                        str.Append($"{preText} <b><color=#3333ff>{GetString("QuickFixPotion")}</color></b>");
                        break;
                    case 6: // Invisibility
                        str.Append($"{preText} <b><color=#01c834>{GetString("InvisibilityPotion")}</color></b>");
                        break;
                    case 7: // Increased vision
                        str.Append($"{preText} <b><color=#eee5be>{GetString("SightPotion")}</color></b>");
                        break;
                    case 10:
                        str.Append($"{preText} <color=#888888>{GetString("None")}</color>");
                        break;
                }

                if (FixNextSabo) str.Append($"\n<b><color=#3333ff>{GetString("QuickFixPotionWaitForUse")}</color></b>");
            }

            return str.ToString();
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            if (Utils.GetPlayerById(playerId) == null || !GameStates.IsInTask || Utils.GetPlayerById(playerId).IsModClient()) return string.Empty;
            var str = new StringBuilder();
            switch (PotionID)
            {
                case 1: // Shield
                    str.Append($" <color=#00ffa5>{GetString("Stored")}:</color> <color=#00ff97>{GetString("ShieldPotion")}</color>");
                    break;
                case 2: // Suicide
                    str.Append($" <color=#00ffa5>{GetString("Stored")}:</color> <color=#ff0000>{GetString("AwkwardPotion")}</color>");
                    break;
                case 3: // TP to random player
                    str.Append($" <color=#00ffa5>{GetString("Stored")}:</color> <color=#42d1ff>{GetString("TeleportPotion")}</color>");
                    break;
                case 4: // Increased speed
                    str.Append($" <color=#00ffa5>{GetString("Stored")}:</color> <color=#ff8400>{GetString("SpeedPotion")}</color>");
                    break;
                case 5: // Quick fix next sabo
                    str.Append($" <color=#00ffa5>{GetString("Stored")}:</color> <color=#3333ff>{GetString("QuickFixPotion")}</color>");
                    break;
                case 6: // Invisibility
                    str.Append($" <color=#00ffa5>{GetString("Stored")}:</color> <color=#01c834>{GetString("InvisibilityPotion")}</color>");
                    break;
                case 7: // Increased vision
                    str.Append($" <color=#00ffa5>{GetString("Stored")}:</color> <color=#eee5be>{GetString("SightPotion")}</color>");
                    break;
            }

            if (FixNextSabo) str.Append($" <color=#777777>({GetString("QuickFix")})</color>");
            return str.ToString();
        }

        public static void RepairSystem(PlayerControl pc, SystemTypes systemType, byte amount)
        {
            if (Main.PlayerStates[pc.PlayerId].Role is not Alchemist { IsEnable: true } am) return;

            am.FixNextSabo = false;

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

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            return !IsProtected;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            if (UsePets.GetBool()) hud.PetButton?.OverrideText(GetString("AlchemistVentButtonText"));
            else hud.AbilityButton?.OverrideText(GetString("AlchemistVentButtonText"));
        }
    }
}