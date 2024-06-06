using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;
using static EHR.Translator;

namespace EHR.Roles.Neutral;

public class Jackal : RoleBase
{
    private const int Id = 12100;
    public static List<byte> PlayerIdList = [];

    public static OptionItem KillCooldown;
    public static OptionItem CanVent;
    public static OptionItem CanSabotage;
    public static OptionItem CanWinBySabotageWhenNoImpAlive;
    public static OptionItem HasImpostorVision;
    public static OptionItem ResetKillCooldownWhenSbGetKilled;
    private static OptionItem ResetKillCooldownOn;
    public static OptionItem SidekickCountMode;
    public static OptionItem KillCooldownSK;
    public static OptionItem CanVentSK;
    public static OptionItem CanSabotageSK;

    public static bool On;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Jackal);
        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = new BooleanOptionItem(Id + 11, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanSabotage = new BooleanOptionItem(Id + 12, "CanSabotage", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        CanWinBySabotageWhenNoImpAlive = new BooleanOptionItem(Id + 14, "JackalCanWinBySabotageWhenNoImpAlive", true, TabGroup.NeutralRoles)
            .SetParent(CanSabotage);
        HasImpostorVision = new BooleanOptionItem(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        ResetKillCooldownWhenSbGetKilled = new BooleanOptionItem(Id + 16, "ResetKillCooldownWhenPlayerGetKilled", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        ResetKillCooldownOn = new FloatOptionItem(Id + 28, "ResetKillCooldownOn", new(0f, 180f, 2.5f), 15f, TabGroup.NeutralRoles)
            .SetParent(ResetKillCooldownWhenSbGetKilled)
            .SetValueFormat(OptionFormat.Seconds);
        JackalCanKillSidekick = new BooleanOptionItem(Id + 15, "JackalCanKillSidekick", false, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        var SKOpts = new BooleanOptionItem(Id + 17, "SidekickSettings", true, TabGroup.NeutralRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Jackal]);
        KillCooldownSK = new FloatOptionItem(Id + 20, "KillCooldown", new(0f, 180f, 2.5f), 20f, TabGroup.NeutralRoles)
            .SetParent(SKOpts)
            .SetValueFormat(OptionFormat.Seconds);
        CanVentSK = new BooleanOptionItem(Id + 21, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        CanSabotageSK = new BooleanOptionItem(Id + 22, "CanSabotage", true, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        SidekickCanKillJackal = new BooleanOptionItem(Id + 23, "SidekickCanKillJackal", false, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        SidekickCanKillSidekick = new BooleanOptionItem(Id + 24, "SidekickCanKillSidekick", false, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
        SidekickCountMode = new StringOptionItem(Id + 25, "SidekickCountMode", Options.SidekickCountMode, 0, TabGroup.NeutralRoles)
            .SetParent(SKOpts);
    }

    public override void Init()
    {
        PlayerIdList = [];
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        PlayerIdList.Add(playerId);
        playerId.SetAbilityUseLimit(1);

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static bool CanRecruit(byte id) => id.GetAbilityUseLimit() > 0;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
    public override bool CanUseSabotage(PlayerControl pc) => CanSabotage.GetBool() && pc.IsAlive();

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.KillButton.OverrideText(CanRecruit(id) ? $"{GetString("GangsterButtonText")}" : $"{GetString("KillButtonText")}");
        hud.SabotageButton.ToggleVisible(CanSabotage.GetBool());
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());

    public static void AfterPlayerDiedTask(PlayerControl target)
    {
        if (target.Is(CustomRoles.Jackal)) return;
        Main.AllAlivePlayerControls
            .Where(x => x.Is(CustomRoles.Jackal))
            .Do(x => x.SetKillCooldown(ResetKillCooldownOn.GetFloat()));
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer.GetAbilityUseLimit() < 1 || !CanBeSidekick(target, out var needBasisChange, out var targetRoleType)) return true;

        killer.RpcRemoveAbilityUse();
        target.RpcSetCustomRole(targetRoleType == RoleTypes.Shapeshifter ? CustomRoles.Recruit : CustomRoles.Sidekick);
        if (needBasisChange) target.ChangeRoleBasis(RoleTypes.Impostor);

        if (!Main.ResetCamPlayerList.Contains(target.PlayerId))
            Main.ResetCamPlayerList.Add(target.PlayerId);

        killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("GangsterSuccessfullyRecruited")));
        target.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Jackal), GetString("BeRecruitedByJackal")));

        Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target, ForceLoop: true);
        Utils.NotifyRoles(SpecifySeer: target, SpecifyTarget: killer, ForceLoop: true);

        killer.ResetKillCooldown();
        target.RpcGuardAndKill(killer);
        target.RpcGuardAndKill(target);

        Logger.Info("SetRole:" + target.Data?.PlayerName + " = " + target.GetCustomRole() + " + " + CustomRoles.Sidekick, "Assign " + CustomRoles.Sidekick);
        return false;
    }

    private static bool CanBeSidekick(PlayerControl pc, out bool needBasisChange, out RoleTypes targetRoleType)
    {
        targetRoleType = pc.GetRoleTypes();
        needBasisChange = targetRoleType is not RoleTypes.Impostor and not RoleTypes.Shapeshifter;
        return pc != null && !pc.Is(CustomRoles.Sidekick) && !pc.Is(CustomRoles.Recruit) && !pc.Is(CustomRoles.Loyal) && !pc.Is(CustomRoles.Rascal) && !pc.Is(CustomRoles.Madmate) && !pc.Is(CustomRoles.Charmed) && !pc.Is(CustomRoles.Contagious) && pc.GetCustomRole().IsAbleToBeSidekicked();
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (pc.IsAlive()) return;
        var sk = Main.AllPlayerControls.FirstOrDefault(x => x.Is(CustomRoles.Sidekick));
        if (sk == null) return;
        sk.RpcSetCustomRole(CustomRoles.Jackal);
        sk.SetAbilityUseLimit(0);
    }
}