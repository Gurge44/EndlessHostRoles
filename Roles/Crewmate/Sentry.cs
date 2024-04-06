using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace EHR.Roles.Impostor
{
    internal class Sentry : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static OptionItem ShowInfoCooldown;
        private static OptionItem ShowInfoDuration;
        private static OptionItem PlayersKnowAboutCamera;
        private static OptionItem AbilityUseLimit;
        public static OptionItem AbilityUseGainWithEachTaskCompleted;
        public static OptionItem AbilityChargesWhenFinishedTasks;

        private PlainShipRoom MonitoredRoom;

        private HashSet<byte> DeadBodiesInRoom;
        private HashSet<byte> PlayersVentedInRoom;
        private HashSet<byte> PlayersShiftedInRoom;

        public static void SetupCustomOption()
        {
            const int id = 11370;
            Options.SetupRoleOptions(id, TabGroup.CrewmateRoles, CustomRoles.Sentry);
            ShowInfoCooldown = IntegerOptionItem.Create(id + 2, "Sentry.ShowInfoCooldown", new(1, 60, 1), 15, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry])
                .SetValueFormat(OptionFormat.Seconds);
            ShowInfoDuration = IntegerOptionItem.Create(id + 3, "Sentry.ShowInfoDuration", new(1, 60, 1), 5, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry])
                .SetValueFormat(OptionFormat.Seconds);
            PlayersKnowAboutCamera = BooleanOptionItem.Create(id + 4, "Sentry.PlayersKnowAboutCamera", true, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);
            AbilityUseLimit = IntegerOptionItem.Create(id + 5, "AbilityUseLimit", new(0, 20, 1), 5, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);
            AbilityUseGainWithEachTaskCompleted = FloatOptionItem.Create(id + 6, "AbilityUseGainWithEachTaskCompleted", new(0.1f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);
            AbilityChargesWhenFinishedTasks = FloatOptionItem.Create(id + 7, "AbilityChargesWhenFinishedTasks", new(0.1f, 5f, 0.1f), 1f, TabGroup.CrewmateRoles, false)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Sentry]);
        }

        public override void Add(byte playerId)
        {
            On = true;
            MonitoredRoom = null;
            DeadBodiesInRoom = [];
            PlayersVentedInRoom = [];
            PlayersShiftedInRoom = [];
            playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
        }

        public override void Init()
        {
            On = false;
        }

        public override void OnPet(PlayerControl pc)
        {
            if (MonitoredRoom == null) MonitoredRoom = pc.GetPlainShipRoom();
            else DisplayRoomInfo(pc);
        }

        void DisplayRoomInfo(PlayerControl pc)
        {
            if (pc.GetAbilityUseLimit() < 1) return;
            pc.RpcRemoveAbilityUse();

            var players = Main.AllAlivePlayerControls.Where(IsInMonitoredRoom).Select(x => Utils.ColorString(Main.PlayerColors[x.PlayerId], x.GetRealName())).Join();
            var bodies = GetColoredNames(DeadBodiesInRoom);
            var vented = GetColoredNames(PlayersVentedInRoom);
            var shifted = GetColoredNames(PlayersShiftedInRoom);

            pc.Notify(string.Format(Translator.GetString("Sentry.Notify.Info"), players, bodies, vented, shifted), ShowInfoDuration.GetInt());
            return;

            static string GetColoredNames(IEnumerable<byte> ids) => ids.Where(x => Utils.GetPlayerById(x) != null).Select(x => Utils.ColorString(Main.PlayerColors[x], Utils.GetPlayerById(x).GetRealName())).Join();
        }

        bool IsInMonitoredRoom(PlayerControl pc) => pc.GetPlainShipRoom().name == MonitoredRoom.name;

        public void OnAnyoneShapeshiftLoop(PlayerControl shapeshifter)
        {
            if (IsInMonitoredRoom(shapeshifter)) PlayersShiftedInRoom.Add(shapeshifter.PlayerId);
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
                if (state.Role is Sentry st && st.IsInMonitoredRoom(pc))
                {
                    st.PlayersVentedInRoom.Add(pc.PlayerId);
                }
            }
        }

        public static string GetSuffix(PlayerControl seer)
        {
            return !PlayersKnowAboutCamera.GetBool() ? string.Empty : string.Format(Translator.GetString("Sentry.Suffix.MonitoredRoom"), CustomRoles.Sentry.ToColoredString());
        }
    }
}
