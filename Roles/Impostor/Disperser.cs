using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Impostor;

public class Disperser : RoleBase
{
    private const int Id = 17000;

    public static OptionItem DisperserShapeshiftCooldown;
    private static OptionItem DisperserShapeshiftDuration;
    private static OptionItem DisperserLimitOpt;
    public static OptionItem DisperserAbilityUseGainWithEachKill;

    public static bool On;
    public override bool IsEnable => On;

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Disperser);
        DisperserShapeshiftCooldown = new FloatOptionItem(Id + 5, "ShapeshiftCooldown", new(1f, 60f, 1f), 20f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Disperser])
            .SetValueFormat(OptionFormat.Seconds);
        DisperserShapeshiftDuration = new FloatOptionItem(Id + 6, "ShapeshiftDuration", new(1f, 30f, 1f), 1f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Disperser])
            .SetValueFormat(OptionFormat.Seconds);
        DisperserLimitOpt = new IntegerOptionItem(Id + 7, "AbilityUseLimit", new(0, 5, 1), 1, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Disperser])
            .SetValueFormat(OptionFormat.Times);
        DisperserAbilityUseGainWithEachKill = new FloatOptionItem(Id + 8, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 0.3f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Disperser])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        playerId.SetAbilityUseLimit(DisperserLimitOpt.GetInt());
        On = true;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        if (UsePhantomBasis.GetBool()) AURoleOptions.PhantomCooldown = DisperserShapeshiftCooldown.GetFloat();
        else
        {
            if (UsePets.GetBool()) return;
            AURoleOptions.ShapeshifterCooldown = DisperserShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = DisperserShapeshiftDuration.GetFloat();
        }
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (shapeshifter == null || (!shapeshifting && !UseUnshiftTrigger.GetBool())) return false;
        if (shapeshifter.GetAbilityUseLimit() < 1)
        {
            shapeshifter.SetKillCooldown(DisperserShapeshiftDuration.GetFloat() + 1f);
            return false;
        }

        Disperse(shapeshifter);

        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (pc == null || pc.GetAbilityUseLimit() < 1) return false;

        Disperse(pc);

        return false;
    }

    private static void Disperse(PlayerControl player)
    {
        player.RpcRemoveAbilityUse();

        foreach (var pc in PlayerControl.AllPlayerControls)
        {
            if (player.PlayerId == pc.PlayerId || pc.Data.IsDead || pc.onLadder || pc.inVent || GameStates.IsMeeting)
            {
                if (!pc.Is(CustomRoles.Disperser))
                    pc.Notify(ColorString(GetRoleColor(CustomRoles.Disperser), string.Format(GetString("ErrorTeleport"), pc.GetRealName())));

                continue;
            }

            pc.RPCPlayCustomSound("Teleport");
            pc.TPtoRndVent();
            pc.Notify(ColorString(GetRoleColor(CustomRoles.Disperser), string.Format(GetString("TeleportedInRndVentByDisperser"), pc.GetRealName())));
        }
    }

    public override void SetButtonTexts(HudManager __instance, byte id)
    {
        __instance.AbilityButton.ToggleVisible(GetPlayerById(id).IsAlive());

        if (UsePets.GetBool())
        {
            __instance.PetButton?.OverrideText(GetString("DisperserVentButtonText"));
        }
        else
        {
            __instance.AbilityButton?.OverrideText(GetString("DisperserVentButtonText"));
            __instance.AbilityButton?.SetUsesRemaining((int)id.GetAbilityUseLimit());
        }
    }
}