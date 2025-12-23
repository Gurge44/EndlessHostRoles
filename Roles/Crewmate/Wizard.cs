using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Crewmate;

public class Wizard : RoleBase
{
    public static bool On;
    private static List<Wizard> Instances = [];

    private static OptionItem Vision;
    private static OptionItem MinSpeedValue;
    private static OptionItem MaxSpeedValue;
    private static OptionItem MinVisionValue;
    private static OptionItem MaxVisionValue;
    private static OptionItem MinKCDValue;
    private static OptionItem MaxKCDValue;
    private static OptionItem SpeedStep;
    private static OptionItem VisionStep;
    private static OptionItem KCDStep;
    private static OptionItem AbilityCooldown;
    private static OptionItem AbilityUseLimit;
    public static OptionItem AbilityUseGainWithEachTaskCompleted;
    public static OptionItem AbilityChargesWhenFinishedTasks;

    private static readonly Dictionary<Buff, float> MaxBuffValues = new()
    {
        [Buff.Speed] = 3f,
        [Buff.Vision] = 1.2f,
        [Buff.KCD] = 40f
    };

    private static readonly Dictionary<Buff, float> MinBuffValues = new()
    {
        [Buff.Speed] = 0.6f,
        [Buff.Vision] = 0.3f,
        [Buff.KCD] = 5f
    };

    private static readonly Dictionary<Buff, float> Steps = new()
    {
        [Buff.Speed] = 0.3f,
        [Buff.Vision] = 0.15f,
        [Buff.KCD] = 5f
    };

    private Dictionary<Buff, float> BuffValues;
    private int Count;
    private Dictionary<byte, Dictionary<Buff, float>> PlayerBuffs;
    private Buff SelectedBuff;

    private bool TaskMode;

