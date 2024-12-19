﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using UnityEngine;

namespace EHR.Impostor;

public class Stasis : RoleBase
{
    public static bool On;
    private static List<Stasis> Instances = [];

    public static OptionItem AbilityCooldown;
    public static OptionItem AbilityDuration;
    private static OptionItem AffectsOtherImpostors;
    private static OptionItem CanVent;
    private static OptionItem KillCooldown;

    private bool UsingAbility;

    public override bool IsEnable => On;

    public static bool IsTimeFrozen => Instances.Any(x => x.UsingAbility);

    public override void SetupCustomOption()
    {
        StartSetup(649025, true)
            .AutoSetupOption(ref AbilityCooldown, 30, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 10, new IntegerValueRule(1, 60, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AffectsOtherImpostors, true)
            .AutoSetupOption(ref CanVent, false)
            .AutoSetupOption(ref KillCooldown, 30f, new FloatValueRule(0f, 180f, 1f), OptionFormat.Seconds);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Instances.Add(this);
        UsingAbility = false;
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePets.GetBool()) return;

        AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetInt();
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting && !Options.UseUnshiftTrigger.GetBool()) return true;

        OnPet(shapeshifter);
        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        OnPet(pc);
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        UsingAbility = true;

        ReportDeadBodyPatch.CanReport.SetAllValues(false);
        Main.AllPlayerSpeed.SetAllValues(Main.MinSpeed);
        Main.PlayerStates.Values.DoIf(x => !x.IsDead, x => x.IsBlackOut = true);

        int time = AbilityDuration.GetInt();

        foreach (PlayerControl player in Main.AllPlayerControls)
        {
            if (!player.IsAlive() || player.PlayerId == pc.PlayerId || (player.Is(Team.Impostor) && !AffectsOtherImpostors.GetBool()))
            {
                ReportDeadBodyPatch.CanReport[player.PlayerId] = true;
                Main.AllPlayerSpeed[player.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                Main.PlayerStates[player.PlayerId].IsBlackOut = false;
            }
            else
            {
                if (Main.KillTimers[player.PlayerId] < time) player.SetKillCooldown(time);
                player.RpcResetAbilityCooldown();

                if (!player.HasAbilityCD() && player.GetCustomRole().PetActivatedAbility())
                    player.AddAbilityCD(time);
            }
        }

        Utils.SyncAllSettings();
        Main.Instance.StartCoroutine(Countdown());
        return;

        IEnumerator Countdown()
        {
            var imps = AffectsOtherImpostors.GetBool() ? [pc] : Main.AllAlivePlayerControls.Where(x => x.Is(Team.Impostor)).ToArray();

            for (int i = 0; i < time; i++)
            {
                // ReSharper disable once AccessToModifiedClosure
                imps.Do(x => x.Notify($"<#00ffa5>{Translator.GetString("Stasis.TimeFrozenNotify")}</color> <#888888>-</color> {time - i}", overrideAll: true));
                yield return new WaitForSeconds(1f);
            }

            UsingAbility = false;
            imps.Do(x => x.Notify(Translator.GetString("Stasis.TimeFreezeEndNotify"), overrideAll: true));

            ReportDeadBodyPatch.CanReport.SetAllValues(true);
            Main.AllPlayerSpeed.SetAllValues(Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
            Main.PlayerStates.Values.Do(x => x.IsBlackOut = false);
            Utils.SyncAllSettings();

            pc.RpcResetAbilityCooldown();
        }
    }
}