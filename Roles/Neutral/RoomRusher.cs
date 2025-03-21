using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Neutral;

public class RoomRusher : RoleBase
{
    public static bool On;

    private static HashSet<SystemTypes> AllRooms = [];
    private static RandomSpawn.SpawnMap Map;

    private static OptionItem GlobalTimeMultiplier;
    private static OptionItem MaxVents;
    private static OptionItem RoomNameDisplay;
    private static OptionItem Arrow;
    private static OptionItem RoomsToWin;

    private int CompletedNum;
    private long LastUpdate;
    private SystemTypes RoomGoal;

    private byte RoomRusherId;
    private int TimeLeft;
    private int VentsLeft;

    public static bool CanVent => MaxVents.GetInt() > 0;

    public override bool IsEnable => On;

    public bool Won => CompletedNum >= RoomsToWin.GetInt();

    public override void SetupCustomOption()
    {
        StartSetup(645100)
            .AutoSetupOption(ref GlobalTimeMultiplier, 1f, new FloatValueRule(0.05f, 2f, 0.05f), OptionFormat.Multiplier, overrideName: "RR_GlobalTimeMultiplier")
            .AutoSetupOption(ref MaxVents, 1, new IntegerValueRule(0, 30, 1))
            .AutoSetupOption(ref RoomNameDisplay, true, overrideName: "RR_DisplayRoomName")
            .AutoSetupOption(ref Arrow, false, overrideName: "RR_DisplayArrowToRoom")
            .AutoSetupOption(ref RoomsToWin, 20, new IntegerValueRule(1, 300, 1));
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        RoomRusherId = playerId;
        VentsLeft = MaxVents.GetInt();
        CompletedNum = 0;
        LastUpdate = Utils.TimeStamp;
        TimeLeft = 50;

        LateTask.New(() =>
        {
            AllRooms = ShipStatus.Instance.AllRooms.Select(x => x.RoomId).ToHashSet();
            AllRooms.Remove(SystemTypes.Hallway);
            AllRooms.Remove(SystemTypes.Outside);
            AllRooms.RemoveWhere(x => x.ToString().Contains("Decontamination"));

            Map = RandomSpawn.SpawnMap.GetSpawnMap();

            StartNewRound(true);
        }, Main.CurrentMap == MapNames.Airship ? 22f : 14f);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(true);
        AURoleOptions.EngineerCooldown = 1f;
        AURoleOptions.EngineerInVentMaxTime = 300f;
    }

    void StartNewRound(bool initial = false, bool dontCount = false, bool afterMeeting = false)
    {
        MapNames map = Main.CurrentMap;

        SystemTypes previous = !initial
            ? RoomGoal
            : map switch
            {
                MapNames.Skeld => SystemTypes.Cafeteria,
                MapNames.Mira => afterMeeting ? SystemTypes.Cafeteria : SystemTypes.Launchpad,
                MapNames.Dleks => SystemTypes.Cafeteria,
                MapNames.Polus => afterMeeting ? SystemTypes.Office : SystemTypes.Dropship,
                MapNames.Airship => SystemTypes.MainHall,
                MapNames.Fungle => SystemTypes.Dropship,
                _ => throw new ArgumentOutOfRangeException(map.ToString(), "Invalid map")
            };

        if (!initial && !dontCount) CompletedNum++;
        PlayerControl rrpc = RoomRusherId.GetPlayer();
        RoomGoal = AllRooms.Without(previous).RandomElement();
        Vector2 goalPos = Map.Positions.GetValueOrDefault(RoomGoal, RoomGoal.GetRoomClass().transform.position);
        Vector2 previousPos = Map.Positions.GetValueOrDefault(previous, initial ? rrpc.Pos() : previous.GetRoomClass().transform.position);
        float distance = initial || afterMeeting ? 50 : Vector2.Distance(goalPos, previousPos);
        float speed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
        var time = (int)Math.Ceiling(distance / speed);
        Dictionary<(SystemTypes, SystemTypes), int> multipliers = RoomRush.Multipliers[map == MapNames.Dleks ? MapNames.Skeld : map];
        time *= multipliers.GetValueOrDefault((previous, RoomGoal), multipliers.GetValueOrDefault((RoomGoal, previous), 1));

        bool involvesDecontamination = map switch
        {
            MapNames.Mira => (previous is SystemTypes.Laboratory or SystemTypes.Reactor) ^ (RoomGoal is SystemTypes.Laboratory or SystemTypes.Reactor),
            MapNames.Polus => previous == SystemTypes.Specimens || RoomGoal == SystemTypes.Specimens,
            _ => false
        };

        if (involvesDecontamination)
        {
            bool polus = map == MapNames.Polus;

            int decontaminationTime = Options.ChangeDecontaminationTime.GetBool()
                ? polus
                    ? Options.DecontaminationTimeOnPolus.GetInt()
                    : Options.DecontaminationTimeOnMiraHQ.GetInt()
                : 3;

            time += decontaminationTime * (polus ? 2 : 4);
        }

        switch (map)
        {
            case MapNames.Airship when RoomGoal == SystemTypes.Ventilation:
                time = (int)(time * 0.7f);
                break;
            case MapNames.Fungle when RoomGoal == SystemTypes.Laboratory || previous == SystemTypes.Laboratory:
                time += (int)(8 / speed);
                break;
            case MapNames.Polus when (RoomGoal == SystemTypes.Laboratory && previous is not SystemTypes.Storage and not SystemTypes.Specimens) || (previous == SystemTypes.Laboratory && RoomGoal is not SystemTypes.Office and not SystemTypes.Storage and not SystemTypes.Electrical):
                time -= (int)(5 * speed);
                break;
        }

        TimeLeft = Math.Max((int)Math.Round(time * GlobalTimeMultiplier.GetFloat()), 5);
        Logger.Info($"Goal = from: {Translator.GetString(previous.ToString())} ({previous}), to: {Translator.GetString(RoomGoal.ToString())} ({RoomGoal}) - Time: {TimeLeft}  ({map})", "Room Rusher");
        LocateArrow.RemoveAllTarget(RoomRusherId);
        LocateArrow.Add(RoomRusherId, goalPos);

        Utils.NotifyRoles(SpecifySeer: rrpc, SpecifyTarget: rrpc);

        Utils.SendRPC(CustomRPC.SyncRoleData, RoomRusherId, 1, (byte)RoomGoal);
        Utils.SendRPC(CustomRPC.SyncRoleData, RoomRusherId, 2, VentsLeft);
        Utils.SendRPC(CustomRPC.SyncRoleData, RoomRusherId, 3, CompletedNum);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        VentsLeft--;
        Utils.SendRPC(CustomRPC.SyncRoleData, RoomRusherId, 2, VentsLeft);
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return pc.PlayerId != RoomRusherId || (CanVent && VentsLeft > 0);
    }