    private byte WizardId;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(648250)
            .AutoSetupOption(ref Vision, 0.5f, new FloatValueRule(0f, 1.3f, 0.1f), OptionFormat.Multiplier)
            .AutoSetupOption(ref MinSpeedValue, 0.6f, new FloatValueRule(0.05f, 3f, 0.05f), OptionFormat.Multiplier)
            .AutoSetupOption(ref MaxSpeedValue, 3f, new FloatValueRule(0.05f, 3f, 0.05f), OptionFormat.Multiplier)
            .AutoSetupOption(ref MinVisionValue, 0.3f, new FloatValueRule(0f, 1.35f, 0.05f), OptionFormat.Multiplier)
            .AutoSetupOption(ref MaxVisionValue, 1.2f, new FloatValueRule(0f, 1.35f, 0.05f), OptionFormat.Multiplier)
            .AutoSetupOption(ref MinKCDValue, 5f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref MaxKCDValue, 40f, new FloatValueRule(0f, 60f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref SpeedStep, 0.3f, new FloatValueRule(0.05f, 1f, 0.05f), OptionFormat.Multiplier)
            .AutoSetupOption(ref VisionStep, 0.15f, new FloatValueRule(0.05f, 0.5f, 0.05f), OptionFormat.Multiplier)
            .AutoSetupOption(ref KCDStep, 5f, new FloatValueRule(0.5f, 10f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityCooldown, 15, new IntegerValueRule(0, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityUseLimit, 1f, new FloatValueRule(0, 20, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityUseGainWithEachTaskCompleted, 0.3f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .AutoSetupOption(ref AbilityChargesWhenFinishedTasks, 0.2f, new FloatValueRule(0f, 5f, 0.05f), OptionFormat.Times)
            .CreateOverrideTasksData();
    }

    public override void Init()
    {
        On = false;
        Instances = [];

        MaxBuffValues[Buff.Speed] = MaxSpeedValue.GetFloat();
        MaxBuffValues[Buff.Vision] = MaxVisionValue.GetFloat();
        MaxBuffValues[Buff.KCD] = MaxKCDValue.GetFloat();

        MinBuffValues[Buff.Speed] = MinSpeedValue.GetFloat();
        MinBuffValues[Buff.Vision] = MinVisionValue.GetFloat();
        MinBuffValues[Buff.KCD] = MinKCDValue.GetFloat();

        Steps[Buff.Speed] = SpeedStep.GetFloat();
        Steps[Buff.Vision] = VisionStep.GetFloat();
        Steps[Buff.KCD] = KCDStep.GetFloat();
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        WizardId = playerId;

        BuffValues = new()
        {
            [Buff.Speed] = 1.5f,
            [Buff.Vision] = 0.9f,
            [Buff.KCD] = 15f
        };

        PlayerBuffs = [];
        SelectedBuff = default(Buff);
        TaskMode = false;
        Count = 0;
        playerId.SetAbilityUseLimit(AbilityUseLimit.GetFloat());
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive() && !TaskMode;
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = AbilityCooldown.GetInt();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = 1f;
        AURoleOptions.ShapeshifterDuration = 1f;

        float vision = Vision.GetFloat();

        opt.SetVision(false);
        opt.SetFloat(FloatOptionNames.CrewLightMod, vision);
        opt.SetFloat(FloatOptionNames.ImpostorLightMod, vision);
    }

    public static void OnAnyoneApplyGameOptions(IGameOptions opt, byte id)
    {
        foreach (Wizard instance in Instances)
        {
            if (!instance.PlayerBuffs.TryGetValue(id, out Dictionary<Buff, float> buffs)) continue;

            foreach ((Buff buff, float value) in buffs)
            {
                switch (buff)
                {
                    case Buff.Speed:
                    {
                        Main.AllPlayerSpeed[id] = value;
                        break;
                    }
                    case Buff.Vision:
                    {
                        opt.SetFloat(FloatOptionNames.CrewLightMod, value);
                        opt.SetFloat(FloatOptionNames.ImpostorLightMod, value);
                        break;
                    }
                    case Buff.KCD:
                    {
                        Main.AllPlayerKillCooldown[id] = value;
                        break;
                    }
                }
            }
        }
    }

    public override void OnPet(PlayerControl pc)
    {
        if (SelectedBuff == Buff.KCD)
            SelectedBuff = default(Buff);
        else
            SelectedBuff++;

        Utils.SendRPC(CustomRPC.SyncRoleData, WizardId, 1, (int)SelectedBuff);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        BuffValues[SelectedBuff] += Steps[SelectedBuff];

        if (BuffValues[SelectedBuff] > MaxBuffValues[SelectedBuff])
            BuffValues[SelectedBuff] = MinBuffValues[SelectedBuff];
        else
            BuffValues[SelectedBuff] = (float)Math.Round(BuffValues[SelectedBuff], 1);

        Utils.SendRPC(CustomRPC.SyncRoleData, WizardId, 2, BuffValues[SelectedBuff]);
        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        return false;
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!base.OnCheckMurder(killer, target) || killer.GetAbilityUseLimit() < 1f) return false;

        float value = BuffValues[SelectedBuff];
        if (SelectedBuff == Buff.Speed) value = Math.Max(value, Main.MinSpeed);

        if (!PlayerBuffs.TryGetValue(target.PlayerId, out Dictionary<Buff, float> buffs))
            PlayerBuffs[target.PlayerId] = new() { { SelectedBuff, value } };
        else
            buffs[SelectedBuff] = value;

        var sender = CustomRpcSender.Create("Wizard", SendOption.Reliable);
        var hasValue = false;

        hasValue |= sender.SyncSettings(target);
        hasValue |= sender.SetKillCooldown(killer);
        killer.RpcRemoveAbilityUse();

        hasValue |= sender.Notify(killer, string.Format(Translator.GetString("Wizard.BuffGivenNotify"), target.PlayerId.ColoredPlayerName(), Translator.GetString($"Wizard.Buff.{SelectedBuff}"), Math.Round(value, 1)));
        Utils.SendRPC(CustomRPC.SyncRoleData, 3, target.PlayerId);
        hasValue |= sender.NotifyRolesSpecific(killer, target, out sender, out bool cleared);
        if (cleared) hasValue = false;

        sender.SendMessage(!hasValue);
        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!Main.IntroDestroyed || !GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;

        if (Count++ < 40) return;

        Count = 0;

        switch (TaskMode)
        {
            case true when (pc.GetAbilityUseLimit() >= 1 || pc.GetTaskState().IsTaskFinished) && pc.IsAlive():
                pc.RpcChangeRoleBasis(CustomRoles.Wizard);
                TaskMode = false;
                Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 4, TaskMode);
                break;
            case false when !pc.IsAlive():
                pc.RpcSetRoleGlobal(RoleTypes.CrewmateGhost);
                TaskMode = true;
                Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 4, TaskMode);
                break;
            case false when pc.GetAbilityUseLimit() < 1 && pc.IsAlive():
                pc.RpcSetRoleGlobal(RoleTypes.Crewmate, setRoleMap: true);
                pc.Notify(Translator.GetString("OutOfAbilityUsesDoMoreTasks"));
                TaskMode = true;
                Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 4, TaskMode);
                break;
        }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                SelectedBuff = (Buff)reader.ReadPackedInt32();
                break;
            case 2:
                BuffValues[SelectedBuff] = reader.ReadSingle();
                break;
            case 3:
                byte id = reader.ReadByte();
                float value = BuffValues[SelectedBuff];
                if (SelectedBuff == Buff.Speed) value = Math.Max(value, Main.MinSpeed);

                if (!PlayerBuffs.TryGetValue(id, out Dictionary<Buff, float> buffs))
                    PlayerBuffs[id] = new() { { SelectedBuff, value } };
                else
                    buffs[SelectedBuff] = value;

                break;
            case 4:
                TaskMode = reader.ReadBoolean();
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != WizardId || meeting) return string.Empty;

        if (PlayerBuffs.TryGetValue(target.PlayerId, out Dictionary<Buff, float> buffs)) return string.Join('\n', buffs.Select(x => $"{Translator.GetString($"Wizard.Buff.{x.Key}")}: {Math.Round(x.Value, 1):N1}{GetBuffFormat(x.Key)}"));

        if (seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud)) return string.Empty;

        return string.Format(Translator.GetString("Wizard.SelectedBuff"), SelectedBuff, BuffValues[SelectedBuff], GetBuffFormat(SelectedBuff));

        string GetBuffFormat(Buff buff) =>
            buff switch
            {
                Buff.Speed => "x",
                Buff.Vision => "x",
                Buff.KCD => "s",
                _ => throw new ArgumentOutOfRangeException(nameof(buff), buff, "Invalid buff")
            };
    }

    private enum Buff
    {
        Speed,
        Vision,
        KCD
    }
}