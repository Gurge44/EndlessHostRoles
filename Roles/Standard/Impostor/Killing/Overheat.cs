using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using UnityEngine;

namespace EHR.Roles;

public class Overheat : RoleBase
{
    private const int StartingTemperature = 35;
    public static bool On;

    private static OptionItem OverheatChanceIncrease;
    private static OptionItem OverheatChanceIncreaseFrequency;
    private static OptionItem OverheatRollChanceFrequency;
    private static OptionItem KCDDecreasePerIncreasedTemperature;

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

        OverheatRollChanceFrequency = new FloatOptionItem(id + 4, "Overheat.RollChanceFrequency", new(0.5f, 60f, 0.5f), 30f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Overheat])
            .SetValueFormat(OptionFormat.Seconds);

        KCDDecreasePerIncreasedTemperature = new FloatOptionItem(id + 6, "Overheat.KCDDecreasePerIncreasedTemperature", new(0.5f, 15f, 0.5f), 5f, TabGroup.ImpostorRoles)
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
        if (!AmongUsClient.Instance.AmHost) return;
        var chanceIncreaseTimer = new CountdownTimer(OverheatChanceIncreaseFrequency.GetFloat() + 8, ChanceIncreaseElapsed, cancelOnMeeting: false);
        var rollChanceTimer = new CountdownTimer(OverheatRollChanceFrequency.GetFloat() + 8, RollElapsed, cancelOnMeeting: false);
        return;

        void ChanceIncreaseElapsed()
        {
            chanceIncreaseTimer = new CountdownTimer(OverheatChanceIncreaseFrequency.GetFloat(), ChanceIncreaseElapsed, cancelOnMeeting: false);
            if (GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks) return;

            var pc = playerId.GetPlayer();

            if (pc == null || !pc.IsAlive())
            {
                chanceIncreaseTimer?.Dispose();
                chanceIncreaseTimer = null;
                return;
            }
            
            Temperature += OverheatChanceIncrease.GetInt();
            SendRPC(playerId);
            pc.ResetKillCooldown();
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        void RollElapsed()
        {
            rollChanceTimer = new CountdownTimer(OverheatRollChanceFrequency.GetFloat(), RollElapsed, cancelOnMeeting: false);
            if (GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks) return;

            var pc = playerId.GetPlayer();

            if (pc == null || !pc.IsAlive())
            {
                rollChanceTimer?.Dispose();
                rollChanceTimer = null;
                return;
            }
            
            if (IRandom.Instance.Next(100) < Temperature - StartingTemperature)
                pc.Suicide();
        }
    }

    public override void Init()
    {
        On = false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePhantomBasis.GetBool())
            AURoleOptions.PhantomCooldown = 1f;
        else
        {
            if (Options.UsePets.GetBool()) return;

            AURoleOptions.ShapeshifterCooldown = 1f;
            AURoleOptions.ShapeshifterDuration = 1f;
        }
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

    private void CoolDown(PlayerControl pc)
    {
        Temperature = StartingTemperature;
        SendRPC(pc.PlayerId);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        pc.ResetKillCooldown();
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

    public override bool OnVanish(PlayerControl pc)
    {
        CoolDown(pc);
        return false;
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (Main.PlayerStates[seer.PlayerId].Role is not Overheat oh || seer.IsModdedClient() && !hud || seer.PlayerId != target.PlayerId) return string.Empty;

        Color color = GetTemperatureColor(oh.Temperature);
        string str = Translator.GetString("Overheat.Suffix");
        return string.Format(str, Utils.ColorString(color, $"{oh.Temperature}°C"));
    }
}