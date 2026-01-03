using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Neutral;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Impostor;

public class Dazzler : RoleBase
{
    private const int Id = 3500;
    public static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem ShapeshiftCooldown;
    private static OptionItem CauseVision;
    public static OptionItem DazzleLimitOpt;
    private static OptionItem ResetDazzledVisionOnDeath;
    public static OptionItem DazzlerAbilityUseGainWithEachKill;

    public List<byte> PlayersDazzled = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Dazzler);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 25f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Dazzler])
            .SetValueFormat(OptionFormat.Seconds);

        ShapeshiftCooldown = new FloatOptionItem(Id + 11, "DazzleCooldown", new(0f, 180f, 0.5f), 20f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Dazzler])
            .SetValueFormat(OptionFormat.Seconds);

        CauseVision = new FloatOptionItem(Id + 13, "DazzlerCauseVision", new(0f, 5f, 0.05f), 0.4f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Dazzler])
            .SetValueFormat(OptionFormat.Multiplier);

        DazzleLimitOpt = new IntegerOptionItem(Id + 14, "DazzlerDazzleLimit", new(1, 15, 1), 1, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Dazzler])
            .SetValueFormat(OptionFormat.Times);

        ResetDazzledVisionOnDeath = new BooleanOptionItem(Id + 15, "DazzlerResetDazzledVisionOnDeath", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Dazzler]);

        DazzlerAbilityUseGainWithEachKill = new FloatOptionItem(Id + 16, "AbilityUseGainWithEachKill", new(0f, 5f, 0.1f), 1f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Dazzler])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PlayerIdList = [];
        PlayersDazzled = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        PlayersDazzled = [];
        playerId.SetAbilityUseLimit(DazzleLimitOpt.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
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

        if (!PlayersDazzled.Contains(target.PlayerId) && PlayersDazzled.Count < pc.GetAbilityUseLimit())
        {
            target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Dazzler), GetString("DazzlerDazzled")));
            PlayersDazzled.Add(target.PlayerId);
            target.MarkDirtySettings();
        }

        return false;
    }

    public static void SetDazzled(PlayerControl player, IGameOptions opt)
    {
        foreach (KeyValuePair<byte, PlayerState> state in Main.PlayerStates)
        {
            if (state.Value.Role is Dazzler { IsEnable: true } dz)
            {
                if (dz.PlayersDazzled.Contains(player.PlayerId) && (!ResetDazzledVisionOnDeath.GetBool() || Utils.GetPlayerById(state.Key).IsAlive()))
                {
                    opt.SetVision(false);
                    opt.SetFloat(FloatOptionNames.CrewLightMod, CauseVision.GetFloat());
                    opt.SetFloat(FloatOptionNames.ImpostorLightMod, CauseVision.GetFloat());
                }
            }
        }
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton?.OverrideText(GetString("DazzleButtonText"));
    }
}
