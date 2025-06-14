﻿using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;

namespace EHR.Impostor;

public class Overheat : RoleBase
{
    private const int StartingTemperature = 35;
    public static bool On;

    private static OptionItem OverheatChanceIncrease;
    private static OptionItem OverheatChanceIncreaseFrequency;
    private static OptionItem OverheatRollChanceFrequency;
    private static OptionItem CoolDownTime;
    private static OptionItem KCDDecreasePerIncreasedTemperature;
    private float ChanceIncreaseTimer;
    private float RollChanceTimer;

    public int Temperature;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        const int id = 12332;
        Options.SetupRoleOptions(id, TabGroup.ImpostorRoles, CustomRoles.Overheat);

        OverheatChanceIncrease = new FloatOptionItem(id + 2, "Overheat.ChanceIncrease", new(1f, 10f, 1f), 1f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Overheat])
            .SetValueFormat(OptionFormat.Percent);

        OverheatChanceIncreaseFrequency = new FloatOptionItem(id + 3, "Overheat.ChanceIncreaseFrequency", new(0.5f, 30f, 0.5f), 10f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Overheat])
            .SetValueFormat(OptionFormat.Seconds);

        OverheatRollChanceFrequency = new FloatOptionItem(id + 4, "Overheat.RollChanceFrequency", new(0.5f, 60f, 0.5f), 8f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Overheat])
            .SetValueFormat(OptionFormat.Seconds);

        CoolDownTime = new FloatOptionItem(id + 5, "Overheat.CoolDownTime", new(0.5f, 30f, 0.5f), 4f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Overheat])
            .SetValueFormat(OptionFormat.Seconds);

        KCDDecreasePerIncreasedTemperature = new FloatOptionItem(id + 6, "Overheat.KCDDecreasePerIncreasedTemperature", new(0.5f, 15f, 0.5f), 3f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Overheat])
            .SetValueFormat(OptionFormat.Seconds);
    }

    private static Color GetTemperatureColor(int temperature)
    {
        return temperature switch
        {
            <= 35 => Color.blue,
            36 => Color.cyan,
            37 => Color.green,
            38 => Color.yellow,
            39 => Palette.Orange,
            >= 40 => Color.red
        };
    }

    public override void Add(byte playerId)
    {
        On = true;
        Temperature = StartingTemperature;
        ChanceIncreaseTimer = -8f;
        RollChanceTimer = -8f;
        SendRPC(playerId);
    }

    public override void Init()
    {
        On = false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePets.GetBool()) return;

        AURoleOptions.ShapeshifterCooldown = 1f;
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    private void SendRPC(byte id)
    {
        Utils.SendRPC(CustomRPC.SyncOverheat, id, Temperature);
    }

    public override void SetKillCooldown(byte id)
    {
        float kcd = Options.AdjustedDefaultKillCooldown;
        kcd -= KCDDecreasePerIncreasedTemperature.GetFloat() * (Temperature - StartingTemperature);
        Main.AllPlayerKillCooldown[id] = kcd;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!GameStates.IsInTask || !pc.IsAlive() || ExileController.Instance != null) return;

        ChanceIncreaseTimer += Time.fixedDeltaTime;
        RollChanceTimer += Time.fixedDeltaTime;

        if (ChanceIncreaseTimer >= OverheatChanceIncreaseFrequency.GetFloat())
        {
            ChanceIncreaseTimer = 0f;
            Temperature += OverheatChanceIncrease.GetInt();
            SendRPC(pc.PlayerId);
            pc.ResetKillCooldown();
            pc.MarkDirtySettings();
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        if (RollChanceTimer >= OverheatRollChanceFrequency.GetFloat())
        {
            RollChanceTimer = 0f;
            if (IRandom.Instance.Next(100) < Temperature - StartingTemperature) pc.Suicide();
        }
    }

    private void CoolDown(PlayerControl pc)
    {
        Temperature = StartingTemperature;
        SendRPC(pc.PlayerId);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);

        float speed = Main.AllPlayerSpeed[pc.PlayerId];
        Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
        pc.MarkDirtySettings();

        LateTask.New(() =>
        {
            Main.AllPlayerSpeed[pc.PlayerId] = speed;
            pc.MarkDirtySettings();
        }, CoolDownTime.GetFloat(), "Overheat Cool Down");
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        CoolDown(shapeshifter);
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        CoolDown(pc);
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (Main.PlayerStates[seer.PlayerId].Role is not Overheat oh) return string.Empty;
        if (seer.IsModdedClient() && !hud) return string.Empty;
        if (seer.PlayerId != target.PlayerId) return string.Empty;

        Color color = GetTemperatureColor(oh.Temperature);
        string str = Translator.GetString("Overheat.Suffix");
        return string.Format(str, Utils.ColorString(color, $"{oh.Temperature}°C"));
    }
}