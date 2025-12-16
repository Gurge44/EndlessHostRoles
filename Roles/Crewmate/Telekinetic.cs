using System;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using EHR.Patches;
using Hazel;

namespace EHR.Crewmate;

public class Telekinetic : RoleBase
{
    public static bool On;

    private static OptionItem FreezeDuration;
    private static OptionItem ShieldDuration;
    private static OptionItem SpeedDuration;
    private static OptionItem IncreasedSpeed;

    private Mode CurrentMode;
    private long LastUpdate;
    private bool Shielded;
    private PlayerControl TelekineticPC;
    private int Timer;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        const int id = 649350;
        const TabGroup tab = TabGroup.CrewmateRoles;
        const CustomRoles role = CustomRoles.Telekinetic;

        Options.SetupRoleOptions(id, tab, role);

        FreezeDuration = new IntegerOptionItem(id + 2, "Telekinetic.FreezeDuration", new(0, 60, 1), 10, tab)
            .SetParent(Options.CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Seconds);

        ShieldDuration = new IntegerOptionItem(id + 3, "Telekinetic.ShieldDuration", new(0, 60, 1), 20, tab)
            .SetParent(Options.CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Seconds);

        SpeedDuration = new IntegerOptionItem(id + 4, "Telekinetic.SpeedDuration", new(0, 60, 1), 15, tab)
            .SetParent(Options.CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Seconds);

        IncreasedSpeed = new FloatOptionItem(id + 5, "Telekinetic.IncreasedSpeed", new(0.05f, 5f, 0.05f), 2f, tab)
            .SetParent(Options.CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Multiplier);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        TelekineticPC = Utils.GetPlayerById(playerId);
        CurrentMode = default(Mode);
        Timer = 40;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.EngineerCooldown = 0f;
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (!pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance || Main.HasJustStarted) return;

        long now = Utils.TimeStamp;
        if (now == LastUpdate) return;

        LastUpdate = now;

        switch (Timer)
        {
            case > 60:
                pc.Suicide();
                break;
            case > 0:
                Timer--;
                SendRPC();
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
                break;
        }
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        CurrentMode = (Mode)(((int)CurrentMode + 1) % Enum.GetValues(typeof(Mode)).Length);
        SendRPC();
    }

    public override void OnPet(PlayerControl pc)
    {
        PlayerControl target = ExternalRpcPetPatch.SelectKillButtonTarget(pc);
        bool hasTarget = target != null;

        switch (CurrentMode)
        {
            case Mode.TeleportTarget when hasTarget:
                target.TP(Pelican.GetBlackRoomPS());
                Timer += 40;
                Freeze();
                break;
            case Mode.TeleportSelf:
                pc.TP(Pelican.GetBlackRoomPS());
                Timer += 30;
                Freeze();
                break;
            case Mode.Freeze when hasTarget:
                float speed = Main.AllPlayerSpeed[target.PlayerId];
                Main.AllPlayerSpeed[target.PlayerId] = Main.MinSpeed;
                target.MarkDirtySettings();

                LateTask.New(() =>
                {
                    Main.AllPlayerSpeed[target.PlayerId] = speed;
                    target.MarkDirtySettings();
                }, FreezeDuration.GetFloat(), "Telekinetic.Freeze");

                Timer += 35;

                if (target.AmOwner)
                    Achievements.Type.TooCold.CompleteAfterGameEnd();

                break;
            case Mode.Kill when hasTarget:
                pc.RpcCheckAndMurder(target);
                Timer += 60;
                Freeze();
                break;
            case Mode.Shield:
                Shielded = true;
                LateTask.New(() => Shielded = false, ShieldDuration.GetFloat(), "Telekinetic.Shield");
                Timer += 45;
                Freeze();
                break;
            case Mode.Speed:
                float selfSpeed = Main.AllPlayerSpeed[pc.PlayerId];
                Main.AllPlayerSpeed[pc.PlayerId] = IncreasedSpeed.GetFloat();
                pc.MarkDirtySettings();

                LateTask.New(() =>
                {
                    Main.AllPlayerSpeed[pc.PlayerId] = selfSpeed;
                    pc.MarkDirtySettings();
                }, SpeedDuration.GetFloat(), "Telekinetic.Speed");

                Timer += 30;
                break;
            case Mode.Doors:
                DoorsReset.OpenAllDoors();
                Timer += 35;
                Freeze();
                break;
        }

        SendRPC();
        return;

        void Freeze()
        {
            float speed = Main.AllPlayerSpeed[pc.PlayerId];
            Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
            pc.MarkDirtySettings();

            LateTask.New(() =>
            {
                Main.AllPlayerSpeed[pc.PlayerId] = speed;
                pc.MarkDirtySettings();
            }, 5f, "Telekinetic.SelfFreeze");
        }
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        return !Shielded;
    }

    public override void AfterMeetingTasks()
    {
        Timer = 40;
        SendRPC();
    }

    private void SendRPC()
    {
        Utils.SendRPC(CustomRPC.SyncRoleData, TelekineticPC.PlayerId, Timer, (int)CurrentMode);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Timer = reader.ReadPackedInt32();
        CurrentMode = (Mode)reader.ReadPackedInt32();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != TelekineticPC.PlayerId || (seer.IsModdedClient() && !hud) || meeting) return string.Empty;
        return string.Format(Translator.GetString("Telekinetic.Suffix"), Translator.GetString($"Telekinetic.Mode.{CurrentMode}"));
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return $"<#ffffff>{Timer}</color>{base.GetProgressText(playerId, comms)}";
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }

    private enum Mode
    {
        TeleportTarget,
        TeleportSelf,
        Freeze,
        Kill,
        Shield,
        Speed,
        Doors
    }

    public override void ManipulateGameEndCheckCrew(PlayerState playerState, out bool keepGameGoing, out int countsAs)
    {
        if (playerState.IsDead)
        {
            base.ManipulateGameEndCheckCrew(playerState, out keepGameGoing, out countsAs);
            return;
        }

        keepGameGoing = true;
        countsAs = 1;
    }
}