using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral;

public class Auditor : RoleBase
{
    public static bool On;
    private static List<Auditor> Instances = [];

    private static OptionItem LoweredVision;
    private static OptionItem LoweredVisionDuration;
    private static OptionItem StealAllChargesInsteadOfOne;
    private static OptionItem AbilityUseLimit;
    private static OptionItem SmokebombCooldown;
    private static OptionItem AuditCooldown;

    private AbilityTriggers AbilityTrigger;
    private byte AuditorID;
    private Dictionary<byte, long> LoweredVisionPlayers = [];
    private Modes Mode;
    private HashSet<byte> RevealedPlayers = [];

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(645300)
            .AutoSetupOption(ref LoweredVision, 0.3f, new FloatValueRule(0.05f, 1.25f, 0.05f), OptionFormat.Multiplier)
            .AutoSetupOption(ref LoweredVisionDuration, 10, new IntegerValueRule(0, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref StealAllChargesInsteadOfOne, false)
            .AutoSetupOption(ref AbilityUseLimit, 5, new IntegerValueRule(1, 20, 1), OptionFormat.Times)
            .AutoSetupOption(ref SmokebombCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AuditCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        AuditorID = playerId;
        AbilityTrigger = Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool() ? AbilityTriggers.Vanish : Options.UseUnshiftTrigger.GetBool() && Options.UseUnshiftTriggerForNKs.GetBool() ? AbilityTriggers.Unshift : Options.UsePets.GetBool() ? AbilityTriggers.Pet : AbilityTriggers.Vent;
        Mode = Modes.Auditing;
        LoweredVisionPlayers = [];
        RevealedPlayers = [];
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetInt());
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.GetAbilityUseLimit() > 0;
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return pc.IsAlive() && AbilityTrigger == AbilityTriggers.Vent;
    }

    public static void OnAnyoneApplyGameOptions(IGameOptions opt, byte id)
    {
        if (!Instances.Any(x => x.LoweredVisionPlayers.ContainsKey(id))) return;

        opt.SetVision(false);
        var vision = LoweredVision.GetFloat();
        opt.SetFloat(FloatOptionNames.CrewLightMod, vision);
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, vision);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        switch (Mode)
        {
            case Modes.Auditing:
            {
                if (StealAllChargesInsteadOfOne.GetBool())
                {
                    if (!float.IsNaN(target.GetAbilityUseLimit()))
                        target.SetAbilityUseLimit(0);
                }
                else target.RpcRemoveAbilityUse();

                killer.SetKillCooldown(AuditCooldown.GetFloat());
                break;
            }
            case Modes.Smokebombing:
            {
                var containsKey = LoweredVisionPlayers.ContainsKey(target.PlayerId);
                LoweredVisionPlayers[target.PlayerId] = Utils.TimeStamp + LoweredVisionDuration.GetInt();
                if (!containsKey) target.MarkDirtySettings();
                killer.SetKillCooldown(SmokebombCooldown.GetFloat());
                break;
            }
        }

        if (RevealedPlayers.Add(target.PlayerId))
        {
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, 1, target.PlayerId);
        }

        return false;
    }

    public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
    {
        if (AbilityTrigger != AbilityTriggers.Vent) return;
        SwitchMode(physics.myPlayer);
    }

    public override void OnPet(PlayerControl pc)
    {
        if (AbilityTrigger != AbilityTriggers.Pet) return;
        SwitchMode(pc);
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting && AbilityTrigger == AbilityTriggers.Unshift)
            SwitchMode(shapeshifter);

        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (AbilityTrigger == AbilityTriggers.Vanish)
            SwitchMode(pc);

        return false;
    }

    void SwitchMode(PlayerControl pc)
    {
        Mode = Mode == Modes.Auditing ? Modes.Smokebombing : Modes.Auditing;
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 2, (int)Mode);
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance) return;

        byte[] toRemove = LoweredVisionPlayers.Where(x => x.Value <= Utils.TimeStamp).Select(x => x.Key).ToArray();

        foreach (byte id in toRemove)
        {
            LoweredVisionPlayers.Remove(id);
            id.GetPlayer().MarkDirtySettings();
        }
    }

    public override void OnReportDeadBody()
    {
        LoweredVisionPlayers.Clear();
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                RevealedPlayers.Add(reader.ReadByte());
                break;
            case 2:
                Mode = (Modes)reader.ReadPackedInt32();
                break;
        }
    }

    public override bool KnowRole(PlayerControl seer, PlayerControl target)
    {
        if (base.KnowRole(seer, target)) return true;
        return seer.PlayerId == AuditorID && RevealedPlayers.Contains(target.PlayerId);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != AuditorID || seer.PlayerId != target.PlayerId || (seer.IsModClient() && !hud) || meeting) return string.Empty;
        return string.Format(Translator.GetString($"Auditor.Suffix.{Mode}"), Translator.GetString($"OccultistActionSwitchMode.{AbilityTrigger}"));
    }

    enum AbilityTriggers
    {
        Vent,
        Pet,
        Unshift,
        Vanish
    }

    enum Modes
    {
        Auditing,
        Smokebombing
    }
}