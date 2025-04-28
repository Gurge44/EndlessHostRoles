using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using UnityEngine;
using static EHR.Options;

namespace EHR.Crewmate;

internal class TimeMaster : RoleBase
{
    public static bool On;

    public static OptionItem TimeMasterRewindTimeLength;
    public static OptionItem TimeMasterSkillCooldown;
    public static OptionItem TimeMasterMaxUses;
    public static OptionItem TimeMasterAbilityChargesWhenFinishedTasks;
    public static OptionItem TimeMasterAbilityUseGainWithEachTaskCompleted;

    private static Dictionary<long, Dictionary<byte, Vector2>> BackTrack = [];
    public static bool Rewinding;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(652100, TabGroup.CrewmateRoles, CustomRoles.TimeMaster);

        TimeMasterRewindTimeLength = new IntegerOptionItem(652110, "TimeMasterRewindTimeLength", new(0, 10, 1), 15, TabGroup.CrewmateRoles)
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
        playerId.SetAbilityUseLimit(TimeMasterMaxUses.GetInt());
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
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        pc.MyPhysics?.RpcExitVent(vent.Id);

        if (pc.GetAbilityUseLimit() < 1) return;
        pc.RpcRemoveAbilityUse();

        Main.Instance.StartCoroutine(Rewind());
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
            var sender = CustomRpcSender.Create("TimeMaster.Rewind", SendOption.Reliable);
            var hasValue = false;

            foreach (PlayerControl player in Main.AllPlayerControls)
            {
                player.ReactorFlash(flashDuration: length * delay);
                hasValue |= sender.Notify(player, notify, Math.Max(length * delay, 4f));
                player.MarkDirtySettings();

                if (sender.stream.Length > 800)
                {
                    sender.SendMessage();
                    sender = CustomRpcSender.Create("TimeMaster.Rewind", SendOption.Reliable);
                    hasValue = false;
                }
            }

            sender.SendMessage(dispose: !hasValue);

            for (long i = now - 1; i >= now - length; i--)
            {
                if (!BackTrack.TryGetValue(i, out Dictionary<byte, Vector2> track)) continue;

                sender = CustomRpcSender.Create("TimeMaster.Rewind - 2", SendOption.Reliable);
                hasValue = false;

                foreach ((byte playerId, Vector2 pos) in track)
                {
                    PlayerControl player = playerId.GetPlayer();
                    if (player == null || !player.IsAlive()) continue;

                    hasValue |= sender.TP(player, pos);
                }

                sender.SendMessage(dispose: !hasValue);

                yield return new WaitForSeconds(delay);
            }

            sender = CustomRpcSender.Create("TimeMaster.Rewind - 3", SendOption.Reliable);
            hasValue = false;

            foreach (DeadBody deadBody in Object.FindObjectsOfType<DeadBody>())
            {
                if (!Main.PlayerStates.TryGetValue(deadBody.ParentId, out PlayerState ps)) continue;

                if (ps.RealKiller.TimeStamp.AddSeconds(length) >= DateTime.Now)
                {
                    ps.Player.RpcRevive();
                    hasValue |= sender.TP(ps.Player, deadBody.TruePosition);
                    hasValue |= sender.Notify(ps.Player, Translator.GetString("RevivedByTimeMaster"), 15f);

                    if (sender.stream.Length > 800)
                    {
                        sender.SendMessage();
                        sender = CustomRpcSender.Create("TimeMaster.Rewind - 3", SendOption.Reliable);
                        hasValue = false;
                    }
                }
            }

            sender.SendMessage(dispose: !hasValue);

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

        BackTrack[now] = Main.AllAlivePlayerControls.ToDictionary(x => x.PlayerId, x => x.Pos());
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }
}