using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;

namespace EHR.Roles;

internal class Bloodmoon : IGhostRole
{
    private static OptionItem CD;
    private static OptionItem Duration;
    private static OptionItem Speed;
    private static OptionItem DieOnMeetingCall;
    private static OptionItem CDIncreasePerUse;

    private static readonly Dictionary<byte, (long TimeStamp, byte KillerId)> ScheduledDeaths = [];

    private byte BloodmoonID;
    
    public Team Team => Team.Impostor | Team.Neutral;
    public RoleTypes RoleTypes => RoleTypes.GuardianAngel;
    public int Cooldown => CD.GetInt() + (Main.PlayerStates.Values.Count(x => x.GetRealKiller() == BloodmoonID && x.deathReason == PlayerState.DeathReason.LossOfBlood) + ScheduledDeaths.Values.Count(v => v.KillerId == BloodmoonID)) * CDIncreasePerUse.GetInt();

    public void OnAssign(PlayerControl pc)
    {
        Main.AllPlayerSpeed[pc.PlayerId] = Speed.GetFloat();
        pc.MarkDirtySettings();
        BloodmoonID = pc.PlayerId;
    }

    public void OnProtect(PlayerControl pc, PlayerControl target)
    {
        if (target.Is(CustomRoles.Pestilence) || (target.Is(Team.Impostor) && pc.Is(Team.Impostor)) || !pc.RpcCheckAndMurder(target, true)) return;
        ScheduledDeaths.TryAdd(target.PlayerId, (Utils.TimeStamp, pc.PlayerId));
        Main.AllPlayerSpeed[pc.PlayerId] = Speed.GetFloat();
        pc.MarkDirtySettings(); // failsafe check
        pc.AddAbilityCD(Cooldown + Duration.GetInt());
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

        Speed = new FloatOptionItem(649404, "Bloodmoon.Speed", new(0.05f, 3f, 0.05f), 0.25f, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodmoon])
            .SetValueFormat(OptionFormat.Multiplier);

        DieOnMeetingCall = new BooleanOptionItem(649405, "Bloodmoon.DieOnMeetingCall", true, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodmoon]);
        
        CDIncreasePerUse = new IntegerOptionItem(649406, "Bloodmoon.CDCIncreasePerUse", new(0, 600, 1), 15, TabGroup.OtherRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Bloodmoon])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public static void Update(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || ScheduledDeaths.Count == 0) return;
        if (!PerSecondUpdateScheduler.ShouldRunUpdate(pc.PlayerId)) return;

        long now = Utils.TimeStamp;
        List<byte> toRemove = null;

        foreach ((byte id, (long markTS, byte killerId)) in ScheduledDeaths)
        {
            PlayerControl player = Utils.GetPlayerById(id);
            
            if (!player || !player.IsAlive())
            {
                toRemove ??= [];
                toRemove.Add(id);
                continue;
            }

            if (now - markTS < Duration.GetInt())
            {
                Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                continue;
            }

            if (pc.RpcCheckAndMurder(player, true))
            {
                player.Suicide(PlayerState.DeathReason.LossOfBlood, killerId.GetPlayer());
                toRemove ??= [];
                toRemove.Add(id);
            }
        }
        
        toRemove?.ForEach(x => ScheduledDeaths.Remove(x));
    }

    public static void OnMeetingStart()
    {
        if (DieOnMeetingCall.GetBool())
        {
            foreach (var kvp in ScheduledDeaths)
            {
                PlayerControl pc = Utils.GetPlayerById(kvp.Key);
                if (!pc || !pc.IsAlive()) continue;

                pc.Suicide(PlayerState.DeathReason.LossOfBlood, Utils.GetPlayerById(kvp.Value.KillerId));
            }
        }

        ScheduledDeaths.Clear();
    }  

    public static string GetSuffix(PlayerControl seer)
    {
        if (!ScheduledDeaths.TryGetValue(seer.PlayerId, out var deathInfo)) return string.Empty;

        long timeLeft = Duration.GetInt() - (Utils.TimeStamp - deathInfo.TimeStamp) + 1;

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
