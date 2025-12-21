using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;
using static EHR.Options;

namespace EHR.Neutral;

internal class Samurai : RoleBase
{
    public static bool On;

    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem NearbyDuration;
    private static OptionItem SuccessKCD;
    private static OptionItem KillDelay;
    private Dictionary<byte, long> Delays;

    private PlayerControl SamuraiPC;
    public (byte Id, long TimeStamp) Target;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        const int id = 16880;
        SetupRoleOptions(id, TabGroup.NeutralRoles, CustomRoles.Samurai);

        KillCooldown = new FloatOptionItem(id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Samurai])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(id + 3, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Samurai]);

        HasImpostorVision = new BooleanOptionItem(id + 4, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Samurai]);

        NearbyDuration = new FloatOptionItem(id + 5, "Samurai.NearbyDuration", new(0f, 30f, 0.5f), 3f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Samurai])
            .SetValueFormat(OptionFormat.Seconds);

        SuccessKCD = new FloatOptionItem(id + 6, "Samurai.SuccessKCD", new(0f, 180f, 0.5f), 17.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Samurai])
            .SetValueFormat(OptionFormat.Seconds);

        KillDelay = new FloatOptionItem(id + 7, "Samurai.KillDelay", new(0f, 60f, 0.5f), 5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Samurai])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        SamuraiPC = Utils.GetPlayerById(playerId);
        Target = (byte.MaxValue, 0);
        Delays = [];
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return Target.Id == byte.MaxValue && pc.IsAlive();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (Target.Id != byte.MaxValue) return false;

        Target = (target.PlayerId, Utils.TimeStamp);
        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || ExileController.Instance != null) return;

        long now = Utils.TimeStamp;

        foreach (KeyValuePair<byte, long> kvp in Delays)
        {
            PlayerControl player = Utils.GetPlayerById(kvp.Key);
            if (player == null || !player.IsAlive()) continue;

            if (kvp.Value + KillDelay.GetInt() <= now)
            {
                if (pc.RpcCheckAndMurder(player, true))
                    player.Suicide(realKiller: pc);
            }
        }

        if (Target.Id == byte.MaxValue || !pc.IsAlive()) return;

        PlayerControl target = Utils.GetPlayerById(Target.Id);
        if (target == null) return;

        if (Vector2.Distance(target.Pos(), pc.Pos()) > NormalGameOptionsV10.KillDistances[Mathf.Clamp(pc.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f)
        {
            Target = (byte.MaxValue, 0);
            pc.RpcCheckAndMurder(target);
            return;
        }

        if (Target.TimeStamp + NearbyDuration.GetInt() <= now)
        {
            Delays[Target.Id] = now;
            Target = (byte.MaxValue, 0);
            pc.SetKillCooldown(SuccessKCD.GetFloat());
        }
    }

    public override void OnReportDeadBody()
    {
        Target = (byte.MaxValue, 0);
        
        foreach (byte id in Delays.Keys)
        {
            PlayerControl player = Utils.GetPlayerById(id);
            if (player == null || !player.IsAlive()) continue;

            if (SamuraiPC.RpcCheckAndMurder(player, true)) player.Suicide(realKiller: SamuraiPC);
        }

        Delays.Clear();
    }
}