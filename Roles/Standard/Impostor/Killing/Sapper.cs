using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles;

public class Sapper : RoleBase
{
    private const int Id = 643000;
    public static List<byte> PlayerIdList = [];

    public static OptionItem ShapeshiftCooldown;
    private static OptionItem Delay;
    private static OptionItem Radius;
    private static OptionItem CanSabotage;
    private static OptionItem CanKill;
    private static OptionItem CooldownsResetEachOther;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Sapper);

        ShapeshiftCooldown = new FloatOptionItem(Id + 11, "SapperCD", new(0f, 180f, 0.5f), 30f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sapper])
            .SetValueFormat(OptionFormat.Seconds);

        Delay = new IntegerOptionItem(Id + 12, "SapperDelay", new(1, 15, 1), 5, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sapper])
            .SetValueFormat(OptionFormat.Times);

        Radius = new FloatOptionItem(Id + 13, "SapperRadius", new(0f, 10f, 0.25f), 3f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sapper])
            .SetValueFormat(OptionFormat.Multiplier);

        CanSabotage = new BooleanOptionItem(Id + 14, "CanSabotage", false, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sapper]);
        
        CanKill = new BooleanOptionItem(Id + 15, "CanKill", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Sapper]);
        
        CooldownsResetEachOther = new BooleanOptionItem(Id + 16, "CooldownsResetEachOther", true, TabGroup.ImpostorRoles)
            .SetParent(CanKill);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        if (UsePhantomBasis.GetBool())
            AURoleOptions.PhantomCooldown = ShapeshiftCooldown.GetFloat();
        else
        {
            AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }
    }

    public override bool CanUseSabotage(PlayerControl pc)
    {
        return CanSabotage.GetBool();
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;
        return PlaceBomb(shapeshifter);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return CanKill.GetBool();
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (!CooldownsResetEachOther.GetBool()) return;
        
        if (UsePets.GetBool() && !UsePhantomBasis.GetBool())
            killer.AddAbilityCD();
        else
            killer.RpcResetAbilityCooldown();
    }

    public override void OnPet(PlayerControl pc)
    {
        PlaceBomb(pc);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        return PlaceBomb(pc);
    }

    private static bool PlaceBomb(PlayerControl pc)
    {
        if (pc == null) return false;
        if (!pc.IsAlive() || Pelican.IsEaten(pc.PlayerId)) return false;
        if (CanKill.GetBool() && CooldownsResetEachOther.GetBool()) pc.SetKillCooldown();
        Vector2 pos = pc.Pos();
        CountdownTimer timer = null;
        timer = new CountdownTimer(Delay.GetInt(), () =>
        {
            foreach (PlayerControl tg in FastVector2.GetPlayersInRange(pos, Radius.GetFloat()))
            {
                if (tg.PlayerId == pc.PlayerId)
                {
                    LateTask.New(() =>
                    {
                        if (!GameStates.IsEnded) pc.Suicide(PlayerState.DeathReason.Bombed);
                    }, 0.5f, "Sapper Bomb Suicide");
                    continue;
                }

                tg.Suicide(PlayerState.DeathReason.Bombed, pc);

                if (pc.AmOwner && tg.IsImpostor())
                    Achievements.Type.FriendlyFire.Complete();
            }

            pc.Notify(GetString("MagicianBombExploded"));
        }, onTick: () =>
        {
            // ReSharper disable once AccessToModifiedClosure
            pc.Notify(string.Format(GetString("MagicianBombExlodesIn"), (int)Math.Ceiling(timer?.Remaining.TotalSeconds ?? 0)), 3f, overrideAll: true);
        });
        return false;
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (UsePets.GetBool() && !UsePhantomBasis.GetBool())
            hud.PetButton?.OverrideText(GetString("BomberShapeshiftText"));
        else
            hud.AbilityButton?.OverrideText(GetString("BomberShapeshiftText"));
    }
}