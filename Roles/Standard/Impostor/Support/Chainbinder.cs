using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Roles;

using static Translator;

public class Chainbinder : RoleBase
{
    public static bool On;

    private const float PullInterval = 0.1f;

    private static OptionItem AbilityCooldown;
    private static OptionItem LinkDuration;
    private static OptionItem BindRange;
    private static OptionItem MaxDistance;
    private static OptionItem CanBindImpostors;
    private static List<byte> PlayerIdList = [];

    private byte ChainbinderId;
    private byte FirstTarget = byte.MaxValue;
    private byte SecondTarget = byte.MaxValue;
    private float RemainingDuration;
    private float PullTimer;

    public override bool IsEnable => On;

    private bool HasLink => FirstTarget != byte.MaxValue && SecondTarget != byte.MaxValue;

    public override void SetupCustomOption()
    {
        StartSetup(658380)
            .AutoSetupOption(ref AbilityCooldown, 25f, new FloatValueRule(5f, 60f, 0.5f), OptionFormat.Seconds, overrideName: "ChainbinderCooldown")
            .AutoSetupOption(ref LinkDuration, 10f, new FloatValueRule(2.5f, 60f, 0.5f), OptionFormat.Seconds, overrideName: "ChainbinderDuration")
            .AutoSetupOption(ref BindRange, 3f, new FloatValueRule(1f, 15f, 0.5f), OptionFormat.Multiplier, overrideName: "ChainbinderBindRange")
            .AutoSetupOption(ref MaxDistance, 2f, new FloatValueRule(0.5f, 10f, 0.5f), OptionFormat.Multiplier, overrideName: "ChainbinderMaxDistance")
            .AutoSetupOption(ref CanBindImpostors, false, overrideName: "ChainbinderCanBindImpostors");
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
        ChainbinderId = playerId;
        ClearLocalState();
    }

    public override void Remove(byte playerId)
    {
        ClearLink(sync: true, refresh: false);
        PlayerIdList.Remove(playerId);
        On = PlayerIdList.Count > 0;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePhantomBasis.GetBool())
        {
            AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
            AURoleOptions.PhantomDuration = 1f;
        }
        else
        {
            AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (Options.UsePets.GetBool() && !Options.UsePhantomBasis.GetBool())
            hud.PetButton?.OverrideText(GetString("ChainbinderButtonText"));
        else
            hud.AbilityButton?.OverrideText(GetString("ChainbinderButtonText"));
    }

    public override void OnPet(PlayerControl pc)
    {
        TryChainPlayers(pc);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        TryChainPlayers(pc);
        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (shapeshifting) TryChainPlayers(shapeshifter, target);
        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!HasLink) return;

        if (!pc.IsAlive() || !GameStates.IsInTask)
        {
            ClearLink(sync: true, refresh: true);
            return;
        }

        RemainingDuration -= UnityEngine.Time.fixedDeltaTime;
        if (RemainingDuration <= 0f)
        {
            ClearLink(notifyBinder: true);
            return;
        }

        PlayerControl first = Utils.GetPlayerById(FirstTarget);
        PlayerControl second = Utils.GetPlayerById(SecondTarget);

        if (!AreLinkedPlayersValid(first, second))
        {
            ClearLink(notifyBinder: true);
            return;
        }

        if (first.onLadder || first.inMovingPlat || first.inVent || second.onLadder || second.inMovingPlat || second.inVent) return;

        PullTimer -= UnityEngine.Time.fixedDeltaTime;
        if (PullTimer > 0f) return;

        float currentDistance = Vector2.Distance(first.Pos(), second.Pos());
        float maxDistance = MaxDistance.GetFloat();

        if (currentDistance <= maxDistance) return;

        Vector2 direction = (second.Pos() - first.Pos()).normalized;
        if (direction == Vector2.zero) return;

        PullTimer = PullInterval;
        Vector2 adjustment = direction * ((currentDistance - maxDistance) / 2f);