    public override void AfterMeetingTasks()
    {
        StartNewRound(dontCount: true, afterMeeting: true);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!Main.IntroDestroyed || !GameStates.IsInTask || ExileController.Instance || !pc.IsAlive()) return;

        PlainShipRoom room = pc.GetPlainShipRoom();

        if (!pc.inMovingPlat && !pc.inVent && room != null && room.RoomId == RoomGoal)
        {
            Logger.Info($"{pc.GetRealName()} entered the correct room", "Room Rusher");
            StartNewRound();
        }

        long now = Utils.TimeStamp;
        if (LastUpdate == now) return;
        LastUpdate = now;

        TimeLeft--;
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

        if (TimeLeft <= 0)
        {
            Logger.Info("Time is up", "Room Rusher");

            if (Won) StartNewRound(dontCount: true);
            else pc.Suicide();

            if (pc.IsLocalPlayer())
                Achievements.Type.OutOfTime.Complete();
        }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                RoomGoal = (SystemTypes)reader.ReadByte();
                break;
            case 2:
                VentsLeft = reader.ReadPackedInt32();
                break;
            case 3:
                CompletedNum = reader.ReadPackedInt32();
                break;
        }
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        var s1 = Utils.ColorString(Won ? Color.green : Color.white, $" {CompletedNum}");
        var s2 = Utils.ColorString(Won ? Color.white : Color.yellow, $"/{RoomsToWin.GetInt()}");
        return base.GetProgressText(playerId, comms) + s1 + s2;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != RoomRusherId || seer.PlayerId != target.PlayerId || meeting || hud || !seer.IsAlive()) return string.Empty;

        StringBuilder sb = new();
        bool done = Won;
        Color color = done ? Color.green : Color.yellow;

        if (RoomNameDisplay.GetBool()) sb.Append(Utils.ColorString(color, Translator.GetString(RoomGoal.ToString())) + "\n");
        if (Arrow.GetBool()) sb.Append(Utils.ColorString(color, LocateArrow.GetArrows(seer)) + "\n");

        color = done ? Color.white : Color.yellow;
        sb.Append(Utils.ColorString(color, TimeLeft.ToString()) + "\n");

        if (!CanVent || seer.IsModdedClient()) return sb.ToString().Trim();

        sb.Append('\n');
        sb.Append(string.Format(Translator.GetString("RR_VentsRemaining"), VentsLeft));

        return sb.ToString().Trim();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (hud.AbilityButton == null || !CanVent) return;
        hud.AbilityButton.SetUsesRemaining(VentsLeft);
    }
}