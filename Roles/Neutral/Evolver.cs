using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral
{
    public class Evolver : RoleBase
    {
        private const int Id = 644500;
        public static bool On;

        static OptionItem KillCooldown;
        private int ChooseTimer;
        private PlayerControl EvolverPC;

        private long LastUpdate = Utils.TimeStamp;
        int SelectedUpgradeIndex;

        private (float KillCooldown, bool ImpostorVision, float Vision, float Speed, int KillDistance, bool CanVent, int VentUseLimit, bool CanSabotage, int SabotageUseLimit, bool Shielded) Stats;
        List<Upgrade> Upgrades;

        public override bool IsEnable => On;
        private (float MinKillCooldown, float MaxVision, float MaxSpeed, int MaxKillDistance) Limits => (1f, Stats.ImpostorVision ? 1.5f : 5f, 3f, 2);

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Evolver);
            KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Evolver])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;

            EvolverPC = Utils.GetPlayerById(playerId);
            ChooseTimer = 0;
            Upgrades = [];
            SelectedUpgradeIndex = -1;

            var opts = Main.RealOptionsData;
            Stats = (
                KillCooldown.GetFloat(),
                false,
                opts.GetFloat(FloatOptionNames.CrewLightMod),
                opts.GetFloat(FloatOptionNames.PlayerSpeedMod),
                Math.Clamp(opts.GetInt(Int32OptionNames.KillDistance), 0, 2),
                false, 0, false, 0, false
            );

            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = Stats.KillCooldown;
        public override bool CanUseImpostorVentButton(PlayerControl pc) => pc.inVent || Stats is { CanVent: true, VentUseLimit: > 0 };
        public override bool CanUseSabotage(PlayerControl pc) => Stats is { CanSabotage: true, SabotageUseLimit: > 0 };

        public override void ApplyGameOptions(IGameOptions opt, byte playerId)
        {
            opt.SetFloat(FloatOptionNames.CrewLightMod, Stats.Vision);
            opt.SetFloat(FloatOptionNames.ImpostorLightMod, Stats.Vision);
            opt.SetVision(Stats.ImpostorVision);
            Main.AllPlayerSpeed[playerId] = Stats.Speed;
            opt.SetInt(Int32OptionNames.KillDistance, Stats.KillDistance);
        }

        public override void OnCoEnterVent(PlayerPhysics physics, int ventId)
        {
            Stats.VentUseLimit--;
            EnsureStatLimits();
        }

        public override bool OnSabotage(PlayerControl pc)
        {
            Stats.SabotageUseLimit--;
            EnsureStatLimits();
            return true;
        }

        public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
        {
            return !Stats.Shielded;
        }

        public override void OnReportDeadBody()
        {
            Stats.Shielded = false;

            if (ChooseTimer > 0)
            {
                ApplySelectedUpgradeAndReset();
                ChooseTimer = 0;
            }
        }

        public override void AfterMeetingTasks()
        {
            if (!EvolverPC.IsAlive()) return;

            Upgrades = Enum.GetValues<Upgrade>().Except(GetBannedUpgradeList()).Shuffle().GetRange(0, 3);
            SelectedUpgradeIndex = 0;
            ChooseTimer = 15;

            SendRPC();
            Utils.SendRPC(CustomRPC.SyncEvolver, EvolverPC.PlayerId, 2, SelectedUpgradeIndex);
            Utils.SendRPC(CustomRPC.SyncEvolver, EvolverPC.PlayerId, 3, ChooseTimer);
        }

        IEnumerable<Upgrade> GetBannedUpgradeList()
        {
            var banned = new List<Upgrade>();
            if (Stats.KillCooldown <= Limits.MinKillCooldown) banned.Add(Upgrade.DecreaseKillCooldown);
            if (Stats.ImpostorVision) banned.Add(Upgrade.GainImpostorVision);
            if (Stats.Vision >= Limits.MaxVision) banned.Add(Upgrade.IncreaseVision);
            if (Stats.Speed >= Limits.MaxSpeed) banned.Add(Upgrade.IncreaseSpeed);
            if (Stats.KillDistance >= Limits.MaxKillDistance) banned.Add(Upgrade.IncreaseKillDistance);
            banned.Add(Stats.CanVent ? Upgrade.GainVent : Upgrade.IncreaseVentUseLimit);
            banned.Add(Stats.CanSabotage ? Upgrade.GainSabotage : Upgrade.IncreaseSabotageUseLimit);
            return banned;
        }

        public override void OnFixedUpdate(PlayerControl pc)
        {
            if (!GameStates.IsInTask || !pc.IsAlive() || ChooseTimer == 0 || SelectedUpgradeIndex == -1 || Upgrades.Count == 0 || LastUpdate == Utils.TimeStamp) return;
            LastUpdate = Utils.TimeStamp;

            ChooseTimer--;
            Utils.SendRPC(CustomRPC.SyncEvolver, EvolverPC.PlayerId, 3, ChooseTimer);

            if (ChooseTimer == 0) ApplySelectedUpgradeAndReset();

            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        void ApplySelectedUpgradeAndReset()
        {
            switch (Upgrades[SelectedUpgradeIndex])
            {
                case Upgrade.DecreaseKillCooldown:
                    Stats.KillCooldown -= 2.5f;
                    break;
                case Upgrade.GainImpostorVision:
                    Stats.ImpostorVision = true;
                    break;
                case Upgrade.IncreaseVision:
                    Stats.Vision += 0.4f;
                    break;
                case Upgrade.IncreaseSpeed:
                    Stats.Speed += 0.25f;
                    break;
                case Upgrade.IncreaseKillDistance:
                    Stats.KillDistance++;
                    break;
                case Upgrade.GainVent:
                    Stats.CanVent = true;
                    Stats.VentUseLimit = 1;
                    break;
                case Upgrade.IncreaseVentUseLimit:
                    Stats.VentUseLimit += 3;
                    break;
                case Upgrade.GainSabotage:
                    Stats.CanSabotage = true;
                    Stats.SabotageUseLimit = 1;
                    break;
                case Upgrade.IncreaseSabotageUseLimit:
                    Stats.SabotageUseLimit += 2;
                    break;
                case Upgrade.GainShield:
                    Stats.Shielded = true;
                    break;
            }

            EnsureStatLimits();
            if (GameStates.IsInTask)
            {
                EvolverPC.SyncSettings();
                if (Main.KillTimers[EvolverPC.PlayerId] > Stats.KillCooldown) EvolverPC.SetKillCooldown();
                if (PlayerControl.LocalPlayer.PlayerId == EvolverPC.PlayerId) HudManager.Instance.SetHudActive(EvolverPC, EvolverPC.Data.Role, true);
            }

            Upgrades = [];
            SelectedUpgradeIndex = -1;
            Utils.SendRPC(CustomRPC.SyncEvolver, EvolverPC.PlayerId, 2, SelectedUpgradeIndex);
        }

        void EnsureStatLimits()
        {
            Stats.KillCooldown = Math.Max(Stats.KillCooldown, Limits.MinKillCooldown);
            Stats.Vision = Math.Min(Stats.Vision, Limits.MaxVision);
            Stats.Speed = Math.Min(Stats.Speed, Limits.MaxSpeed);
            Stats.KillDistance = Math.Min(Stats.KillDistance, Limits.MaxKillDistance);
            Stats.VentUseLimit = Math.Max(Stats.VentUseLimit, 0);
            Stats.SabotageUseLimit = Math.Max(Stats.SabotageUseLimit, 0);

            Utils.NotifyRoles(SpecifySeer: EvolverPC, SpecifyTarget: EvolverPC);
            Logger.Info($" KCD: {Stats.KillCooldown}, Vision: {Stats.Vision}, Speed: {Stats.Speed}, KDis: {Stats.KillDistance}", "Evolver Stats");
        }

        public override void OnPet(PlayerControl pc)
        {
            if (Upgrades.Count == 0 || SelectedUpgradeIndex == -1) return;

            SelectedUpgradeIndex = (SelectedUpgradeIndex + 1) % Upgrades.Count;
            ChooseTimer = 8;

            Utils.SendRPC(CustomRPC.SyncEvolver, EvolverPC.PlayerId, 2, SelectedUpgradeIndex);
            Utils.SendRPC(CustomRPC.SyncEvolver, EvolverPC.PlayerId, 3, ChooseTimer);
        }

        void SendRPC()
        {
            var w = Utils.CreateRPC(CustomRPC.SyncEvolver);
            w.Write(EvolverPC.PlayerId);
            w.Write(1);
            w.Write(Upgrades.Count);
            Upgrades.ForEach(x => w.Write((int)x));
            Utils.EndRPC(w);
        }

        public void ReceiveRPC(MessageReader reader)
        {
            switch (reader.ReadPackedInt32())
            {
                case 1:
                    Upgrades = [];
                    for (int i = 0; i < Upgrades.Count; i++)
                        Upgrades.Add((Upgrade)reader.ReadPackedInt32());
                    break;
                case 2:
                    SelectedUpgradeIndex = reader.ReadPackedInt32();
                    if (SelectedUpgradeIndex == -1) Upgrades = [];
                    break;
                case 3:
                    ChooseTimer = reader.ReadPackedInt32();
                    break;
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            if (seer.PlayerId != target.PlayerId || (seer.IsModClient() && !isHUD) || isMeeting || ChooseTimer == 0 || Upgrades.Count == 0 || SelectedUpgradeIndex == -1) return string.Empty;
            return string.Format(Translator.GetString("EvolverSuffix"), ChooseTimer, Translator.GetString($"EvolverUpgrade.{Upgrades[SelectedUpgradeIndex]}"), string.Join(", ", Upgrades.ConvertAll(x => Translator.GetString($"EvolverUpgrade.{x}"))));
        }

        public override string GetProgressText(byte playerId, bool comms)
        {
            var sb = new System.Text.StringBuilder();

            if (Stats.CanVent) sb.Append(string.Format(Translator.GetString("EvolverProgress.Vent"), Stats.VentUseLimit));
            if (Stats.CanSabotage) sb.Append(string.Format(Translator.GetString("EvolverProgress.Sabotage"), Stats.SabotageUseLimit));

            if (sb.Length > 0)
            {
                sb.Insert(0, "<#ffffff>");
                sb.Append("</color>");
            }

            return sb.ToString();
        }

        enum Upgrade
        {
            DecreaseKillCooldown,
            GainImpostorVision,
            IncreaseVision,
            IncreaseSpeed,
            IncreaseKillDistance,
            GainVent,
            IncreaseVentUseLimit,
            GainSabotage,
            IncreaseSabotageUseLimit,
            GainShield
        }
    }
}