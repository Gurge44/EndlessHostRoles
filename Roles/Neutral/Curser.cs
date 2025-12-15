using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

namespace EHR.Neutral;

public class Curser : RoleBase
{
    public static bool On;
    private static List<Curser> Instances = [];

    private static OptionItem AbilityUseLimit;
    private static OptionItem AbilityCooldown;
    private static OptionItem ImpostorVision;
    private static OptionItem CanVent;
    private static OptionItem LowerVision;
    private static OptionItem LowerSpeed;

    public HashSet<byte> KnownFactionPlayers = [];
    private HashSet<byte> LowerSpeedPlayers = [];

    private HashSet<byte> LowerVisionPlayers = [];

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(645450)
            .AutoSetupOption(ref AbilityUseLimit, 3f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityCooldown, 15f, new FloatValueRule(0.5f, 90f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref ImpostorVision, false)
            .AutoSetupOption(ref CanVent, false)
            .AutoSetupOption(ref LowerVision, 0.3f, new FloatValueRule(0f, 1.5f, 0.05f), OptionFormat.Multiplier)
            .AutoSetupOption(ref LowerSpeed, 0.7f, new FloatValueRule(0.05f, 3f, 0.05f), OptionFormat.Multiplier);
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
        LowerVisionPlayers = [];
        LowerSpeedPlayers = [];
        KnownFactionPlayers = [];
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive() && pc.GetAbilityUseLimit() > 0;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = AbilityCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(ImpostorVision.GetBool());
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.CheckDoubleTrigger(target, () =>
        {
            KnownFactionPlayers.Add(target.PlayerId);
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            killer.RpcRemoveAbilityUse();
        }))
        {
            Pick:
            int random = IRandom.Instance.Next(4);

            switch (random)
            {
                case 0:
                    target.RpcRemoveAbilityUse();
                    break;
                case 1:
                    CustomRoles addon = Options.GroupedAddons[AddonTypes.Harmful].Where(x => !target.Is(x) && !x.IsNotAssignableMidGame() && CustomRolesHelper.CheckAddonConflict(x, target)).RandomElement();
                    if (addon == default(CustomRoles)) goto Pick;
                    target.RpcSetCustomRole(addon);
                    break;
                case 2:
                    LowerVisionPlayers.Add(target.PlayerId);
                    target.MarkDirtySettings();
                    break;
                case 3:
                    LowerSpeedPlayers.Add(target.PlayerId);
                    target.MarkDirtySettings();
                    break;
            }

            killer.Notify(Translator.GetString($"Curser.Notify.{random}"));
            killer.SetKillCooldown();
            killer.RpcRemoveAbilityUse();
        }

        return false;
    }

    public static void OnAnyoneApplyGameOptions(IGameOptions opt, byte playerId)
    {
        foreach (Curser instance in Instances)
        {
            if (instance.LowerVisionPlayers.Contains(playerId))
            {
                opt.SetVision(false);
                float vision = LowerVision.GetFloat();
                opt.SetFloat(FloatOptionNames.CrewLightMod, vision);
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, vision);
            }

            if (instance.LowerSpeedPlayers.Contains(playerId))
                Main.AllPlayerSpeed[playerId] = LowerSpeed.GetFloat();
        }
    }

    public void OnDeath()
    {
        LowerVisionPlayers.UnionWith(LowerSpeedPlayers);
        PlayerControl[] players = LowerVisionPlayers.ToValidPlayers().ToArray();
        LowerVisionPlayers.Clear();
        LowerSpeedPlayers.Clear();
        players.Do(x => x.MarkDirtySettings());
    }
}