        first.TP(first.Pos() + adjustment, log: false);
        second.TP(second.Pos() - adjustment, log: false);
    }

    public override void OnReportDeadBody()
    {
        ClearLink(sync: true, refresh: true);
    }

    public override void AfterMeetingTasks()
    {
        ClearLink(sync: true, refresh: true);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != ChainbinderId || (seer.IsModdedClient() && !hud) || meeting || !HasLink) return string.Empty;
        return string.Format(GetString("Chainbinder.PairSuffix"), FirstTarget.ColoredPlayerName(), SecondTarget.ColoredPlayerName());
    }

    public void ReceiveRPC(MessageReader reader)
    {
        FirstTarget = reader.ReadByte();
        SecondTarget = reader.ReadByte();
        RemainingDuration = HasLink ? LinkDuration.GetFloat() : 0f;
        PullTimer = 0f;
    }

    private void TryChainPlayers(PlayerControl binder, PlayerControl requestedTarget = null)
    {
        if (!binder || binder.PlayerId != ChainbinderId || !binder.IsAlive()) return;

        if (!TryGetTargets(binder, requestedTarget, out PlayerControl first, out PlayerControl second))
        {
            binder.Notify(GetString("Chainbinder.NotEnoughTargets"));
            binder.RpcResetAbilityCooldown();
            return;
        }

        FirstTarget = first.PlayerId;
        SecondTarget = second.PlayerId;
        RemainingDuration = LinkDuration.GetFloat();
        PullTimer = 0f;

        SyncState();

        binder.Notify(string.Format(GetString("Chainbinder.PairBound"), FirstTarget.ColoredPlayerName(), SecondTarget.ColoredPlayerName()));
        Utils.NotifyRoles(SpecifySeer: binder, SpecifyTarget: binder);

        Logger.Info($"{binder.GetNameWithRole().RemoveHtmlTags()} chained {first.GetNameWithRole().RemoveHtmlTags()} to {second.GetNameWithRole().RemoveHtmlTags()}", "Chainbinder");
    }

    private bool TryGetTargets(PlayerControl binder, PlayerControl requestedTarget, out PlayerControl first, out PlayerControl second)
    {
        first = null;
        second = null;

        List<PlayerControl> validTargets = Main.EnumerateAlivePlayerControls()
            .Where(x => IsValidTarget(binder, x))
            .ToList();

        if (requestedTarget)
        {
            if (!IsValidTarget(binder, requestedTarget)) return false;

            first = requestedTarget;
            byte firstId = first.PlayerId;
            Vector2 firstPosition = first.Pos();
            second = validTargets
                .Where(x => x.PlayerId != firstId)
                .OrderBy(x => Vector2.Distance(x.Pos(), firstPosition))
                .FirstOrDefault();

            return second;
        }

        PlayerControl[] closestTargets = validTargets
            .OrderBy(x => Vector2.Distance(x.Pos(), binder.Pos()))
            .Take(2)
            .ToArray();

        if (closestTargets.Length < 2) return false;

        first = closestTargets[0];
        second = closestTargets[1];
        return true;
    }

    private bool IsValidTarget(PlayerControl binder, PlayerControl target)
    {
        if (!binder || !target || target.PlayerId == binder.PlayerId || !target.IsAlive()) return false;
        if (Pelican.IsEaten(target.PlayerId) || target.onLadder || target.inMovingPlat || target.inVent) return false;
        if (!FastVector2.DistanceWithinRange(binder.Pos(), target.Pos(), BindRange.GetFloat())) return false;
        if (!CanBindImpostors.GetBool() && target.GetCustomRole().IsImpostor()) return false;
        return !IsLinkedByAnotherChainbinder(target.PlayerId);
    }

    private bool IsLinkedByAnotherChainbinder(byte targetId)
    {
        foreach (byte playerId in PlayerIdList)
        {
            if (playerId == ChainbinderId || !Main.PlayerStates.TryGetValue(playerId, out PlayerState state) || state.Role is not Chainbinder { HasLink: true } cb) continue;
            if (cb.FirstTarget == targetId || cb.SecondTarget == targetId) return true;
        }

        return false;
    }

    private static bool AreLinkedPlayersValid(PlayerControl first, PlayerControl second)
    {
        return first && second && first.IsAlive() && second.IsAlive() && !Pelican.IsEaten(first.PlayerId) && !Pelican.IsEaten(second.PlayerId);
    }

    private void ClearLink(bool notifyBinder = false, bool sync = true, bool refresh = true)
    {
        bool hadLink = HasLink;
        ClearLocalState();

        if (sync) SyncState();
        if (!hadLink) return;

        PlayerControl binder = Utils.GetPlayerById(ChainbinderId);
        if (notifyBinder && binder && binder.IsAlive())
            binder.Notify(GetString("Chainbinder.PairEnded"));

        if (refresh && binder)
            Utils.NotifyRoles(SpecifySeer: binder, SpecifyTarget: binder);
    }

    private void ClearLocalState()
    {
        FirstTarget = byte.MaxValue;
        SecondTarget = byte.MaxValue;
        RemainingDuration = 0f;
        PullTimer = 0f;
    }

    private void SyncState()
    {
        Utils.SendRPC(CustomRPC.SyncRoleData, ChainbinderId, FirstTarget, SecondTarget);
    }
}
