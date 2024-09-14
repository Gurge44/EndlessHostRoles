﻿using System;
using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using HarmonyLib;
using UnityEngine;

namespace EHR.Crewmate
{
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

        private readonly HashSet<byte> LastNotified = [];

        private HashSet<byte> DeadBodiesInRoom;

        private Dictionary<byte, long> LastInfoSend = [];
        public PlainShipRoom MonitoredRoom;

        private PlayerControl SentryPC;

        private HashSet<byte> UsingDevice = [];
        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            int id = 11370;
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
            AbilityUseLimit = new IntegerOptionItem(++id, "AbilityUseLimit", new(0, 20, 1), 0, TabGroup.CrewmateRoles)
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
            playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
            UsingDevice = [];
        }

        public override void Init()
        {
            On = false;
            AvailableDevices = DisableDevice.DevicePos.Where(x =>
            {
                var correctMap = x.Key.Contains(Main.CurrentMap.ToString(), StringComparison.OrdinalIgnoreCase);
                var devicesOpt = (UsableDevicesStrings)UsableDevicesForInfoView.GetValue();
                var enabled = devicesOpt switch
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
            var room = pc.GetPlainShipRoom();
            bool hasntChosenRoom = MonitoredRoom == null || MonitoredRoom == default || MonitoredRoom == default(PlainShipRoom);

            if (room == default(PlainShipRoom) && hasntChosenRoom)
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
            else DisplayRoomInfo(pc, false);
        }

        void DisplayRoomInfo(PlayerControl pc, bool fromDevice)
        {
            if (CannotSeeInfoDuringComms.GetBool() && Utils.IsActive(SystemTypes.Comms)) return;

            if (!fromDevice)
            {
                if (pc.GetAbilityUseLimit() < 1) return;
                pc.RpcRemoveAbilityUse();
            }

            var roomName = Translator.GetString(MonitoredRoom.RoomId.ToString());
            var players = Main.AllAlivePlayerControls.Where(IsInMonitoredRoom).Select(x => Utils.GetPlayerById(x.shapeshiftTargetPlayerId) ?? x).Select(x => Utils.ColorString(Main.PlayerColors[x.PlayerId], x.GetRealName())).Join();
            var bodies = GetColoredNames(DeadBodiesInRoom);

            var noDataString = Translator.GetString("Sentry.Notify.Info.NoData");
            if (players.Length == 0) players = noDataString;
            if (bodies.Length == 0) bodies = noDataString;

            pc.Notify(string.Format(Translator.GetString("Sentry.Notify.Info"), roomName, players, bodies), ShowInfoDuration.GetInt());
            return;

            static string GetColoredNames(IEnumerable<byte> ids) => ids.Where(x => Utils.GetPlayerById(x) != null).Select(x => Utils.ColorString(Main.PlayerColors[x], Utils.GetPlayerById(x).GetRealName())).Join();
        }

        bool IsInMonitoredRoom(PlayerControl pc) => MonitoredRoom != null && SentryPC.IsAlive() && pc.GetPlainShipRoom() == MonitoredRoom;

        public void OnAnyoneShapeshiftLoop(PlayerControl shapeshifter, PlayerControl target)
        {
            if (IsInMonitoredRoom(shapeshifter) && NameNotifyManager.Notifies.TryGetValue(SentryPC.PlayerId, out var notifies) && notifies.Count > 0)
            {
                bool shapeshifting = shapeshifter.PlayerId != target.PlayerId;
                var ssTarget = shapeshifting ? target : shapeshifter;
                var ss = shapeshifting ? shapeshifter : Utils.GetPlayerById(shapeshifter.shapeshiftTargetPlayerId);
                var text = "\n" + string.Format(
                    Translator.GetString("Sentry.Notify.Shapeshifted"),
                    Utils.ColorString(Main.PlayerColors[ss.PlayerId], ss.GetRealName()),
                    Utils.ColorString(Main.PlayerColors[ssTarget.PlayerId], Main.AllPlayerNames[ssTarget.PlayerId]));

                SentryPC.Notify($"{string.Join('\n', notifies.Keys)}{text}", ShowInfoDuration.GetInt() - (Utils.TimeStamp - notifies.First().Value));

                LateTask.New(() =>
                {
                    if (NameNotifyManager.Notifies.TryGetValue(SentryPC.PlayerId, out var laterNotifies) && laterNotifies.Count > 0)
                    {
                        var newText = string.Join('\n', laterNotifies.Keys).Replace(text, string.Empty);
                        SentryPC.Notify(newText, ShowInfoDuration.GetInt() - (Utils.TimeStamp - laterNotifies.First().Value));
                    }
                }, 3f, log: false);
            }
        }

        public static void OnAnyoneMurder(PlayerControl target)
        {
            foreach (var state in Main.PlayerStates.Values)
            {
                if (state.Role is Sentry st && st.IsInMonitoredRoom(target))
                {
                    st.DeadBodiesInRoom.Add(target.PlayerId);
                }
            }
        }

        public static void OnAnyoneEnterVent(PlayerControl pc)
        {
            if (!AmongUsClient.Instance.AmHost) return;

            foreach (var state in Main.PlayerStates.Values)
            {
                if (state.Role is Sentry st && st.IsInMonitoredRoom(pc) && NameNotifyManager.Notifies.TryGetValue(st.SentryPC.PlayerId, out var notifies) && notifies.Count > 0)
                {
                    var text = "\n" + string.Format(
                        Translator.GetString("Sentry.Notify.Vented"),
                        Utils.ColorString(Main.PlayerColors[pc.PlayerId], pc.GetRealName()));

                    st.SentryPC.Notify($"{string.Join('\n', notifies.Keys)}{text}", ShowInfoDuration.GetInt() - (Utils.TimeStamp - notifies.First().Value));

                    LateTask.New(() =>
                    {
                        if (NameNotifyManager.Notifies.TryGetValue(st.SentryPC.PlayerId, out var laterNotifies) && laterNotifies.Count > 0)
                        {
                            var newText = string.Join('\n', laterNotifies.Keys).Replace(text, string.Empty);
                            st.SentryPC.Notify(newText, ShowInfoDuration.GetInt() - (Utils.TimeStamp - laterNotifies.First().Value));
                        }
                    }, 3f, log: false);
                }
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
        {
            if (seer.PlayerId != target.PlayerId) return string.Empty;

            if (Main.PlayerStates[seer.PlayerId].Role is Sentry s && s.MonitoredRoom != null)
                return string.Format(Translator.GetString("Sentry.Suffix.Self"), Translator.GetString($"{s.MonitoredRoom.RoomId}"));

            if (!PlayersKnowAboutCamera.GetBool()) return string.Empty;

            foreach (var state in Main.PlayerStates.Values)
            {
                if (state.Role is Sentry st && st.IsInMonitoredRoom(seer))
                {
                    return string.Format(Translator.GetString("Sentry.Suffix.MonitoredRoom"), CustomRoles.Sentry.ToColoredString());
                }
            }

            return string.Empty;
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
            if (MonitoredRoom == null || MonitoredRoom == default || MonitoredRoom == default(PlainShipRoom)) return;
            if (LastInfoSend.TryGetValue(pc.PlayerId, out var ts) && ts == Utils.TimeStamp) return;
            LastInfoSend[pc.PlayerId] = Utils.TimeStamp;

            if (!CheckTeam(pc)) return;

            var pos = pc.Pos();
            var range = DisableDevice.UsableDistance - 1f;
            if (!AvailableDevices.Any(x => Vector2.Distance(pos, x) <= range))
            {
                if (UsingDevice.Contains(pc.PlayerId))
                {
                    NameNotifyManager.Notifies.Remove(pc.PlayerId);
                    Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                    UsingDevice.Remove(pc.PlayerId);
                }

                return;
            }

            DisplayRoomInfo(pc, true);
            UsingDevice.Add(pc.PlayerId);
        }

        static bool CheckTeam(PlayerControl pc)
        {
            SimpleTeam team = pc.GetTeam() switch
            {
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

        enum UsableDevicesStrings
        {
            None,
            Cameras,
            Admin,
            CamerasAndAdmin
        }

        enum AdditionalDevicesStrings
        {
            None,
            DoorLog,
            Binoculars,
            DoorLogAndBinoculars
        }

        enum SimpleTeam
        {
            Crewmate,
            Impostor,
            NK,
            NNK
        }
    }
}