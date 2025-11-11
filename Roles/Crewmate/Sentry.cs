using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using HarmonyLib;

namespace EHR.Crewmate;

internal class Sentry : RoleBase
{
    public static bool On;

    public static OptionItem ShowInfoCooldown;
    private static OptionItem ShowInfoDuration;
    private static OptionItem PlayersKnowAboutCamera;
    private static OptionItem CannotSeeInfoDuringComms;
    private static OptionItem UsableDevicesForInfoView;
    private static OptionItem AdditionalDevicesForInfoView;
    private static OptionItem AbilityUseLimit;
    public static OptionItem AbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;
    private static readonly Dictionary<SimpleTeam, OptionItem> TeamsCanSeeInfo = [];

    private static Vector2[] AvailableDevices = [];

    private readonly Dictionary<byte, long> LastInfoSend = [];

    private readonly HashSet<byte> LastNotified = [];

    private HashSet<byte> DeadBodiesInRoom;
    public PlainShipRoom MonitoredRoom;

    private PlayerControl SentryPC;

    private HashSet<byte> UsingDevice = [];
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        var id = 11370;
        Options.SetupRoleOptions(id++, TabGroup.CrewmateRoles, CustomRoles.Sentry);

        ShowInfoCooldown = new IntegerOptionItem(++id, "AbilityCooldown", new(1, 60, 1), 15, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry])
            .SetValueFormat(OptionFormat.Seconds);

        ShowInfoDuration = new IntegerOptionItem(++id, "Sentry.ShowInfoDuration", new(1, 60, 1), 5, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry])
            .SetValueFormat(OptionFormat.Seconds);

        PlayersKnowAboutCamera = new BooleanOptionItem(++id, "Sentry.PlayersKnowAboutCamera", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);

        CannotSeeInfoDuringComms = new BooleanOptionItem(++id, "Sentry.CannotSeeInfoDuringComms", true, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);

        UsableDevicesForInfoView = new StringOptionItem(++id, "Sentry.UsableDevicesForInfoView", Enum.GetNames<UsableDevicesStrings>(), 0, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);

        AdditionalDevicesForInfoView = new StringOptionItem(++id, "Sentry.AdditionalDevicesForInfoView", Enum.GetNames<AdditionalDevicesStrings>(), 0, TabGroup.CrewmateRoles)
            .SetParent(UsableDevicesForInfoView);

        Enum.GetValues<SimpleTeam>().Do(x =>
        {
            TeamsCanSeeInfo[x] = new BooleanOptionItem(++id, "Sentry.TeamsCanSeeInfo." + x, true, TabGroup.CrewmateRoles)
                .SetParent(UsableDevicesForInfoView);
        });

        AbilityUseLimit = new FloatOptionItem(++id, "AbilityUseLimit", new(0, 20, 0.05f), 0, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);

        AbilityUseGainWithEachTaskCompleted = new FloatOptionItem(++id, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 1f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);

        AbilityChargesWhenFinishedTasks = new FloatOptionItem(++id, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);
    }

    public override void Add(byte playerId)
    {
        On = true;
        SentryPC = Utils.GetPlayerById(playerId);
        MonitoredRoom = null;
        DeadBodiesInRoom = [];
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
        UsingDevice = [];
    }

    public override void Init()
    {
        On = false;

        AvailableDevices = DisableDevice.DevicePos.Where(x =>
        {
            bool correctMap = x.Key.Contains(Main.CurrentMap.ToString(), StringComparison.OrdinalIgnoreCase);
            var devicesOpt = (UsableDevicesStrings)UsableDevicesForInfoView.GetValue();

            bool enabled = devicesOpt switch
            {
                UsableDevicesStrings.None => false,
                UsableDevicesStrings.Cameras => x.Key.Contains("Camera") && !x.Key.Contains("Fungle"),
                UsableDevicesStrings.Admin => x.Key.Contains("Admin"),
                UsableDevicesStrings.CamerasAndAdmin => x.Key.Contains("Camera") || x.Key.Contains("Admin"),
                _ => false
            };

            if (!enabled && devicesOpt != UsableDevicesStrings.None)
            {
                enabled |= (AdditionalDevicesStrings)AdditionalDevicesForInfoView.GetValue() switch
                {
                    AdditionalDevicesStrings.None => false,
                    AdditionalDevicesStrings.DoorLog => x.Key.Contains("DoorLog"),
                    AdditionalDevicesStrings.Binoculars => x.Key.Contains("Camera") && x.Key.Contains("Fungle"),
                    AdditionalDevicesStrings.DoorLogAndBinoculars => x.Key.Contains("DoorLog") || (x.Key.Contains("Camera") && x.Key.Contains("Fungle")),
                    _ => false
                };
            }

            return correctMap && enabled;
        }).Select(x => x.Value).ToArray();
    }

    public override void OnPet(PlayerControl pc)
    {
        PlainShipRoom room = pc.GetPlainShipRoom();
        bool hasntChosenRoom = MonitoredRoom == null || MonitoredRoom == null || MonitoredRoom == null;

        if (room == null && hasntChosenRoom)
        {
            pc.AddAbilityCD(3);
            pc.Notify(Translator.GetString("Sentry.Notify.InvalidRoom"));
            return;
        }

        if (hasntChosenRoom)
        {
            MonitoredRoom = room;
            Utils.SendRPC(CustomRPC.SyncSentry, pc.PlayerId);
        }
        else
            DisplayRoomInfo(pc, false);
    }

    private void DisplayRoomInfo(PlayerControl pc, bool fromDevice)
    {
        if (CannotSeeInfoDuringComms.GetBool() && Utils.IsActive(SystemTypes.Comms)) return;

        if (!fromDevice)
        {
            if (pc.GetAbilityUseLimit() < 1) return;
            pc.RpcRemoveAbilityUse();
        }

        string roomName = Translator.GetString(MonitoredRoom.RoomId.ToString());
        string players = Main.AllAlivePlayerControls.Where(IsInMonitoredRoom).Select(x => Utils.GetPlayerById(x.shapeshiftTargetPlayerId) ?? x).Select(x => Utils.ColorString(Main.PlayerColors[x.PlayerId], x.GetRealName())).Join();
        string bodies = GetColoredNames(DeadBodiesInRoom);

        string noDataString = Translator.GetString("Sentry.Notify.Info.NoData");
        if (players.Length == 0) players = noDataString;
        if (bodies.Length == 0) bodies = noDataString;

        pc.Notify(string.Format(Translator.GetString("Sentry.Notify.Info"), roomName, players, bodies), ShowInfoDuration.GetInt(), fromDevice);
        return;

        static string GetColoredNames(IEnumerable<byte> ids) => ids.Where(x => Utils.GetPlayerById(x) != null).Select(x => Utils.ColorString(Main.PlayerColors[x], Utils.GetPlayerById(x).GetRealName())).Join();
    }

    private bool IsInMonitoredRoom(PlayerControl pc)
    {
        return MonitoredRoom != null && SentryPC.IsAlive() && pc.IsInRoom(MonitoredRoom);
    }

    public void OnAnyoneShapeshiftLoop(PlayerControl shapeshifter, PlayerControl target)
    {
        if (IsInMonitoredRoom(shapeshifter) && NameNotifyManager.Notifies.TryGetValue(SentryPC.PlayerId, out Dictionary<string, long> notifies) && notifies.Count > 0)
        {
            bool shapeshifting = shapeshifter.PlayerId != target.PlayerId;
            PlayerControl ssTarget = shapeshifting ? target : shapeshifter;
            PlayerControl ss = shapeshifting ? shapeshifter : Utils.GetPlayerById(shapeshifter.shapeshiftTargetPlayerId);

            string text = "\n" + string.Format(
                Translator.GetString("Sentry.Notify.Shapeshifted"),
                Utils.ColorString(Main.PlayerColors[ss.PlayerId], ss.GetRealName()),
                Utils.ColorString(Main.PlayerColors[ssTarget.PlayerId], Main.AllPlayerNames[ssTarget.PlayerId]));

            SentryPC.Notify(text, 3f);
        }
    }

    public static void OnAnyoneMurder(PlayerControl target)
    {
        foreach (PlayerState state in Main.PlayerStates.Values)
        {
            if (state.Role is Sentry st && st.IsInMonitoredRoom(target))
                st.DeadBodiesInRoom.Add(target.PlayerId);
        }
    }

    public static void OnAnyoneEnterVent(PlayerControl pc)
    {
        if (!AmongUsClient.Instance.AmHost) return;

        foreach (PlayerState state in Main.PlayerStates.Values)
        {
            if (state.Role is Sentry st && st.IsInMonitoredRoom(pc) && NameNotifyManager.Notifies.TryGetValue(st.SentryPC.PlayerId, out Dictionary<string, long> notifies) && notifies.Count > 0)
            {
                string text = "\n" + string.Format(
                    Translator.GetString("Sentry.Notify.Vented"),
                    Utils.ColorString(Main.PlayerColors[pc.PlayerId], pc.GetRealName()));

                st.SentryPC.Notify(text, 3f);
            }
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId) return string.Empty;

        if (seer.PlayerId == SentryPC.PlayerId && MonitoredRoom != null)
            return string.Format(Translator.GetString("Sentry.Suffix.Self"), Translator.GetString($"{MonitoredRoom.RoomId}"));

        if (!PlayersKnowAboutCamera.GetBool()) return string.Empty;

        return IsInMonitoredRoom(seer) ? string.Format(Translator.GetString("Sentry.Suffix.MonitoredRoom"), CustomRoles.Sentry.ToColoredString()) : string.Empty;
    }

    public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
    {
        if (!GameStates.IsInTask || lowLoad) return;

        bool nowInMonitoredRoom = IsInMonitoredRoom(pc);
        bool wasInMonitoredRoom = LastNotified.Contains(pc.PlayerId);

        switch (nowInMonitoredRoom)
        {
            case true when !wasInMonitoredRoom:
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                LastNotified.Add(pc.PlayerId);
                break;
            case false when wasInMonitoredRoom:
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                LastNotified.Remove(pc.PlayerId);
                break;
        }
    }

    public override void OnCheckPlayerPosition(PlayerControl pc)
    {
        if (MonitoredRoom == null || MonitoredRoom == null || MonitoredRoom == null) return;

        if (LastInfoSend.TryGetValue(pc.PlayerId, out long ts) && ts == Utils.TimeStamp) return;

        LastInfoSend[pc.PlayerId] = Utils.TimeStamp;

        if (!CheckTeam(pc)) return;

        Vector2 pos = pc.Pos();
        float range = DisableDevice.UsableDistance - 1f;

        if (!AvailableDevices.Any(x => Vector2.Distance(pos, x) <= range))
        {
            if (UsingDevice.Remove(pc.PlayerId))
            {
                NameNotifyManager.Notifies.Remove(pc.PlayerId);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }

            return;
        }

        DisplayRoomInfo(pc, true);
        UsingDevice.Add(pc.PlayerId);
    }

    private static bool CheckTeam(PlayerControl pc)
    {
        SimpleTeam team = pc.GetTeam() switch
        {
            Team.Coven => SimpleTeam.Coven,
            Team.Crewmate => SimpleTeam.Crewmate,
            Team.Impostor => SimpleTeam.Impostor,
            Team.Neutral => pc.IsNeutralKiller() ? SimpleTeam.NK : SimpleTeam.NNK,
            _ => SimpleTeam.Crewmate
        };

        return TeamsCanSeeInfo[team].GetBool();
    }

    public override void AfterMeetingTasks()
    {
        DeadBodiesInRoom.Clear();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.PetButton?.OverrideText(Translator.GetString("SentryPetButtonText"));
    }

    private enum UsableDevicesStrings
    {
        None,
        Cameras,
        Admin,
        CamerasAndAdmin
    }

    private enum AdditionalDevicesStrings
    {
        None,
        DoorLog,
        Binoculars,
        DoorLogAndBinoculars
    }

    private enum SimpleTeam
    {
        Crewmate,
        Impostor,
        NK,
        NNK,
        Coven
    }
}