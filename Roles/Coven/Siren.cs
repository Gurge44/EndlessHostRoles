using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;

namespace EHR.Coven;

public class Siren : Coven
{
    public static bool On;
    private static List<Siren> Instances = [];

    private static OptionItem AbilityCooldown;
    private static OptionItem SingRange;
    private static OptionItem Stage1EffectsLength;
    private static OptionItem Stage2EffectsLength;
    private static OptionItem ReducedVision;
    private static OptionItem ReducedSpeed;
    public static OptionItem EntrancedCanKillCoven;
    public static OptionItem CovenKnowEntranced;
    public static OptionItem EntrancedKnowEntranced;
    public static OptionItem CovenCanKillEntranced;
    public static OptionItem EntrancedCountMode;
    public static OptionItem EntrancedKnowCoven;
    private static OptionItem CanVentBeforeNecronomicon;
    private static OptionItem CanVentAfterNecronomicon;

    private static readonly string[] CovenKnowEntrancedOptions =
    [
        "CKEO.SirenOnly",
        "CKEO.AllCoven"
    ];

    private static readonly string[] EntrancedCountModeOptions =
    [
        "ECMO.Nothing",
        "ECMO.Coven",
        "ECMO.OriginalTeam"
    ];

    private Dictionary<byte, long> EffectEndTS;
    private byte SirenId;

    private Dictionary<byte, int> Stages;

    protected override NecronomiconReceivePriorities NecronomiconReceivePriority => NecronomiconReceivePriorities.Random;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(650130)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref SingRange, 4f, new FloatValueRule(0.25f, 10f, 0.25f), OptionFormat.Multiplier)
            .AutoSetupOption(ref Stage1EffectsLength, 10, new IntegerValueRule(0, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref Stage2EffectsLength, 10, new IntegerValueRule(0, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref ReducedVision, 0.25f, new FloatValueRule(0f, 1f, 0.05f), OptionFormat.Multiplier)
            .AutoSetupOption(ref ReducedSpeed, 0.25f, new FloatValueRule(0f, 2f, 0.05f), OptionFormat.Multiplier)
            .AutoSetupOption(ref EntrancedCanKillCoven, false)
            .AutoSetupOption(ref CovenKnowEntranced, 1, CovenKnowEntrancedOptions)
            .AutoSetupOption(ref EntrancedKnowEntranced, false)
            .AutoSetupOption(ref CovenCanKillEntranced, true)
            .AutoSetupOption(ref EntrancedCountMode, 1, EntrancedCountModeOptions)
            .AutoSetupOption(ref EntrancedKnowCoven, 1, CovenKnowEntrancedOptions)
            .AutoSetupOption(ref CanVentBeforeNecronomicon, false)
            .AutoSetupOption(ref CanVentAfterNecronomicon, true);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Stages = [];
        EffectEndTS = [];
        SirenId = playerId;
        Instances.Add(this);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
    }

    public static void ApplyGameOptionsForOthers(IGameOptions opt, byte playerId)
    {
        foreach (Siren instance in Instances)
        {
            if (instance.EffectEndTS.ContainsKey(playerId))
            {
                float vision = ReducedVision.GetFloat();
                opt.SetVision(false);
                opt.SetFloat(FloatOptionNames.CrewLightMod, vision);
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, vision);
                Main.AllPlayerSpeed[playerId] = ReducedSpeed.GetFloat();
            }
        }
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return HasNecronomicon ? CanVentAfterNecronomicon.GetBool() : CanVentBeforeNecronomicon.GetBool();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return HasNecronomicon;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        PlayerControl[] nearbyPlayers = Utils.GetPlayersInRadius(SingRange.GetFloat(), pc.Pos()).Without(pc).ToArray();

        var sender = CustomRpcSender.Create("Siren.OnVanish", SendOption.Reliable);
        var hasValue = false;

        foreach (PlayerControl player in nearbyPlayers)
        {
            if (!Stages.TryGetValue(player.PlayerId, out var stage))
                Stages[player.PlayerId] = stage = HasNecronomicon ? 2 : 1;

            switch (stage)
            {
                case 1:
                    EffectEndTS[player.PlayerId] = Utils.TimeStamp + Stage1EffectsLength.GetInt();
                    player.MarkDirtySettings();
                    hasValue |= sender.NotifyRolesSpecific(pc, player, out sender, out bool cleared);
                    if (cleared) hasValue = false;
                    break;
                case 2:
                    ReportDeadBodyPatch.CanReport[player.PlayerId] = false;
                    EffectEndTS[player.PlayerId] = Utils.TimeStamp + Stage2EffectsLength.GetInt();
                    player.MarkDirtySettings();
                    hasValue |= sender.NotifyRolesSpecific(pc, player, out sender, out cleared);
                    if (cleared) hasValue = false;
                    break;
                case 3:
                    player.RpcSetCustomRole(CustomRoles.Entranced);
                    break;
            }
        }

        sender.SendMessage(!hasValue);
        return false;
    }

    public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
    {
        if (lowLoad || !GameStates.IsInTask || ExileController.Instance || !pc.IsAlive() || !EffectEndTS.TryGetValue(pc.PlayerId, out var endTS) || endTS > Utils.TimeStamp) return;

        ReportDeadBodyPatch.CanReport[pc.PlayerId] = true;
        EffectEndTS.Remove(pc.PlayerId);
        pc.MarkDirtySettings();
    }

    public override bool KnowRole(PlayerControl player, PlayerControl target)
    {
        if (base.KnowRole(player, target)) return true;
        if (player.Is(CustomRoles.Entranced) && (target.Is(CustomRoles.Siren) || (target.Is(CustomRoleTypes.Coven) && EntrancedKnowCoven.GetValue() == 1))) return true;
        return EntrancedKnowEntranced.GetBool() && player.Is(CustomRoles.Entranced) && target.Is(CustomRoles.Entranced);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != SirenId || hud || seer.PlayerId == target.PlayerId) return string.Empty;
        return $"{Stages.GetValueOrDefault(target.PlayerId, 0)}/3";
    }
}