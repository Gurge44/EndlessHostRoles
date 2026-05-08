using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Gamemodes;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Roles;

public class RoomRusher : RoleBase
{
    public static bool On;

    private static readonly StringBuilder Suffix = new();
    private static HashSet<SystemTypes> AllRooms = [];

    private static OptionItem GlobalTimeAddition;
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
            .AutoSetupOption(ref GlobalTimeAddition, 4, new IntegerValueRule(0, 15, 1), OptionFormat.Seconds, overrideName: "RR_GlobalTimeAddition")
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
        
        if (!AmongUsClient.Instance.AmHost) return;

        LateTask.New(() =>
        {
            AllRooms = ShipStatus.Instance.AllRooms.Select(x => x.RoomId).ToHashSet();
            AllRooms.Remove(SystemTypes.Hallway);
            AllRooms.Remove(SystemTypes.Outside);
            if (SubmergedCompatibility.IsSubmerged()) AllRooms.RemoveWhere(x => (byte)x > 135);

            StartNewRound(true);
        }, Main.CurrentMap == MapNames.Airship ? 22f : 14f);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(true);
        AURoleOptions.EngineerCooldown = 1f;
        AURoleOptions.EngineerInVentMaxTime = 300f;
    }

    private void StartNewRound(bool initial = false, bool dontCount = false, bool afterMeeting = false)
    {
        MapNames map = Main.CurrentMap;

        SystemTypes previous = !initial
            ? RoomGoal
            : map switch
            {
                MapNames.Skeld => SystemTypes.Cafeteria,
                MapNames.MiraHQ => afterMeeting ? SystemTypes.Cafeteria : SystemTypes.Launchpad,
                MapNames.Dleks => SystemTypes.Cafeteria,
                MapNames.Polus => afterMeeting ? SystemTypes.Office : SystemTypes.Dropship,
                MapNames.Airship => SystemTypes.MainHall,
                MapNames.Fungle => SystemTypes.Dropship,
                (MapNames)6 => (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.UpperCentral,
                _ => throw new ArgumentOutOfRangeException(map.ToString(), "Invalid map")
            };

        if (!initial && !dontCount) CompletedNum++;
        PlayerControl rrpc = RoomRusherId.GetPlayer();
        RoomGoal = AllRooms.Without(previous).RandomElement();
        Vector2 goalPos = RoomGoal.GetRoomClass().transform.position;
        float speed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
        var rawTimes = RoomRush.RawTimeNeeded.GetValueOrDefault(map, []);
        var time = initial ? rawTimes.Values.Max() : rawTimes.GetValueOrDefault((previous, RoomGoal), rawTimes.GetValueOrDefault((RoomGoal, previous), 25));
        time = (int)Math.Round(time / (speed / 1.25f));

        bool involvesDecontamination = map switch
        {
            MapNames.MiraHQ => previous is SystemTypes.Laboratory or SystemTypes.Reactor ^ RoomGoal is SystemTypes.Laboratory or SystemTypes.Reactor,
            MapNames.Polus => previous == SystemTypes.Specimens || RoomGoal == SystemTypes.Specimens,
            (MapNames)6 => (previous == (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Ballast) ^ (RoomGoal == (SystemTypes)SubmergedCompatibility.SubmergedSystemTypes.Ballast),
            _ => false
        };

        if (involvesDecontamination)
            time += 2;

        switch (map)
        {
            case MapNames.Airship:
                time += previous switch
                {
                    SystemTypes.Engine => 3,
                    SystemTypes.MainHall => 2,
                    _ => 0
                };
                break;
        }

        time += GlobalTimeAddition.GetInt();
        if (time < 6) time = 6;
        
        Logger.Info($"Goal = from: {Translator.GetString(previous.ToString())} ({previous}), to: {Translator.GetString(RoomGoal.ToString())} ({RoomGoal}) - Time: {TimeLeft}  ({map})", "Room Rusher");
        LocateArrow.RemoveAllTarget(RoomRusherId);
        LocateArrow.Add(RoomRusherId, goalPos);

        Utils.NotifyRoles(SpecifySeer: rrpc, SpecifyTarget: rrpc);

        Utils.SendRPC(CustomRPC.SyncRoleData, RoomRusherId, 1, (byte)RoomGoal);
        Utils.SendRPC(CustomRPC.SyncRoleData, RoomRusherId, 2, VentsLeft);
        Utils.SendRPC(CustomRPC.SyncRoleData, RoomRusherId, 3, CompletedNum);
        Utils.SendRPC(CustomRPC.SyncRoleData, RoomRusherId, 4, TimeLeft);
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        VentsLeft--;
        Utils.SendRPC(CustomRPC.SyncRoleData, RoomRusherId, 2, VentsLeft);
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.PlayerId != RoomRusherId || (CanVent && VentsLeft > 0);
    }

    public override void AfterMeetingTasks()
    {
        StartNewRound(dontCount: true, afterMeeting: true);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!Main.IntroDestroyed || !GameStates.IsInTask || ExileController.Instance || !pc.IsAlive()) return;

        if (!pc.inMovingPlat && !pc.inVent && pc.IsInRoom(RoomGoal))
        {
            Logger.Info($"{pc.GetRealName()} entered the correct room", "Room Rusher");
            StartNewRound();
        }

        long now = Utils.TimeStamp;
        if (LastUpdate == now) return;
        LastUpdate = now;

        TimeLeft--;
        Utils.SendRPC(CustomRPC.SyncRoleData, RoomRusherId, 4, TimeLeft);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

        if (TimeLeft <= 0)
        {
            Logger.Info("Time is up", "Room Rusher");

            if (Won) StartNewRound(dontCount: true);
            else pc.Suicide();

            if (pc.AmOwner)
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
            case 4:
                TimeLeft = reader.ReadPackedInt32();
                break;
        }
    }

    public override void GetProgressText(byte playerId, bool comms, StringBuilder resultText)
    {
        base.GetProgressText(playerId, comms, resultText);

        Color32 color1 = Won ? Color.green : Color.white;
        resultText.Append(' ')
            .Append(Utils.ColorPrefix(color1))
            .Append(CompletedNum)
            .Append("</color>");

        Color32 color2 = Won ? Color.white : Color.yellow;
        resultText.Append(Utils.ColorPrefix(color2))
            .Append('/')
            .Append(RoomsToWin.GetInt())
            .Append("</color>");
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != RoomRusherId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting || !seer.IsAlive()) return string.Empty;

        Suffix.Clear();
        bool done = Won;
        Color color = done ? Color.green : Color.yellow;

        if (RoomNameDisplay.GetBool()) Suffix.Append(Utils.ColorString(color, Translator.GetString(RoomGoal))).Append('\n');
        if (Arrow.GetBool()) Suffix.Append(Utils.ColorString(color, LocateArrow.GetArrows(seer))).Append('\n');

        color = done ? Color.white : Color.yellow;
        Suffix.Append(Utils.ColorString(color, TimeLeft.ToString())).Append('\n');

        if (!CanVent || seer.IsModdedClient()) return Suffix.ToString().Trim();

        Suffix.Append('\n');
        Suffix.AppendFormat(Translator.GetString("RR_VentsRemaining"), VentsLeft);

        return Suffix.ToString().Trim();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (hud.AbilityButton == null || !CanVent) return;
        hud.AbilityButton.SetUsesRemaining(VentsLeft);
    }
}
