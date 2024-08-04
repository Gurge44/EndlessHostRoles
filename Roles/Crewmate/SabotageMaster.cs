using System;
using System.Collections.Generic;
using System.Globalization;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

public class SabotageMaster : RoleBase
{
    private const int Id = 7000;
    public static List<byte> playerIdList = [];

    public static OptionItem SkillLimit;
    public static OptionItem FixesDoors;
    public static OptionItem FixesReactors;
    public static OptionItem FixesOxygens;
    public static OptionItem FixesComms;
    public static OptionItem FixesElectrical;
    public static OptionItem SMAbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;
    public static OptionItem UsesUsedWhenFixingReactorOrO2;
    public static OptionItem UsesUsedWhenFixingLightsOrComms;

    private static bool DoorsProgressing;
    private byte SMId;

    public float UsedSkillCount;

    public override bool IsEnable => playerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        Options.SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.SabotageMaster);
        SkillLimit = new IntegerOptionItem(Id + 10, "SabotageMasterSkillLimit", new(0, 80, 1), 2, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster])
            .SetValueFormat(OptionFormat.Times);
        FixesDoors = new BooleanOptionItem(Id + 11, "SabotageMasterFixesDoors", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster]);
        FixesReactors = new BooleanOptionItem(Id + 12, "SabotageMasterFixesReactors", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster]);
        FixesOxygens = new BooleanOptionItem(Id + 13, "SabotageMasterFixesOxygens", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster]);
        FixesComms = new BooleanOptionItem(Id + 14, "SabotageMasterFixesCommunications", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster]);
        FixesElectrical = new BooleanOptionItem(Id + 15, "SabotageMasterFixesElectrical", true, TabGroup.CrewmateRoles).SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster]);
        SMAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(Id + 16, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 3f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster])
            .SetValueFormat(OptionFormat.Times);
        AbilityChargesWhenFinishedTasks = new FloatOptionItem(Id + 19, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster])
            .SetValueFormat(OptionFormat.Times);
        UsesUsedWhenFixingReactorOrO2 = new FloatOptionItem(Id + 17, "SMUsesUsedWhenFixingReactorOrO2", new(0f, 5f, 0.1f), 4f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster])
            .SetValueFormat(OptionFormat.Times);
        UsesUsedWhenFixingLightsOrComms = new FloatOptionItem(Id + 18, "SMUsesUsedWhenFixingLightsOrComms", new(0f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.SabotageMaster])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        playerIdList = [];
        UsedSkillCount = 0;
        SMId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        UsedSkillCount = 0;
        SMId = playerId;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        var limit = Math.Round(SkillLimit.GetInt() - UsedSkillCount, 1);
        var colored = Utils.ColorString(Utils.GetRoleColor(CustomRoles.SabotageMaster), limit.ToString(CultureInfo.CurrentCulture));
        return $"({colored}){base.GetProgressText(playerId, comms)}";
    }

    public void SendRPC()
    {
        if (!IsEnable || !Utils.DoRPC) return;
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetSabotageMasterLimit, SendOption.Reliable);
        writer.Write(SMId);
        writer.Write(UsedSkillCount);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte id = reader.ReadByte();
        if (Main.PlayerStates[id].Role is not SabotageMaster sm) return;
        sm.UsedSkillCount = reader.ReadSingle();
    }

    public static void RepairSystem(byte playerId, ShipStatus __instance, SystemTypes systemType, byte amount)
    {
        if (Main.PlayerStates[playerId].Role is not SabotageMaster sm) return;

        switch (systemType)
        {
            case SystemTypes.Reactor:
                if (!FixesReactors.GetBool()) break;
                if (SkillLimit.GetFloat() > 0 && sm.UsedSkillCount + UsesUsedWhenFixingReactorOrO2.GetFloat() - 1 >= SkillLimit.GetFloat()) break;
                if (amount is 64 or 65)
                {
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Reactor, 16);
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Reactor, 17);
                    sm.UsedSkillCount += UsesUsedWhenFixingReactorOrO2.GetFloat();
                    sm.SendRPC();
                }

                break;
            case SystemTypes.Laboratory:
                if (!FixesReactors.GetBool()) break;
                if (SkillLimit.GetFloat() > 0 && sm.UsedSkillCount + UsesUsedWhenFixingReactorOrO2.GetFloat() - 1 >= SkillLimit.GetFloat()) break;
                if (amount is 64 or 65)
                {
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Laboratory, 67);
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Laboratory, 66);
                    sm.UsedSkillCount += UsesUsedWhenFixingReactorOrO2.GetFloat();
                    sm.SendRPC();
                }

                break;
            case SystemTypes.LifeSupp:
                if (!FixesOxygens.GetBool()) break;
                if (SkillLimit.GetFloat() > 0 && sm.UsedSkillCount + UsesUsedWhenFixingReactorOrO2.GetFloat() - 1 >= SkillLimit.GetFloat()) break;
                if (amount is 64 or 65)
                {
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.LifeSupp, 67);
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.LifeSupp, 66);
                    sm.UsedSkillCount += UsesUsedWhenFixingReactorOrO2.GetFloat();
                    sm.SendRPC();
                }

                break;
            case SystemTypes.Comms:
                if (!FixesComms.GetBool()) break;
                if (SkillLimit.GetFloat() > 0 && sm.UsedSkillCount + UsesUsedWhenFixingLightsOrComms.GetFloat() - 1 >= SkillLimit.GetFloat()) break;
                if (amount is 64 or 65)
                {
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 16);
                    ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 17);
                    sm.UsedSkillCount += UsesUsedWhenFixingLightsOrComms.GetFloat();
                    sm.SendRPC();
                }

                break;
            case SystemTypes.Doors:
                if (!FixesDoors.GetBool()) break;
                if (DoorsProgressing) break;

                int mapId = Main.NormalOptions.MapId;
                if (AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay) mapId = AmongUsClient.Instance.TutorialMapId;

                DoorsProgressing = true;
                switch (mapId)
                {
                    case 2:
                        //Polus
                        RepairSystemPatch.CheckAndOpenDoorsRange(__instance, amount, 71, 72);
                        RepairSystemPatch.CheckAndOpenDoorsRange(__instance, amount, 67, 68);
                        RepairSystemPatch.CheckAndOpenDoorsRange(__instance, amount, 64, 66);
                        RepairSystemPatch.CheckAndOpenDoorsRange(__instance, amount, 73, 74);
                        break;
                    case 4:
                        //Airship
                        RepairSystemPatch.CheckAndOpenDoorsRange(__instance, amount, 64, 67);
                        RepairSystemPatch.CheckAndOpenDoorsRange(__instance, amount, 71, 73);
                        RepairSystemPatch.CheckAndOpenDoorsRange(__instance, amount, 74, 75);
                        RepairSystemPatch.CheckAndOpenDoorsRange(__instance, amount, 76, 78);
                        RepairSystemPatch.CheckAndOpenDoorsRange(__instance, amount, 68, 70);
                        RepairSystemPatch.CheckAndOpenDoorsRange(__instance, amount, 83, 84);
                        break;
                }

                DoorsProgressing = false;
                break;
        }
    }

    public static void SwitchSystemRepair(byte playerId, SwitchSystem __instance, byte amount)
    {
        if (!FixesElectrical.GetBool() || Main.PlayerStates[playerId].Role is not SabotageMaster sm) return;
        if (SkillLimit.GetFloat() > 0 &&
            sm.UsedSkillCount + UsesUsedWhenFixingLightsOrComms.GetFloat() - 1 >= SkillLimit.GetFloat())
            return;

        if (amount <= 4)
        {
            __instance.ActualSwitches = 0;
            __instance.ExpectedSwitches = 0;
            sm.UsedSkillCount += UsesUsedWhenFixingLightsOrComms.GetFloat();
            sm.SendRPC();
        }
    }
}