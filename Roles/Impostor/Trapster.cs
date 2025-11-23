using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;

namespace EHR.Impostor;

internal class Trapster : RoleBase
{
    private static List<byte> TrapsterBody = [];
    private static Dictionary<byte, byte> KillerOfTrapsterBody = [];

    public static bool On;

    public static OptionItem LegacyTrapster;
    private static OptionItem TrapsterKillCooldown;
    private static OptionItem TrapOnlyWorksOnTheBodyTrapster;
    private static OptionItem TrapConsecutiveBodies;
    public static OptionItem AbilityCooldown;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(16500, TabGroup.ImpostorRoles, CustomRoles.Trapster);

        AbilityCooldown = new FloatOptionItem(16510, "AbilityCooldown", new(1f, 60f, 1f), 20f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Trapster])
            .SetValueFormat(OptionFormat.Seconds);

        TrapsterKillCooldown = new FloatOptionItem(16511, "KillCooldown", new(2.5f, 180f, 0.5f), 20f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Trapster])
            .SetValueFormat(OptionFormat.Seconds);

        LegacyTrapster = new BooleanOptionItem(16512, "UseLegacyVersion", false, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Trapster]);

        TrapOnlyWorksOnTheBodyTrapster = new BooleanOptionItem(16513, "TrapOnlyWorksOnTheBodyTrapster", true, TabGroup.ImpostorRoles)
            .SetParent(LegacyTrapster);

        TrapConsecutiveBodies = new BooleanOptionItem(16514, "TrapConsecutiveBodies", true, TabGroup.ImpostorRoles)
            .SetParent(LegacyTrapster);
    }

    public override void Add(byte playerId)
    {
        On = true;
    }

    public override void Init()
    {
        On = false;
        TrapsterBody = [];
        KillerOfTrapsterBody = [];
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = TrapsterKillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (!LegacyTrapster.GetBool())
        {
            if (UsePhantomBasis.GetBool())
                AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
            else
            {
                if (UsePets.GetBool()) return;

                AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetFloat();
                AURoleOptions.ShapeshifterDuration = 0.1f;
            }
        }
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        if (TrapOnlyWorksOnTheBodyTrapster.GetBool() && !GameStates.IsMeeting && LegacyTrapster.GetBool())
            TrapsterBody.Add(target.PlayerId);

        return true;
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (!TrapOnlyWorksOnTheBodyTrapster.GetBool() && killer != target && LegacyTrapster.GetBool())
        {
            if (!TrapsterBody.Contains(target.PlayerId))
                TrapsterBody.Add(target.PlayerId);

            KillerOfTrapsterBody.TryAdd(target.PlayerId, killer.PlayerId);

            killer.Suicide();
        }
    }

    public static bool OnAnyoneCheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (TrapsterBody.Contains(target.PlayerId) && reporter.IsAlive() && !target.Object.Is(CustomRoles.Disregarded) && LegacyTrapster.GetBool())
        {
            if (!TrapOnlyWorksOnTheBodyTrapster.GetBool())
            {
                byte killerID = KillerOfTrapsterBody[target.PlayerId];

                reporter.Suicide(PlayerState.DeathReason.Trapped, Utils.GetPlayerById(killerID));
                RPC.PlaySoundRPC(killerID, Sounds.KillSound);

                if (!TrapsterBody.Contains(reporter.PlayerId) && TrapConsecutiveBodies.GetBool())
                    TrapsterBody.Add(reporter.PlayerId);

                KillerOfTrapsterBody.TryAdd(reporter.PlayerId, killerID);
                return false;
            }

            byte killerID2 = target.PlayerId;

            reporter.Suicide(PlayerState.DeathReason.Trapped, Utils.GetPlayerById(killerID2));
            RPC.PlaySoundRPC(killerID2, Sounds.KillSound);
            return false;
        }

        if (TrapsterBody.Contains(target.PlayerId) && reporter.IsAlive() && !LegacyTrapster.GetBool())
        {
            if (reporter.IsImpostor()) return true;
            reporter.Suicide(PlayerState.DeathReason.Trapped, Utils.GetPlayerById(target.PlayerId));
            RPC.PlaySoundRPC(target.PlayerId, Sounds.KillSound);
            return false;
        }

        return true;
    }

    public override bool OnVanish(PlayerControl player)
    {
        if (!LegacyTrapster.GetBool())
        {
            Vector2 location = player.Pos();
            TrapsterBody.Add(player.PlayerId);
            Utils.RpcCreateDeadBody(location, (byte)IRandom.Instance.Next(17), player);
            return false;
        }

        return base.OnVanish(player);
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;
        OnVanish(shapeshifter);
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        OnVanish(pc);
    }

    public override bool CheckReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, PlayerControl killer)
    {
        return target == null || target.PlayerId != reporter.PlayerId;
    }
}