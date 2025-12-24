using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Impostor;

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

    private static Dictionary<Vector2, long> Bombs = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    private long LastNotify;

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
        Bombs = [];
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
        Bombs.TryAdd(pc.Pos(), TimeStamp);
        if (CanKill.GetBool() && CooldownsResetEachOther.GetBool()) pc.SetKillCooldown();
        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (pc == null || Bombs.Count == 0 || !GameStates.IsInTask || !pc.IsAlive()) return;

        foreach (KeyValuePair<Vector2, long> bomb in Bombs.Where(bomb => bomb.Value + Delay.GetInt() < TimeStamp).ToArray())
        {
            var b = false;
            IEnumerable<PlayerControl> players = GetPlayersInRadius(Radius.GetFloat(), bomb.Key);

            foreach (PlayerControl tg in players)
            {
                if (tg.PlayerId == pc.PlayerId)
                {
                    b = true;
                    continue;
                }

                tg.Suicide(PlayerState.DeathReason.Bombed, pc);
                
                if (pc.AmOwner && tg.IsImpostor())
                    Achievements.Type.FriendlyFire.Complete();
            }

            Bombs.Remove(bomb.Key);
            pc.Notify(GetString("MagicianBombExploded"));

            if (b)
            {
                LateTask.New(() =>
                {
                    if (!GameStates.IsEnded) pc.Suicide(PlayerState.DeathReason.Bombed);
                }, 0.5f, "Sapper Bomb Suicide");
            }
        }

        long now = TimeStamp;
        if (LastNotify == now) return;
        LastNotify = now;

        var sb = new StringBuilder();
        foreach (long x in Bombs.Values) sb.Append(string.Format(GetString("MagicianBombExlodesIn"), Delay.GetInt() - (TimeStamp - x) + 1));

        pc.Notify(sb.ToString(), overrideAll: true);
    }

    public override void OnReportDeadBody()
    {
        Bombs.Clear();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (UsePets.GetBool() && !UsePhantomBasis.GetBool())
            hud.PetButton?.OverrideText(GetString("BomberShapeshiftText"));
        else
            hud.AbilityButton?.OverrideText(GetString("BomberShapeshiftText"));
    }
}