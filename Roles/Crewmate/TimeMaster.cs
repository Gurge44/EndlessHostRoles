using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;
using static EHR.Options;

namespace EHR.Crewmate;

internal class TimeMaster : RoleBase
{
    public static bool On;
    
    public override bool IsEnable => On;

    public static OptionItem TimeMasterRewindTimeLength;
    public static OptionItem TimeMasterSkillCooldown;
    public static OptionItem TimeMasterSkillDuration;
    public static OptionItem TimeMasterMaxUses;
    public static OptionItem TimeMasterAbilityChargesWhenFinishedTasks;
    public static OptionItem TimeMasterAbilityUseGainWithEachTaskCompleted;

    private static Dictionary<long, Dictionary<byte, Vector2>> BackTrack = [];
    public static bool Rewinding;
    
    public override void SetupCustomOption()
    {
        SetupRoleOptions(8950, TabGroup.CrewmateRoles, CustomRoles.TimeMaster);
        
        TimeMasterRewindTimeLength = new IntegerOptionItem(8959, "TimeMasterRewindTimeLength", new(0, 10, 1), 15, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Seconds);

        TimeMasterSkillCooldown = new FloatOptionItem(8960, "TimeMasterSkillCooldown", new(0f, 180f, 1f), 20f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Seconds);

        TimeMasterSkillDuration = new FloatOptionItem(8961, "TimeMasterSkillDuration", new(0f, 180f, 1f), 15f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Seconds);

        TimeMasterMaxUses = new IntegerOptionItem(8962, "TimeMasterMaxUses", new(0, 180, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Times);

        TimeMasterAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(8963, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.4f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Times);

        TimeMasterAbilityChargesWhenFinishedTasks = new FloatOptionItem(8964, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Add(byte playerId)
    {
        On = true;
        BackTrack = [];
        playerId.SetAbilityUseLimit(TimeMasterMaxUses.GetInt());
    }

    public override void Init()
    {
        On = false;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (UsePets.GetBool()) return;

        AURoleOptions.EngineerCooldown = TimeMasterSkillCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        if (UsePets.GetBool())
            hud.PetButton.buttonLabelText.text = Translator.GetString("TimeMasterVentButtonText");
        else
            hud.AbilityButton.buttonLabelText.text = Translator.GetString("TimeMasterVentButtonText");
    }

    public override void OnPet(PlayerControl pc)
    {
        if (pc.GetAbilityUseLimit() < 1) return;
        pc.RpcRemoveAbilityUse();
        
        Main.Instance.StartCoroutine(Rewind());
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        pc.MyPhysics?.RpcBootFromVent(vent.Id);
        
        if (pc.GetAbilityUseLimit() < 1) return;
        pc.RpcRemoveAbilityUse();
        
        Main.Instance.StartCoroutine(Rewind());
    }

    private static System.Collections.IEnumerator Rewind()
    {
        try
        {
            Rewinding = true;
            
            const float delay = 0.3f;
            long now = Utils.TimeStamp;
            int length = TimeMasterRewindTimeLength.GetInt();
        
            Main.AllPlayerSpeed.SetAllValues(Main.MinSpeed);
            ReportDeadBodyPatch.CanReport.SetAllValues(false);

            string notify = Utils.ColorString(Color.yellow, string.Format(Translator.GetString("TimeMasterRewindStart"), CustomRoles.TimeMaster.ToColoredString()));

            foreach (PlayerControl player in Main.AllPlayerControls)
            {
                player.ReactorFlash(flashDuration: length * delay);
                player.Notify(notify, length * delay);
                player.MarkDirtySettings();
            }

            for (long i = now - 1; i >= now - length; i--)
            {
                if (!BackTrack.TryGetValue(i, out Dictionary<byte, Vector2> track)) continue;

                foreach ((byte playerId, Vector2 pos) in track)
                {
                    var player = playerId.GetPlayer();
                    if (player == null || !player.IsAlive()) continue;

                    player.TP(pos);
                }
            
                yield return new WaitForSeconds(delay);
            }

            foreach (DeadBody deadBody in Object.FindObjectsOfType<DeadBody>())
            {
                if (!Main.PlayerStates.TryGetValue(deadBody.ParentId, out var ps)) continue;
            
                if (ps.RealKiller.TimeStamp.AddSeconds(length) >= DateTime.Now)
                {
                    ps.Player.RpcRevive();
                    ps.Player.TP(deadBody.TruePosition);
                    ps.Player.Notify(Translator.GetString("RevivedByTimeMaster"), 15f);
                }
            }
        
            Main.AllPlayerSpeed.SetAllValues(Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
            ReportDeadBodyPatch.CanReport.SetAllValues(true);
            Utils.MarkEveryoneDirtySettings();
        }
        finally
        {
            Rewinding = false;
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (GameStates.IsMeeting || ExileController.Instance || !player.IsAlive()) return;

        long now = Utils.TimeStamp;
        if (BackTrack.ContainsKey(now)) return;

        BackTrack[now] = Main.AllAlivePlayerControls.ToDictionary(x => x.PlayerId, x => x.Pos());
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }
}