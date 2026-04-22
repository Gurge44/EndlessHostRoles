using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;

namespace EHR.Roles;

using static EHR.Translator;

public class Solarist : RoleBase
{
    public static bool On;

    private const float OrbitInterval = 0.2f;
    private const float BaseRadius = 1.15f;
    private const float RadiusStep = 0.55f;
    private const float AngleStep = 55f;
    private const float AngularSpeed = 210f;

    private static OptionItem AbilityCooldown;
    private static OptionItem OrbitDuration;
    private static OptionItem OrbitRange;
    private static OptionItem MaxOrbitingPlayers;
    private static OptionItem CanOrbitImpostors;
    private static List<byte> PlayerIdList = [];

    private readonly List<byte> OrbitingPlayers = [];
    private byte SolaristId;
    private float RemainingDuration;
    private float OrbitAngle;
    private float OrbitTimer;

    public override bool IsEnable => On;

    private bool HasOrbit => OrbitingPlayers.Count > 0 && RemainingDuration > 0f;

    public override void SetupCustomOption()
    {
        StartSetup(658390)
            .AutoSetupOption(ref AbilityCooldown, 25f, new FloatValueRule(5f, 60f, 0.5f), OptionFormat.Seconds, overrideName: "SolaristCooldown")
            .AutoSetupOption(ref OrbitDuration, 5f, new FloatValueRule(1f, 20f, 0.5f), OptionFormat.Seconds, overrideName: "SolaristDuration")
            .AutoSetupOption(ref OrbitRange, 3f, new FloatValueRule(1f, 10f, 0.5f), OptionFormat.Multiplier, overrideName: "SolaristRange")
            .AutoSetupOption(ref MaxOrbitingPlayers, 2, new IntegerValueRule(1, 5, 1), OptionFormat.Players, overrideName: "SolaristMaxTargets")
            .AutoSetupOption(ref CanOrbitImpostors, false, overrideName: "SolaristCanAffectImpostors");
    }

    public override void Init()
    {
        On = false;
        PlayerIdList = [];
        ClearLocalState();
    }

    public override void Add(byte playerId)
    {
        On = true;
        PlayerIdList.Add(playerId);
        SolaristId = playerId;
        ClearLocalState();
    }

    public override void Remove(byte playerId)
    {
        ClearOrbit(notifySolarist: false);
        PlayerIdList.Remove(playerId);
        On = PlayerIdList.Count > 0;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = 300f;
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton?.OverrideText(GetString("SolaristButtonText"));
    }

    public override void OnPet(PlayerControl pc)
    {
        TryStartOrbit(pc);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        TryStartOrbit(pc);
        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting)
        {
            if (HasOrbit)
            {
                shapeshifter?.Notify(GetString("Solarist.CannotUnshiftWhileOrbiting"));
                return false;
            }

            return true;
        }

