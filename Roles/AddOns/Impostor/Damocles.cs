using System;
using System.Collections.Generic;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.AddOns.Impostor;

public class Damocles : IAddon
{
    private const int Id = 14670;

    private static OptionItem DamoclesExtraTimeAfterKill;
    private static OptionItem DamoclesExtraTimeAfterMeeting;
    private static OptionItem DamoclesStartingTime;

    private static int TimeAfterKill;
    private static int TimeAfterMeeting;
    private static int StartingTime;

    public static Dictionary<byte, int> Timer;

    public static Dictionary<byte, long> LastUpdate;
    public static Dictionary<byte, List<int>> PreviouslyEnteredVents;

    public static bool CountRepairSabotage;
    public AddonTypes Type => AddonTypes.ImpOnly;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(Id, CustomRoles.Damocles, canSetNum: true);

        DamoclesExtraTimeAfterKill = new IntegerOptionItem(Id + 6, "DamoclesExtraTimeAfterKill", new(0, 60, 1), 30, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Damocles])
            .SetValueFormat(OptionFormat.Seconds);

        DamoclesExtraTimeAfterMeeting = new IntegerOptionItem(Id + 4, "DamoclesExtraTimeAfterMeeting", new(0, 60, 1), 30, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Damocles])
            .SetValueFormat(OptionFormat.Seconds);

        DamoclesStartingTime = new IntegerOptionItem(Id + 5, "DamoclesStartingTime", new(0, 60, 1), 30, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Damocles])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public static void Initialize()
    {
        TimeAfterKill = DamoclesExtraTimeAfterKill.GetInt();
        TimeAfterMeeting = DamoclesExtraTimeAfterMeeting.GetInt();
        StartingTime = DamoclesStartingTime.GetInt();

        Timer = [];
        LastUpdate = [];
        PreviouslyEnteredVents = [];
        CountRepairSabotage = true;
    }

    public static void Update(PlayerControl pc)
    {
        byte id = pc.PlayerId;
        long now = Utils.TimeStamp;
        if ((LastUpdate.TryGetValue(id, out long ts) && ts >= now) || !GameStates.IsInTask || pc == null) return;

        if (!pc.IsAlive())
        {
            Main.PlayerStates[id].RemoveSubRole(CustomRoles.Damocles);
            return;
        }

        LastUpdate[id] = now;
        if (!Timer.ContainsKey(id)) Timer[id] = StartingTime + 8;

        Timer[id]--;

        if (Timer[id] < 0)
        {
            Timer[id] = 0;
            pc.Suicide();

            if (pc.AmOwner)
                Achievements.Type.OutOfTime.Complete();
        }

        if (pc.IsNonHostModdedClient()) SendRPC(id);

        if (!pc.IsModdedClient()) Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public static void SendRPC(byte playerId)
    {
        if (!Utils.DoRPC) return;

        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncDamoclesTimer, SendOption.Reliable);
        writer.Write(playerId);
        writer.Write(Timer[playerId]);
        writer.Write(LastUpdate[playerId].ToString());
        List<int> pev = PreviouslyEnteredVents.GetValueOrDefault(playerId, []);
        writer.Write(pev.Count);

        if (pev.Count > 0)
        {
            foreach (int vent in pev.ToArray())
                writer.Write(vent);
        }

        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        Timer[playerId] = reader.ReadInt32();
        LastUpdate[playerId] = long.Parse(reader.ReadString());
        int elements = reader.ReadInt32();

        if (elements > 0)
        {
            for (var i = 0; i < elements; i++)
                PreviouslyEnteredVents[playerId].Add(reader.ReadInt32());
        }
    }

    public static void OnMurder(byte id)
    {
        Timer[id] += TimeAfterKill;
    }

    public static void OnOtherImpostorMurder()
    {
        AdjustTime(10);
    }

    public static void OnEnterVent(byte id, int ventId)
    {
        if (!PreviouslyEnteredVents.ContainsKey(id)) PreviouslyEnteredVents[id] = [];

        if (PreviouslyEnteredVents[id].Contains(ventId)) return;

        PreviouslyEnteredVents[id].Add(ventId);
        Timer[id] += 10;
    }

    public static void AfterMeetingTasks()
    {
        PreviouslyEnteredVents.Clear();

        AdjustTime(TimeAfterMeeting);
        CountRepairSabotage = true;
    }

    public static void OnMeetingStart()
    {
        AdjustTime(9);
    }

    public static void OnCrewmateEjected()
    {
        AdjustTimeByPercent(1.3);
    }

    public static void OnRepairSabotage(byte id)
    {
        Timer[id] -= 15;
    }

    public static void OnImpostorDeath()
    {
        AdjustTime(-20);
    }

    public static void OnReport(byte id)
    {
        Timer[id] = (int)Math.Round(Timer[id] * 0.9);
    }

    public static void OnImpostorEjected()
    {
        AdjustTimeByPercent(0.8);
    }

    private static void AdjustTime(int change)
    {
        Timer.AdjustAllValues(x => x + change);
    }

    private static void AdjustTimeByPercent(double percent)
    {
        Timer.AdjustAllValues(x => (int)Math.Round(x * percent));
    }

    public static string GetProgressText(byte id)
    {
        return string.Format(GetString("DamoclesTimeLeft"), Timer.GetValueOrDefault(id, StartingTime));
    }
}