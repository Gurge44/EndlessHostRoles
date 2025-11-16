using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;

namespace EHR.Neutral;

internal class Tiger : RoleBase
{
    private const int Id = 643500;

    public static OptionItem Radius;
    public static OptionItem EnrageCooldown;
    public static OptionItem EnrageDuration;
    public static OptionItem KillCooldown;
    public static OptionItem CanVent;
    public static OptionItem ImpostorVision;

    public static bool On;

    private float CooldownTimer;
    private int Count;
    public float EnrageTimer;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Tiger);

        Radius = new FloatOptionItem(Id + 2, "TigerRadius", new(0.5f, 10f, 0.5f), 3f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger])
            .SetValueFormat(OptionFormat.Multiplier);

        EnrageCooldown = new FloatOptionItem(Id + 3, "EnrageCooldown", new(0f, 60f, 0.5f), 15f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger])
            .SetValueFormat(OptionFormat.Seconds);

        EnrageDuration = new FloatOptionItem(Id + 4, "EnrageDuration", new(1f, 30f, 1f), 15f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger])
            .SetValueFormat(OptionFormat.Seconds);

        KillCooldown = new FloatOptionItem(Id + 5, "KillCooldown", new(0f, 60f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 6, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger]);

        ImpostorVision = new BooleanOptionItem(Id + 7, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Tiger]);
    }

    public override void Add(byte playerId)
    {
        On = true;
        EnrageTimer = float.NaN;
        CooldownTimer = 0f;
    }

    public override void Init()
    {
        On = false;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool CanUseSabotage(PlayerControl pc)
    {
        return base.CanUseSabotage(pc) || (pc.IsAlive() && !(Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool()));
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        opt.SetVision(ImpostorVision.GetBool());

        if (Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool())
            AURoleOptions.PhantomCooldown = EnrageCooldown.GetFloat() + EnrageDuration.GetFloat();
    }

    public override bool OnSabotage(PlayerControl pc)
    {
        if (CooldownTimer <= 0f)
        {
            StartEnraging();
            CooldownTimer = EnrageCooldown.GetFloat() + EnrageDuration.GetFloat();
        }

        return pc.Is(CustomRoles.Mischievous);
    }

    public override void OnPet(PlayerControl pc)
    {
        StartEnraging();
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (CooldownTimer <= 0f)
        {
            StartEnraging();
            CooldownTimer = EnrageCooldown.GetFloat() + EnrageDuration.GetFloat();
        }

        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;

        if (CooldownTimer <= 0f)
        {
            StartEnraging();
            CooldownTimer = EnrageCooldown.GetFloat() + EnrageDuration.GetFloat();
        }

        return false;
    }

    private void StartEnraging()
    {
        EnrageTimer = EnrageDuration.GetFloat();
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (CooldownTimer > 0f) CooldownTimer -= Time.fixedDeltaTime;

        if (float.IsNaN(EnrageTimer)) return;

        EnrageTimer -= Time.fixedDeltaTime;

        Count++;
        if (Count < 10) return;

        Count = 0;

        Utils.SendRPC(CustomRPC.SyncTiger, pc.PlayerId, EnrageTimer);

        switch (EnrageTimer)
        {
            case <= 0f:
                EnrageTimer = float.NaN;
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                break;
            case <= 5f:
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                break;
        }
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        if (float.IsNaN(EnrageTimer)) return;

        PlayerControl victim = Main.AllAlivePlayerControls.Where(x => x.PlayerId != killer.PlayerId && x.PlayerId != target.PlayerId).MinBy(x => Vector2.Distance(killer.Pos(), x.Pos()));
        if (victim == null || Vector2.Distance(killer.Pos(), victim.Pos()) > Radius.GetFloat()) return;

        if (killer.RpcCheckAndMurder(victim, true)) victim.Suicide(realKiller: killer);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || Main.PlayerStates[seer.PlayerId].Role is not Tiger { IsEnable: true } tg || float.IsNaN(tg.EnrageTimer) || hud || meeting) return string.Empty;
        return tg.EnrageTimer > 5 ? "\u25a9" : $"\u25a9 ({(int)(tg.EnrageTimer + 1)}s)";
    }
}