        TryStartOrbit(shapeshifter, target);
        return HasOrbit;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!AmongUsClient.Instance.AmHost || pc == null || pc.PlayerId != SolaristId || !HasOrbit) return;

        if (!pc.IsAlive() || !GameStates.IsInTask || pc.onLadder || pc.inMovingPlat || pc.inVent)
        {
            ClearOrbit(notifySolarist: true);
            return;
        }

        RemainingDuration -= Time.fixedDeltaTime;
        if (RemainingDuration <= 0f)
        {
            ClearOrbit(notifySolarist: true);
            return;
        }

        OrbitTimer -= Time.fixedDeltaTime;
        if (OrbitTimer > 0f) return;

        List<PlayerControl> activeTargets = GetOrbitTargets();
        if (activeTargets.Count == 0)
        {
            ClearOrbit(notifySolarist: true);
            return;
        }

        OrbitTimer = OrbitInterval;
        OrbitAngle = (OrbitAngle + (AngularSpeed * OrbitInterval)) % 360f;

        Vector2 center = pc.Pos();

        for (int i = 0; i < activeTargets.Count; i++)
        {
            PlayerControl target = activeTargets[i];
            float angle = (OrbitAngle + (AngleStep * i)) * Mathf.Deg2Rad;
            float radius = BaseRadius + (RadiusStep * i);
            Vector2 destination = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            target.TP(destination, noCheckState: true, log: false);
        }
    }

    public override void OnReportDeadBody()
    {
        ClearOrbit(notifySolarist: false);
    }

    public override void AfterMeetingTasks()
    {
        ClearOrbit(notifySolarist: false);
    }

    private void TryStartOrbit(PlayerControl solarist, PlayerControl requestedTarget = null)
    {
        if (!AmongUsClient.Instance.AmHost || solarist == null || solarist.PlayerId != SolaristId || !solarist.IsAlive()) return;

        if (!TryGetTargets(solarist, requestedTarget, out List<PlayerControl> targets))
        {
            solarist.Notify(GetString("Solarist.NoTargets"));
            solarist.RpcResetAbilityCooldown();
            return;
        }

        OrbitingPlayers.Clear();
        OrbitingPlayers.AddRange(targets.Select(x => x.PlayerId));
        RemainingDuration = OrbitDuration.GetFloat();
        OrbitTimer = 0f;
        OrbitAngle = 0f;

        string targetNames = string.Join(", ", OrbitingPlayers.Select(x => x.ColoredPlayerName()));
        solarist.Notify(string.Format(GetString("Solarist.OrbitStarted"), targetNames));

        Logger.Info($"{solarist.GetNameWithRole().RemoveHtmlTags()} started an orbit with {string.Join(", ", targets.Select(x => x.GetNameWithRole().RemoveHtmlTags()))}", "Solarist");
    }

    private bool TryGetTargets(PlayerControl solarist, PlayerControl requestedTarget, out List<PlayerControl> targets)
    {
        targets = [];

        List<PlayerControl> validTargets = Main.AllAlivePlayerControls
            .Where(x => IsValidTarget(solarist, x))
            .OrderBy(x => Vector2.Distance(x.Pos(), solarist.Pos()))
            .ToList();

        if (requestedTarget != null)
        {
            if (!IsValidTarget(solarist, requestedTarget)) return false;

            targets.Add(requestedTarget);
            validTargets.RemoveAll(x => x.PlayerId == requestedTarget.PlayerId);
        }

        int targetLimit = MaxOrbitingPlayers.GetInt();
        if (targets.Count < targetLimit)
            targets.AddRange(validTargets.Take(targetLimit - targets.Count));

        return targets.Count > 0;
    }

    private bool IsValidTarget(PlayerControl solarist, PlayerControl target)
    {
        if (solarist == null || target == null || target.PlayerId == solarist.PlayerId || !target.IsAlive()) return false;
        if (Pelican.IsEaten(target.PlayerId) || target.onLadder || target.inMovingPlat || target.inVent) return false;
        if (!FastVector2.DistanceWithinRange(solarist.Pos(), target.Pos(), OrbitRange.GetFloat())) return false;
        if (!CanOrbitImpostors.GetBool() && target.GetCustomRole().IsImpostor()) return false;
        return !IsOrbitingForAnotherSolarist(target.PlayerId);
    }

    private bool IsOrbitingForAnotherSolarist(byte targetId)
    {
        foreach (byte playerId in PlayerIdList)
        {
            if (playerId == SolaristId || !Main.PlayerStates.TryGetValue(playerId, out PlayerState state) || state.Role is not Solarist solarist || !solarist.HasOrbit) continue;
            if (solarist.OrbitingPlayers.Contains(targetId)) return true;
        }

        return false;
    }

    private List<PlayerControl> GetOrbitTargets()
    {
        OrbitingPlayers.RemoveAll(id =>
        {
            PlayerControl target = Utils.GetPlayerById(id);
            return target == null || !target.IsAlive() || Pelican.IsEaten(id) || target.onLadder || target.inMovingPlat || target.inVent;
        });

        return OrbitingPlayers
            .Select(id => Utils.GetPlayerById(id))
            .Where(x => x != null)
            .ToList();
    }

    private void ClearOrbit(bool notifySolarist)
    {
        bool hadOrbit = HasOrbit;
        ClearLocalState();

        if (!hadOrbit || !notifySolarist) return;

        PlayerControl solarist = Utils.GetPlayerById(SolaristId);
        if (solarist != null && solarist.IsAlive())
            solarist.Notify(GetString("Solarist.OrbitEnded"));
    }

    private void ClearLocalState()
    {
        OrbitingPlayers.Clear();
        RemainingDuration = 0f;
        OrbitAngle = 0f;
        OrbitTimer = 0f;
    }
}
