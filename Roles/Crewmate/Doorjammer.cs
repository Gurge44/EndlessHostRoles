using System.Collections.Generic;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

public class Doorjammer : RoleBase
{
    public static bool On;

    public static OptionItem AbilityCooldown;
    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityUseGainWithEachTaskCompleted;
    private static OptionItem AbilityChargesWhenFinishedTasks;
    private static OptionItem MaxRoomsJammedAtOnce;
    public static OptionItem BlockSabotagesFromJammedRooms;

    public override bool IsEnable => On;

    private byte DoorjammerId;

    public static List<SystemTypes> JammedRooms = [];

    public override void SetupCustomOption()
    {
        StartSetup(656100)
            .AutoSetupOption(ref AbilityCooldown, 30, new IntegerValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachTaskCompleted, 0.3f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityChargesWhenFinishedTasks, 0.2f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref MaxRoomsJammedAtOnce, 5, new IntegerValueRule(1, 30, 1))
            .AutoSetupOption(ref BlockSabotagesFromJammedRooms, true);
    }

    public override void Init()
    {
        On = false;
        JammedRooms = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        DoorjammerId = playerId;
    }

    public override void OnPet(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1) return;

        var plainShipRoom = pc.GetPlainShipRoom();
        if (plainShipRoom == null) return;

        var room = plainShipRoom.RoomId;

        if (JammedRooms.Contains(room))
        {
            LateTask.New(pc.RemoveAbilityCD, 0.2f, log: false);
            return;
        }

        if (JammedRooms.Count >= MaxRoomsJammedAtOnce.GetInt())
        {
            JammedRooms.RemoveAt(0);
            Utils.SendRPC(CustomRPC.SyncRoleData, DoorjammerId, 2);
        }

        pc.RpcRemoveAbilityUse();
        JammedRooms.Add(room);
        Utils.SendRPC(CustomRPC.SyncRoleData, DoorjammerId, 1, (byte)room);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                JammedRooms.Add((SystemTypes)reader.ReadByte());
                break;
            case 2:
                JammedRooms.RemoveAt(0);
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != DoorjammerId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting) return string.Empty;
        string join = string.Join(", ", JammedRooms.ConvertAll(x => Translator.GetString(x.ToString())));
        return string.Format(Translator.GetString("Doorjammer.Suffix"), JammedRooms.Count, MaxRoomsJammedAtOnce.GetInt(), string.IsNullOrWhiteSpace(join) ? string.Empty : $"\n<size=80%>{join}</size>");
    }
}