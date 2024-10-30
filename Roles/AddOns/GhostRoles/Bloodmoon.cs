using System.Collections.Generic;

namespace EHR.AddOns.GhostRoles
{
    internal class Bloodmoon : IGhostRole
    {
        private static OptionItem CD;
        private static OptionItem Duration;
        private static OptionItem Speed;
        private static OptionItem DieOnMeetingCall;

        private static readonly Dictionary<byte, long> ScheduledDeaths = [];

        private long LastUpdate;
        public Team Team => Team.Impostor | Team.Neutral;
        public int Cooldown => Duration.GetInt() + CD.GetInt();

        public void OnAssign(PlayerControl pc)
        {
            Main.AllPlayerSpeed[pc.PlayerId] = Speed.GetFloat();
            pc.MarkDirtySettings();
            LastUpdate = Utils.TimeStamp;
        }

        public void OnProtect(PlayerControl pc, PlayerControl target)
        {
            if (!pc.RpcCheckAndMurder(target, true))
            {
                return;
            }

            ScheduledDeaths.TryAdd(target.PlayerId, Utils.TimeStamp);
        }

        public void SetupCustomOption()
        {
            Options.SetupRoleOptions(649400, TabGroup.OtherRoles, CustomRoles.Bloodmoon);
            CD = new IntegerOptionItem(649402, "AbilityCooldown", new(0, 60, 1), 60, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodmoon])
                .SetValueFormat(OptionFormat.Seconds);
            Duration = new IntegerOptionItem(649403, "Bloodmoon.Duration", new(0, 60, 1), 15, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodmoon])
                .SetValueFormat(OptionFormat.Seconds);
            Speed = new FloatOptionItem(649404, "Bloodmoon.Speed", new(0.05f, 5f, 0.05f), 1f, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodmoon])
                .SetValueFormat(OptionFormat.Multiplier);
            DieOnMeetingCall = new BooleanOptionItem(649405, "Bloodmoon.DieOnMeetingCall", true, TabGroup.OtherRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodmoon]);
        }

        public static void Update(PlayerControl pc, Bloodmoon instance)
        {
            if (!GameStates.IsInTask || ExileController.Instance)
            {
                return;
            }

            long now = Utils.TimeStamp;
            if (now == instance.LastUpdate)
            {
                return;
            }

            instance.LastUpdate = now;

            foreach (KeyValuePair<byte, long> death in ScheduledDeaths)
            {
                PlayerControl player = Utils.GetPlayerById(death.Key);
                if (player == null || !player.IsAlive())
                {
                    continue;
                }

                if (now - death.Value < Duration.GetInt())
                {
                    Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                    continue;
                }

                if (pc.RpcCheckAndMurder(player, true))
                {
                    player.Suicide(realKiller: pc);
                }
            }
        }

        public static void OnMeetingStart()
        {
            if (DieOnMeetingCall.GetBool())
            {
                foreach (byte id in ScheduledDeaths.Keys)
                {
                    PlayerControl pc = Utils.GetPlayerById(id);
                    if (pc == null || !pc.IsAlive())
                    {
                        continue;
                    }

                    pc.Suicide();
                }
            }

            ScheduledDeaths.Clear();
        }

        public static string GetSuffix(PlayerControl seer)
        {
            if (!ScheduledDeaths.TryGetValue(seer.PlayerId, out long ts))
            {
                return string.Empty;
            }

            long timeLeft = Duration.GetInt() - (Utils.TimeStamp - ts) + 1;
            (string TextColor, string TimeColor) colors = GetColors();
            return string.Format(Translator.GetString("Bloodmoon.Suffix"), timeLeft, colors.TextColor, colors.TimeColor);

            (string TextColor, string TimeColor) GetColors()
            {
                return timeLeft switch
                {
                    > 5 => ("#ffff00", "#ffa500"),
                    _ => ("#ff0000", "#ffff00")
                };
            }
        }
    }
}