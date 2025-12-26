using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using Hazel;

namespace EHR.Crewmate;

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
    private bool IsInvis => InvisTime != -10;

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

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    private void SendRPCData()
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
        PotionID = (byte)IRandom.Instance.Next(1, 8);

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
        DrinkPotion(pc, true);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (OnCoEnterVent(pc.MyPhysics, vent.Id)) return;
        if (UsePets.GetBool()) return;
        DrinkPotion(pc);
    }

    public static void DrinkPotion(PlayerControl player, bool isPet = false)
    {
        if (Main.PlayerStates[player.PlayerId].Role is not Alchemist am) return;

        switch (am.PotionID)
        {
            case 1: // Shield
                am.IsProtected = true;
                player.RPCPlayCustomSound("Shield");
                player.Notify(GetString("AlchemistShielded"), ShieldDuration.GetInt());

                LateTask.New(() =>
                {
                    am.IsProtected = false;
                    player.Notify(GetString("AlchemistShieldOut"));
                }, ShieldDuration.GetInt() + 1, "Alchemist Shield");

                break;
            case 2: // Suicide
                LateTask.New(() => player.Suicide(PlayerState.DeathReason.Poison), !isPet ? 2f : 0.1f, "Alchemist Suicide");
                break;
            case 3: // TP to random player
                LateTask.New(() =>
                {
                    player.TP(Main.AllAlivePlayerControls.Without(player).Where(x => !Pelican.IsEaten(x.PlayerId) && !x.inVent && !x.onLadder).ToList().RandomElement());
                    player.RPCPlayCustomSound("Teleport");
                }, !isPet ? 2f : 0.1f, "AlchemistTPToRandomPlayer");
                break;
            case 4: // Increased speed
                player.Notify(GetString("AlchemistHasSpeed"));
                player.MarkDirtySettings();
                float tmpSpeed = Main.AllPlayerSpeed[player.PlayerId];
                Main.AllPlayerSpeed[player.PlayerId] = Speed.GetFloat();

                LateTask.New(() =>
                {
                    Main.AllPlayerSpeed[player.PlayerId] = tmpSpeed;
                    player.MarkDirtySettings();
                    player.Notify(GetString("AlchemistSpeedOut"));
                }, SpeedDuration.GetInt() + 1, "Alchemist Speed");

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
                }, VisionDuration.GetFloat() + 1f, "Alchemist Vision");

                break;
            case 10 when !player.Is(CustomRoles.Nimble):
                player.Notify(GetString("AlchemistNoPotion"));
                break;
        }

        am.SendRPCData();

        am.PotionID = 10;
    }

    private void SendRPC()
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
        AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    bool OnCoEnterVent(PlayerPhysics instance, int ventId)
    {
        if (PotionID != 6) return false;

        PotionID = 10;
        PlayerControl pc = instance.myPlayer;
        if (!AmongUsClient.Instance.AmHost) return false;

        ventedId = ventId;

        instance.RpcExitVentDesync(ventId, pc);

        InvisTime = Utils.TimeStamp;
        SendRPC();
        pc.Notify(GetString("ChameleonInvisState"), InvisDuration.GetFloat());

        return true;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || !IsEnable) return;

        long now = Utils.TimeStamp;

        if (lastFixedTime != now && InvisTime != -10)
        {
            lastFixedTime = now;
            var refresh = false;
            long remainTime = InvisTime + (long)InvisDuration.GetFloat() - now;

            switch (remainTime)
            {
                case < 0:
                    int ventId = ventedId == -10 ? Main.LastEnteredVent[player.PlayerId].Id : ventedId;
                    Main.AllPlayerControls.Without(player).Do(x => player.MyPhysics.RpcExitVentDesync(ventId, x));
                    player.Notify(GetString("SwooperInvisStateOut"));
                    SendRPC();
                    refresh = true;
                    InvisTime = -10;
                    break;
                case <= 10 when !player.IsModdedClient():
                    player.Notify(string.Format(GetString("SwooperInvisStateCountdown"), remainTime + 1), overrideAll: true);
                    break;
            }

            if (refresh) SendRPC();
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (!hud || seer.PlayerId != target.PlayerId || !GameStates.IsInTask || seer.PlayerId != AlchemistId) return string.Empty;

        var sb = new StringBuilder();

        if (IsInvis)
        {
            long remainTime = InvisTime + (long)InvisDuration.GetFloat() - Utils.TimeStamp;
            sb.Append(string.Format(GetString("ChameleonInvisStateCountdown"), remainTime + 1));
        }
        else
        {
            sb.Append($" <{HeaderColour}>{GetString("PotionInStore")}:</color> ");

            if (PotionStyles.TryGetValue(PotionID, out PotionStyle style))
                sb.Append($"<b><{style.Colour}>{GetString(style.NameKey)}</color></b>");
            else
                sb.Append($"<#888888>{GetString("None")}</color>");

            if (FixNextSabo) sb.Append($"\n<b><color=#3333ff>{GetString("QuickFixPotionWaitForUse")}</color></b>");
        }

        return sb.ToString();
    }

    private readonly record struct PotionStyle(string Colour, string NameKey);

    private static readonly Dictionary<int, PotionStyle> PotionStyles = new()
    {
        { 1, new PotionStyle("#00ff97", "ShieldPotion") },
        { 2, new PotionStyle("#ff0000", "AwkwardPotion") },
        { 3, new PotionStyle("#42d1ff", "TeleportPotion") },
        { 4, new PotionStyle("#ff8400", "SpeedPotion") },
        { 5, new PotionStyle("#3333ff", "QuickFixPotion") },
        { 6, new PotionStyle("#01c834", "InvisibilityPotion") },
        { 7, new PotionStyle("#eee5be", "SightPotion") }
    };

    private const string HeaderColour = "#00ffa5";

    public override string GetProgressText(byte playerId, bool comms)
    {
        if (Utils.GetPlayerById(playerId) == null || !GameStates.IsInTask || playerId.IsPlayerModdedClient()) return base.GetProgressText(playerId, comms);

        var sb = new StringBuilder(base.GetProgressText(playerId, comms));

        if (PotionStyles.TryGetValue(PotionID, out PotionStyle style))
        {
            sb.Append(
                $" <{HeaderColour}>{GetString("Stored")}:</color>" +
                $" <{style.Colour}>{GetString(style.NameKey)}</color>");
        }

        if (FixNextSabo) sb.Append($" <#777777>({GetString("QuickFix")})</color>");

        return sb.ToString();
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
        if (UsePets.GetBool())
            hud.PetButton?.OverrideText(GetString("AlchemistVentButtonText"));
        else
            hud.AbilityButton?.OverrideText(GetString("AlchemistVentButtonText"));
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }
}