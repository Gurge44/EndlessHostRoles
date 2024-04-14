using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace EHR.Roles.Impostor
{
    internal class Sentry : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static OptionItem ShowInfoCooldown;
        private static OptionItem ShowInfoDuration;
        private static OptionItem PlayersKnowAboutCamera;
        private static OptionItem CannotSeeInfoDuringComms;
        private static OptionItem AbilityUseLimit;
        public static OptionItem AbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

        private PlayerControl SentryPC;
        private PlainShipRoom MonitoredRoom;

        private HashSet<byte> DeadBodiesInRoom;

        public static void SetupCustomOption()
        {
            const int id = 11370;
            Options.SetupRoleOptions(id, TabGroup.CrewmateRoles, CustomRoles.Sentry);
            ShowInfoCooldown = IntegerOptionItem.Create(id + 2, "AbilityCooldown", new(1, 60, 1), 15, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry])
                .SetValueFormat(OptionFormat.Seconds);
            ShowInfoDuration = IntegerOptionItem.Create(id + 3, "Sentry.ShowInfoDuration", new(1, 60, 1), 5, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry])
                .SetValueFormat(OptionFormat.Seconds);
            PlayersKnowAboutCamera = BooleanOptionItem.Create(id + 4, "Sentry.PlayersKnowAboutCamera", true, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);
            CannotSeeInfoDuringComms = BooleanOptionItem.Create(id + 5, "Sentry.CannotSeeInfoDuringComms", true, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);
            AbilityUseLimit = IntegerOptionItem.Create(id + 6, "AbilityUseLimit", new(0, 20, 1), 0, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);
            AbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(id + 7, "AbilityUseGainWithEachTaskCompleted", new(0.1f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(id + 8, "AbilityChargesWhenFinishedTasks", new(0.1f, 5f, 0.1f), 0.2f, TabGroup.CrewmateRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);
        }

        public override void Add(byte playerId)
        {
            On = true;
            SentryPC = Utils.GetPlayerById(playerId);
            MonitoredRoom = null;
            DeadBodiesInRoom = [];
            playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void OnPet(PlayerControl pc)
        {
            var room = pc.GetPlainShipRoom();
            bool hasntChosenRoom = MonitoredRoom == null || MonitoredRoom == default || MonitoredRoom == default(PlainShipRoom);

            if (room == default(PlainShipRoom) && hasntChosenRoom)
            {
                pc.AddAbilityCD(1);
                pc.Notify(Translator.GetString("Sentry.Notify.InvalidRoom"));
                return;
            }

            if (hasntChosenRoom) MonitoredRoom = room;
            else DisplayRoomInfo(pc);
        }

        void DisplayRoomInfo(PlayerControl pc)
        {
            if (CannotSeeInfoDuringComms.GetBool() && Utils.IsActive(SystemTypes.Comms)) return;

            if (pc.GetAbilityUseLimit() < 1) return;
            pc.RpcRemoveAbilityUse();

            var players = Main.AllAlivePlayerControls.Where(IsInMonitoredRoom).Select(x => Utils.GetPlayerById(x.shapeshiftTargetPlayerId) ?? x).Select(x => Utils.ColorString(Main.PlayerColors[x.PlayerId], x.GetRealName())).Join();
            var bodies = GetColoredNames(DeadBodiesInRoom);

            var noDataString = Translator.GetString("Sentry.Notify.Info.NoData");
            if (players.Length == 0) players = noDataString;
            if (bodies.Length == 0) bodies = noDataString;

            pc.Notify(string.Format(Translator.GetString("Sentry.Notify.Info"), players, bodies), ShowInfoDuration.GetInt());
            return;

            static string GetColoredNames(IEnumerable<byte> ids) => ids.Where(x => Utils.GetPlayerById(x) != null).Select(x => Utils.ColorString(Main.PlayerColors[x], Utils.GetPlayerById(x).GetRealName())).Join();
        }

        public bool IsInMonitoredRoom(PlayerControl pc) => MonitoredRoom != null && pc.GetPlainShipRoom() == MonitoredRoom;

        public void OnAnyoneShapeshiftLoop(PlayerControl shapeshifter, PlayerControl target)
        {
            if (IsInMonitoredRoom(shapeshifter) && NameNotifyManager.Notice.TryGetValue(SentryPC.PlayerId, out var notify))
            {
                var ssTarget = Utils.GetPlayerById(shapeshifter.shapeshiftTargetPlayerId);
                var text = "\n" + string.Format(
                    Translator.GetString("Sentry.Notify.Shapeshifted"),
                    ssTarget == null
                        ? Utils.ColorString(Main.PlayerColors[shapeshifter.PlayerId], shapeshifter.GetRealName())
                        : Utils.ColorString(Main.PlayerColors[ssTarget.PlayerId], ssTarget.GetRealName()),
                    Utils.ColorString(Main.PlayerColors[target.PlayerId], target.GetRealName()));

                SentryPC.Notify($"{notify.TEXT}{text}", ShowInfoDuration.GetInt() - (Utils.TimeStamp - notify.TIMESTAMP));

                _ = new LateTask(() =>
                {
                    if (NameNotifyManager.Notice.TryGetValue(SentryPC.PlayerId, out var laterNotify))
                    {
                        laterNotify.TEXT = laterNotify.TEXT.Replace(text, string.Empty);
                        SentryPC.Notify(laterNotify.TEXT, ShowInfoDuration.GetInt() - (Utils.TimeStamp - laterNotify.TIMESTAMP));
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
            foreach (var state in Main.PlayerStates.Values)
            {
                if (state.Role is Sentry st && st.IsInMonitoredRoom(pc) && NameNotifyManager.Notice.TryGetValue(st.SentryPC.PlayerId, out var notify))
                {
                    var text = "\n" + string.Format(
                        Translator.GetString("Sentry.Notify.Vented"),
                        Utils.ColorString(Main.PlayerColors[pc.PlayerId], pc.GetRealName()));

                    notify.TEXT += text;
                    Utils.NotifyRoles(SpecifySeer: st.SentryPC, SpecifyTarget: st.SentryPC);

                    _ = new LateTask(() =>
                    {
                        if (NameNotifyManager.Notice.TryGetValue(st.SentryPC.PlayerId, out var laterNotify))
                            laterNotify.TEXT = laterNotify.TEXT.Replace(text, string.Empty);
                    }, 3f, log: false);
                }
            }
        }

        public static string GetSuffix(PlayerControl seer)
        {
            if (Main.PlayerStates[seer.PlayerId].Role is Sentry s && s.MonitoredRoom != null) return string.Format(Translator.GetString("Sentry.Suffix.Self"), Translator.GetString($"{s.MonitoredRoom.RoomId}"));

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

        private readonly HashSet<byte> LastNotified = [];

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
    }
}
