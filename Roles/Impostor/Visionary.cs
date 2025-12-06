using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Impostor;

internal class Visionary : RoleBase
{
    public static bool On;

    private static OptionItem UseLimit;
    public static OptionItem VisionaryAbilityUseGainWithEachKill;
    private static OptionItem ShapeshiftCooldown;
    private static OptionItem ChanceOfSeeingAllAlignmentsOnStart;

    public List<byte> RevealedPlayerIds = [];
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(16150, TabGroup.ImpostorRoles, CustomRoles.Visionary);

        UseLimit = new FloatOptionItem(16152, "AbilityUseLimit", new(0, 20, 0.05f), 0, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Visionary])
            .SetValueFormat(OptionFormat.Times);

        VisionaryAbilityUseGainWithEachKill = new FloatOptionItem(16153, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 1f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Visionary])
            .SetValueFormat(OptionFormat.Times);

        ShapeshiftCooldown = new FloatOptionItem(16154, "ShapeshiftCooldown", new(1f, 60f, 1f), 15f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Visionary])
            .SetValueFormat(OptionFormat.Seconds);

        ChanceOfSeeingAllAlignmentsOnStart = new FloatOptionItem(16155, "ChanceOfSeeingAllAlignmentsOnStart", new(0f, 100f, 1f), 0f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Visionary])
            .SetValueFormat(OptionFormat.Percent);
    }

    public override void Add(byte playerId)
    {
        On = true;
        RevealedPlayerIds = [];

        if (IRandom.Instance.Next(100) < ChanceOfSeeingAllAlignmentsOnStart.GetInt())
        {
            RevealedPlayerIds.AddRange(Main.PlayerStates.Keys);
            playerId.SetAbilityUseLimit(0);
            return;
        }

        playerId.SetAbilityUseLimit(UseLimit.GetFloat());
    }

    public override void Init()
    {
        On = false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;

        if (RevealedPlayerIds.Contains(target.PlayerId) || shapeshifter.GetAbilityUseLimit() < 1) return false;

        RevealedPlayerIds.Add(target.PlayerId);
        Utils.SendRPC(CustomRPC.SyncRoleData, shapeshifter.PlayerId, target.PlayerId);
        shapeshifter.RpcRemoveAbilityUse();
        Utils.NotifyRoles(SpecifySeer: shapeshifter, SpecifyTarget: target);

        return false;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        RevealedPlayerIds.Add(reader.ReadByte());
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton?.OverrideText(Translator.GetString("InvestigatorKillButtonText"));
    }
}
