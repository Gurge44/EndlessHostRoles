using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;

namespace EHR.AddOns.GhostRoles;

internal class Bloodmoon : IGhostRole
{
    private static OptionItem CD;
    private static OptionItem Duration;
    private static OptionItem Speed;
    private static OptionItem DieOnMeetingCall;
    private static OptionItem CDIncreasePerUse;

    private static readonly Dictionary<byte, long> ScheduledDeaths = [];

    private long LastUpdate;
    private byte BloodmoonID;
    
    public Team Team => Team.Impostor | Team.Neutral;
    public RoleTypes RoleTypes => RoleTypes.GuardianAngel;
    public int Cooldown => CD.GetInt() + Main.PlayerStates.Values.Count(x => x.GetRealKiller() == BloodmoonID && x.deathReason == PlayerState.DeathReason.LossOfBlood) * CDIncreasePerUse.GetInt();

    public void OnAssign(PlayerControl pc)
    {
        Main.AllPlayerSpeed[pc.PlayerId] = Speed.GetFloat();
        pc.MarkDirtySettings();
        LastUpdate = Utils.TimeStamp;
        BloodmoonID = pc.PlayerId;
    }

    public void OnProtect(PlayerControl pc, PlayerControl target)
    {
        if (target.Is(CustomRoles.Pestilence) || !pc.RpcCheckAndMurder(target, true)) return;
        ScheduledDeaths.TryAdd(target.PlayerId, Utils.TimeStamp);
    }

    public void SetupCustomOption()
    {
        Options.SetupRoleOptions(649400, TabGroup.OtherRoles, CustomRoles.Bloodmoon);

        CD = new IntegerOptionItem(649402, "AbilityCooldown", new(0, 120, 1), 60, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodmoon])
            .SetValueFormat(OptionFormat.Seconds);

        Duration = new IntegerOptionItem(649403, "Bloodmoon.Duration", new(0, 60, 1), 15, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodmoon])
            .SetValueFormat(OptionFormat.Seconds);

        Speed = new FloatOptionItem(649404, "Bloodmoon.Speed", new(0.05f, 3f, 0.05f), 0.5f, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodmoon])
            .SetValueFormat(OptionFormat.Multiplier);

        DieOnMeetingCall = new BooleanOptionItem(649405, "Bloodmoon.DieOnMeetingCall", true, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodmoon]);
        
        CDIncreasePerUse = new IntegerOptionItem(649406, "Bloodmoon.CDCIncreasePerUse", new(0, 60, 1), 15, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodmoon])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public static void Update(PlayerControl pc, Bloodmoon instance)
    {
        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;

        long now = Utils.TimeStamp;
        if (now == instance.LastUpdate) return;
        instance.LastUpdate = now;

        foreach (KeyValuePair<byte, long> death in ScheduledDeaths)
        {
            PlayerControl player = Utils.GetPlayerById(death.Key);
            if (player == null || !player.IsAlive()) continue;

            if (now - death.Value < Duration.GetInt())
            {
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                continue;
            }

            if (pc.RpcCheckAndMurder(player, true)) player.Suicide(PlayerState.DeathReason.LossOfBlood, pc);
        }

        Utils.GetPlayersInRadius(7f, pc.Pos()).DoIf(x => !x.Is(Team.Impostor), x => x.Notify(string.Format(Translator.GetString("BloodmoonNearYou"), CustomRoles.Bloodmoon.ToColoredString()), sendOption: SendOption.None));
    }

    public static void OnMeetingStart()
    {
        if (DieOnMeetingCall.GetBool())
        {
            foreach (byte id in ScheduledDeaths.Keys)
            {
                PlayerControl pc = Utils.GetPlayerById(id);
                if (pc == null || !pc.IsAlive()) continue;

                pc.Suicide();
            }
        }

        ScheduledDeaths.Clear();
    }

    public static string GetSuffix(PlayerControl seer)
    {
        if (!ScheduledDeaths.TryGetValue(seer.PlayerId, out long ts)) return string.Empty;

        long timeLeft = Duration.GetInt() - (Utils.TimeStamp - ts) + 1;
        (string TextColor, string TimeColor) colors = GetColors();
        return string.Format(Translator.GetString("Bloodmoon.Suffix"), timeLeft, colors.TextColor, colors.TimeColor);

        (string TextColor, string TimeColor) GetColors() =>
            timeLeft switch
            {
                > 5 => ("#ffff00", "#ffa500"),
                _ => ("#ff0000", "#ffff00")
            };
    }
}