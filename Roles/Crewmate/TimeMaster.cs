using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Options;

namespace EHR.Crewmate;

internal class TimeMaster : RoleBase
{
    public static bool On;

    public static OptionItem TimeMasterCanUseVitals;
    public static OptionItem TimeMasterRewindTimeLength;
    public static OptionItem TimeMasterSkillCooldown;
    public static OptionItem TimeMasterMaxUses;
    public static OptionItem TimeMasterAbilityChargesWhenFinishedTasks;
    public static OptionItem TimeMasterAbilityUseGainWithEachTaskCompleted;

    private static Dictionary<long, Dictionary<byte, Vector2>> BackTrack = [];
    public static bool Rewinding;

    public override bool IsEnable => On;

    private bool DesyncCommsActive;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(652100, TabGroup.CrewmateRoles, CustomRoles.TimeMaster);

        TimeMasterCanUseVitals = new BooleanOptionItem(652109, "TimeMasterCanUseVitals", true, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster]);

        TimeMasterRewindTimeLength = new IntegerOptionItem(652110, "TimeMasterRewindTimeLength", new(0, 30, 1), 15, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Seconds);

        TimeMasterSkillCooldown = new FloatOptionItem(652111, "AbilityCooldown", new(0f, 180f, 1f), 20f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Seconds);

        TimeMasterMaxUses = new IntegerOptionItem(652112, "TimeMasterMaxUses", new(0, 180, 1), 1, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Times);

        TimeMasterAbilityUseGainWithEachTaskCompleted = new FloatOptionItem(652113, "AbilityUseGainWithEachTaskCompleted", new(0f, 5f, 0.05f), 0.4f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Times);

        TimeMasterAbilityChargesWhenFinishedTasks = new FloatOptionItem(652114, "AbilityChargesWhenFinishedTasks", new(0f, 5f, 0.05f), 0.2f, TabGroup.CrewmateRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.TimeMaster])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Add(byte playerId)
    {
        On = true;
        BackTrack = [];
        DesyncCommsActive = false;
        playerId.SetAbilityUseLimit(TimeMasterMaxUses.GetFloat());
    }

    public override void Init()
    {
        On = false;
        Rewinding = false;
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
        
        if (pc.AmOwner)
            Achievements.Type.APerfectTimeToRewindIt.Complete();
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        if (UsePets.GetBool()) return;
        if (pc.GetAbilityUseLimit() < 1) return;
        pc.RpcRemoveAbilityUse();

        Main.Instance.StartCoroutine(Rewind());
        
        if (pc.AmOwner)
            Achievements.Type.APerfectTimeToRewindIt.Complete();
    }

    private static IEnumerator Rewind()
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
                if (player.inVent || player.MyPhysics?.Animations?.IsPlayingEnterVentAnimation() == true) player.MyPhysics?.RpcExitVent(player.GetClosestVent().Id);
                player.ReactorFlash(flashDuration: length * delay + 0.55f);
                player.Notify(notify, Math.Max((length * delay) + 0.55f, 4f));
                player.MarkDirtySettings();
            }

            yield return new WaitForSeconds(0.55f);

            for (long i = now - 1; i >= now - length; i--)
            {
                if (!BackTrack.TryGetValue(i, out Dictionary<byte, Vector2> track)) continue;

                foreach ((byte playerId, Vector2 pos) in track)
                {
                    PlayerControl player = playerId.GetPlayer();
                    if (player == null || !player.IsAlive()) continue;

                    player.TP(pos);
                }

                yield return new WaitForSeconds(delay);
            }

            foreach (DeadBody deadBody in Object.FindObjectsOfType<DeadBody>())
            {
                if (!Main.PlayerStates.TryGetValue(deadBody.ParentId, out PlayerState ps)) continue;

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
        finally { Rewinding = false; }
    }

    public override void AfterMeetingTasks()
    {
        Rewinding = false;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (GameStates.IsMeeting || ExileController.Instance || !player.IsAlive()) return;

        long now = Utils.TimeStamp;
        if (BackTrack.ContainsKey(now)) return;

        BackTrack[now] = Main.AllAlivePlayerControls.Where(x => !x.inVent && !x.onLadder && !x.inMovingPlat).ToDictionary(x => x.PlayerId, x => x.Pos());

        if (TimeMasterCanUseVitals.GetBool()) return;

        var doComms = false;
        bool commsSaboActive = Utils.IsActive(SystemTypes.Comms);
        float usableDistance = DisableDevice.UsableDistance + 2f;
        Vector2 pos = player.Pos();

        if (!commsSaboActive)
        {
            switch (Main.NormalOptions.MapId)
            {
                case 2:
                    doComms |= Vector2.Distance(pos, DisableDevice.DevicePos["PolusVital"]) <= usableDistance;
                    break;
                case 4:
                    doComms |= Vector2.Distance(pos, DisableDevice.DevicePos["AirshipVital"]) <= usableDistance;
                    break;
                case 5:
                    doComms |= Vector2.Distance(pos, DisableDevice.DevicePos["FungleVital"]) <= usableDistance;
                    break;
            }
        }

        var sender = CustomRpcSender.Create("DisableDevice.FixedUpdate", SendOption.Reliable, log: false);
        var hasValue = false;

        if (doComms && !player.inVent && !DisableDevice.DesyncComms.Contains(player.PlayerId))
        {
            DesyncCommsActive = true;
            sender.RpcDesyncRepairSystem(player, SystemTypes.Comms, 128);
            hasValue = true;
        }
        else if (!commsSaboActive && DesyncCommsActive)
        {
            DesyncCommsActive = false;
            sender.RpcDesyncRepairSystem(player, SystemTypes.Comms, 16);

            if (Main.NormalOptions.MapId is 1 or 5) // Mira HQ or The Fungle
                sender.RpcDesyncRepairSystem(player, SystemTypes.Comms, 17);

            hasValue = true;
        }

        sender.SendMessage(!hasValue);
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = false;
        countsAs = 1;
        int length = TimeMasterRewindTimeLength.GetInt();

        foreach (DeadBody deadBody in Object.FindObjectsOfType<DeadBody>())
        {
            if (!Main.PlayerStates.TryGetValue(deadBody.ParentId, out PlayerState ps)) continue;

            if (ps.RealKiller.TimeStamp.AddSeconds(length) >= DateTime.Now)
            {
                keepGameGoing = true;
                return;
            }
        }
    }
}