using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using UnityEngine;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Impostor;

public class Deathpact : RoleBase
{
    private const int Id = 1100;
    private static List<Deathpact> Instances = [];

    private static List<byte> ActiveDeathpacts = [];

    private static OptionItem KillCooldown;
    private static OptionItem ShapeshiftCooldown;
    private static OptionItem DeathpactDuration;
    private static OptionItem NumberOfPlayersInPact;
    private static OptionItem ShowArrowsToOtherPlayersInPact;
    private static OptionItem ReduceVisionWhileInPact;
    private static OptionItem VisionWhileInPact;
    private static OptionItem KillDeathpactPlayersOnMeeting;

    private byte DeathPactId;
    private long DeathpactTime;
    private List<PlayerControl> PlayersInDeathpact = [];

    public override bool IsEnable => Instances.Count > 0 || Randomizer.Exists;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Deathpact);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 25f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Deathpact])
            .SetValueFormat(OptionFormat.Seconds);

        ShapeshiftCooldown = new FloatOptionItem(Id + 11, "DeathPactCooldown", new(0f, 180f, 0.5f), 5f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Deathpact])
            .SetValueFormat(OptionFormat.Seconds);

        DeathpactDuration = new FloatOptionItem(Id + 13, "DeathpactDuration", new(0f, 180f, 0.5f), 17.5f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Deathpact])
            .SetValueFormat(OptionFormat.Seconds);

        NumberOfPlayersInPact = new IntegerOptionItem(Id + 14, "DeathpactNumberOfPlayersInPact", new(2, 5, 1), 2, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Deathpact])
            .SetValueFormat(OptionFormat.Times);

        ShowArrowsToOtherPlayersInPact = new BooleanOptionItem(Id + 15, "DeathpactShowArrowsToOtherPlayersInPact", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Deathpact]);

        ReduceVisionWhileInPact = new BooleanOptionItem(Id + 16, "DeathpactReduceVisionWhileInPact", false, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Deathpact]);

        VisionWhileInPact = new FloatOptionItem(Id + 17, "DeathpactVisionWhileInPact", new(0f, 5f, 0.05f), 0.4f, TabGroup.ImpostorRoles)
            .SetParent(ReduceVisionWhileInPact)
            .SetValueFormat(OptionFormat.Multiplier);

        KillDeathpactPlayersOnMeeting = new BooleanOptionItem(Id + 18, "DeathpactKillPlayersInDeathpactOnMeeting", false, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Deathpact]);
    }

    public override void Init()
    {
        Instances = [];
        PlayersInDeathpact = [];
        DeathpactTime = 0;
        ActiveDeathpacts = [];
        DeathPactId = byte.MaxValue;
    }

    public override void Add(byte playerId)
    {
        Instances.Add(this);
        PlayersInDeathpact = [];
        DeathpactTime = 0;
        DeathPactId = playerId;
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool OnShapeshift(PlayerControl pc, PlayerControl target, bool shapeshifting)
    {
        if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId) || !shapeshifting) return false;

        if (!target.IsAlive() || Pelican.IsEaten(target.PlayerId))
        {
            pc.Notify(GetString("DeathpactCouldNotAddTarget"));
            return false;
        }

        if (PlayersInDeathpact.All(b => b.PlayerId != target.PlayerId)) PlayersInDeathpact.Add(target);

        if (PlayersInDeathpact.Count < NumberOfPlayersInPact.GetInt()) return false;

        if (ReduceVisionWhileInPact.GetBool())
        {
            foreach (PlayerControl player in PlayersInDeathpact)
            {
                foreach (PlayerControl otherPlayerInPact in PlayersInDeathpact.Where(a => a.PlayerId != player.PlayerId))
                {
                    otherPlayerInPact.MarkDirtySettings();
                    player.MarkDirtySettings();
                }
            }
        }

        pc.Notify(GetString("DeathpactComplete"));
        DeathpactTime = TimeStamp + DeathpactDuration.GetInt();
        ActiveDeathpacts.Add(pc.PlayerId);

        if (ShowArrowsToOtherPlayersInPact.GetBool())
        {
            foreach (PlayerControl player in PlayersInDeathpact)
            {
                foreach (PlayerControl otherPlayerInPact in PlayersInDeathpact.Where(a => a.PlayerId != player.PlayerId))
                {
                    TargetArrow.Add(player.PlayerId, otherPlayerInPact.PlayerId);
                    otherPlayerInPact.MarkDirtySettings();
                }
            }
        }

        return false;
    }

    public static void SetDeathpactVision(PlayerControl player, IGameOptions opt)
    {
        if (!ReduceVisionWhileInPact.GetBool()) return;

        if (Instances.Exists(x => x.PlayersInDeathpact.Exists(b => b.PlayerId == player.PlayerId) && x.PlayersInDeathpact.Count == NumberOfPlayersInPact.GetInt()))
        {
            opt.SetVision(false);
            opt.SetFloat(FloatOptionNames.CrewLightMod, VisionWhileInPact.GetFloat());
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, VisionWhileInPact.GetFloat());
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!IsEnable || !GameStates.IsInTask || player.GetCustomRole() is not CustomRoles.Deathpact and not CustomRoles.Randomizer) return;

        if (!ActiveDeathpacts.Contains(player.PlayerId)) return;

        if (CheckCancelDeathpact(player)) return;

        if (DeathpactTime < TimeStamp && DeathpactTime != 0)
        {
            foreach (PlayerControl playerInDeathpact in PlayersInDeathpact) KillPlayerInDeathpact(player, playerInDeathpact);

            ClearDeathpact(player.PlayerId);
            player.Notify(GetString("DeathpactExecuted"));
        }
    }

    public static bool CheckCancelDeathpact(PlayerControl deathpact)
    {
        if (Main.PlayerStates[deathpact.PlayerId].Role is not Deathpact { IsEnable: true } dp) return true;

        if (dp.PlayersInDeathpact.Any(a => a.Data.Disconnected || !a.IsAlive()))
        {
            ClearDeathpact(deathpact.PlayerId);
            deathpact.Notify(GetString("DeathpactAverted"));
            return true;
        }

        var cancelDeathpact = true;

        foreach (PlayerControl player in dp.PlayersInDeathpact)
        {
            float range = NormalGameOptionsV10.KillDistances[Mathf.Clamp(player.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f;
            cancelDeathpact = dp.PlayersInDeathpact.Where(a => a.PlayerId != player.PlayerId).Select(otherPlayerInPact => Vector2.Distance(player.Pos(), otherPlayerInPact.Pos())).Aggregate(cancelDeathpact, (current, dis) => current && dis <= range);
        }

        if (cancelDeathpact)
        {
            ClearDeathpact(deathpact.PlayerId);
            deathpact.Notify(GetString("DeathpactAverted"));
        }

        return cancelDeathpact;
    }

    public static void KillPlayerInDeathpact(PlayerControl deathpact, PlayerControl target)
    {
        if (deathpact == null || target == null || target.Data.Disconnected) return;

        if (!target.IsAlive()) return;

        target.Suicide(realKiller: deathpact);

        if (target.AmOwner)
            Achievements.Type.OutOfTime.Complete();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (meeting || hud || !ShowArrowsToOtherPlayersInPact.GetBool() || target != null && seer.PlayerId != target.PlayerId || !IsInActiveDeathpact(seer)) return string.Empty;

        var arrows = string.Empty;

        foreach (KeyValuePair<byte, PlayerState> state in Main.PlayerStates)
        {
            if (state.Value.Role is Deathpact { IsEnable: true } dp)
                arrows = dp.PlayersInDeathpact.Where(a => a.PlayerId != seer.PlayerId).Select(otherPlayerInPact => TargetArrow.GetArrows(seer, otherPlayerInPact.PlayerId)).Aggregate(arrows, (current, arrow) => current + ColorString(GetRoleColor(CustomRoles.Crewmate), arrow));
        }

        return arrows;
    }

    public static string GetDeathpactMark(PlayerControl seer, PlayerControl target)
    {
        if (!seer.Is(CustomRoles.Deathpact) || !IsInDeathpact(seer.PlayerId, target)) return string.Empty;

        return ColorString(Palette.ImpostorRed, "◀");
    }

    public static bool IsInActiveDeathpact(PlayerControl player)
    {
        if (ActiveDeathpacts.Count == 0) return false;

        foreach (KeyValuePair<byte, PlayerState> state in Main.PlayerStates)
        {
            if (state.Value.Role is Deathpact { IsEnable: true } dp)
            {
                if (!ActiveDeathpacts.Contains(dp.DeathPactId) || dp.PlayersInDeathpact.All(b => b.PlayerId != player.PlayerId)) continue;

                return true;
            }
        }

        return false;
    }

    public static bool IsInDeathpact(byte deathpact, PlayerControl target)
    {
        return Main.PlayerStates[deathpact].Role is Deathpact { IsEnable: true } dp && dp.PlayersInDeathpact.Any(a => a.PlayerId == target.PlayerId);
    }

    public static string GetDeathpactString(PlayerControl player)
    {
        var result = string.Empty;

        foreach (KeyValuePair<byte, PlayerState> state in Main.PlayerStates)
        {
            if (state.Value.Role is Deathpact { IsEnable: true } dp)
            {
                if (!ActiveDeathpacts.Contains(dp.DeathPactId) || dp.PlayersInDeathpact.All(b => b.PlayerId != player.PlayerId)) continue;

                string otherPlayerNames = dp.PlayersInDeathpact.Where(a => a.PlayerId != player.PlayerId).Aggregate(string.Empty, (current, otherPlayerInPact) => current + otherPlayerInPact.name.ToUpper() + ",");
                otherPlayerNames = otherPlayerNames.Remove(otherPlayerNames.Length - 1);

                var countdown = (int)(dp.DeathpactTime - TimeStamp);

                result += $"{ColorString(GetRoleColor(CustomRoles.Impostor), string.Format(GetString("DeathpactActiveDeathpact"), otherPlayerNames, countdown))}";
            }
        }

        return result;
    }

    public static void ClearDeathpact(byte deathpact)
    {
        if (Main.PlayerStates[deathpact].Role is not Deathpact { IsEnable: true } dp) return;

        dp.DeathpactTime = 0;
        ActiveDeathpacts.Remove(deathpact);
        dp.PlayersInDeathpact.Clear();

        if (ReduceVisionWhileInPact.GetBool())
        {
            foreach (PlayerControl player in dp.PlayersInDeathpact)
            {
                foreach (PlayerControl otherPlayerInPact in dp.PlayersInDeathpact.Where(a => a.PlayerId != player.PlayerId))
                {
                    if (ShowArrowsToOtherPlayersInPact.GetBool()) TargetArrow.Remove(player.PlayerId, otherPlayerInPact.PlayerId);

                    otherPlayerInPact.MarkDirtySettings();
                    player.MarkDirtySettings();
                }
            }
        }
    }

    public override void OnReportDeadBody()
    {
        foreach (byte deathpact in ActiveDeathpacts)
        {
            if (KillDeathpactPlayersOnMeeting.GetBool())
            {
                PlayerControl deathpactPlayer = Main.AllPlayerControls.FirstOrDefault(a => a.PlayerId == deathpact);
                if (deathpactPlayer == null || !deathpactPlayer.IsAlive()) continue;

                foreach (PlayerControl player in PlayersInDeathpact) KillPlayerInDeathpact(deathpactPlayer, player);
            }

            ClearDeathpact(deathpact);
        }
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton?.OverrideText(GetString("DeathpactButtonText"));
    }
}