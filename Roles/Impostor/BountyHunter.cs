using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Impostor
{
    public class BountyHunter : RoleBase
    {
        private const int Id = 800;
        private static List<byte> PlayerIdList = [];

        private static OptionItem OptionTargetChangeTime;
        private static OptionItem OptionSuccessKillCooldown;
        private static OptionItem OptionFailureKillCooldown;
        private static OptionItem OptionShowTargetArrow;

        private static float TargetChangeTime;
        private static float SuccessKillCooldown;
        private static float FailureKillCooldown;
        private static bool ShowTargetArrow;
        private byte BountyId;
        private float ChangeTimer;
        private byte Target;

        private int Timer;

        public override bool IsEnable => PlayerIdList.Count > 0;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.BountyHunter);

            OptionTargetChangeTime = new FloatOptionItem(Id + 10, "BountyTargetChangeTime", new(10f, 180f, 2.5f), 50f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter])
                .SetValueFormat(OptionFormat.Seconds);

            OptionSuccessKillCooldown = new FloatOptionItem(Id + 11, "BountySuccessKillCooldown", new(0f, 180f, 0.5f), 3f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter])
                .SetValueFormat(OptionFormat.Seconds);

            OptionFailureKillCooldown = new FloatOptionItem(Id + 12, "BountyFailureKillCooldown", new(0f, 180f, 2.5f), 35f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter])
                .SetValueFormat(OptionFormat.Seconds);

            OptionShowTargetArrow = new BooleanOptionItem(Id + 13, "BountyShowTargetArrow", true, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.BountyHunter]);
        }

        public override void Init()
        {
            PlayerIdList = [];

            Target = byte.MaxValue;
            ChangeTimer = OptionTargetChangeTime.GetFloat();
            Timer = OptionTargetChangeTime.GetInt();
            BountyId = byte.MaxValue;
        }

        public override void Add(byte playerId)
        {
            PlayerIdList.Add(playerId);
            BountyId = playerId;

            TargetChangeTime = OptionTargetChangeTime.GetFloat();
            SuccessKillCooldown = OptionSuccessKillCooldown.GetFloat();
            FailureKillCooldown = OptionFailureKillCooldown.GetFloat();
            ShowTargetArrow = OptionShowTargetArrow.GetBool();

            Timer = (int)TargetChangeTime;

            Target = byte.MaxValue;
            ChangeTimer = TargetChangeTime;

            if (AmongUsClient.Instance.AmHost) ResetTarget(Utils.GetPlayerById(playerId));
        }

        private void SendRPC()
        {
            if (!Utils.DoRPC) return;

            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetBountyTarget, SendOption.Reliable);
            writer.Write(BountyId);
            writer.Write(Target);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public void ReceiveRPC(byte bountyId, byte targetId)
        {
            Target = targetId;
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer == null || target == null) return false;

            if (GetTarget(killer) == target.PlayerId)
            {
                Logger.Info($"{killer.Data?.PlayerName}: Killed Target", "BountyHunter");
                Main.AllPlayerKillCooldown[killer.PlayerId] = SuccessKillCooldown;
                killer.SyncSettings();
                ResetTarget(killer);
            }
            else
            {
                Logger.Info($"{killer.Data?.PlayerName}: Killed Non-Target", "BountyHunter");
                Main.AllPlayerKillCooldown[killer.PlayerId] = FailureKillCooldown;
                killer.SyncSettings();
            }

            return base.OnCheckMurder(killer, target);
        }

        public override void OnReportDeadBody()
        {
            ChangeTimer = TargetChangeTime;
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (!GameStates.IsInTask || float.IsNaN(ChangeTimer)) return;

            if (!player.IsAlive())
                ChangeTimer = float.NaN;
            else
            {
                byte targetId = GetTarget(player);

                if (ChangeTimer >= TargetChangeTime)
                {
                    byte newTargetId = ResetTarget(player);
                    Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: Utils.GetPlayerById(newTargetId));
                }

                if (ChangeTimer >= 0)
                {
                    ChangeTimer += Time.fixedDeltaTime;
                    int tempTimer = Timer;
                    Timer = (int)(TargetChangeTime - ChangeTimer);
                    if (tempTimer != Timer && Timer <= 15 && !player.IsModClient()) Utils.NotifyRoles(SpecifySeer: player, SpecifyTarget: player);
                }

                if (Utils.GetPlayerById(targetId)?.IsAlive() == false)
                {
                    ResetTarget(player);
                    Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}'s target was reset because the previous target died", "BountyHunter");
                    Utils.NotifyRoles(SpecifySeer: player);
                }
            }
        }

        public byte GetTarget(PlayerControl player)
        {
            if (player == null) return 0xff;

            byte targetId = Target == byte.MaxValue ? ResetTarget(player) : Target;
            return targetId;
        }

        private byte ResetTarget(PlayerControl player)
        {
            if (!AmongUsClient.Instance.AmHost) return 0xff;

            byte playerId = player.PlayerId;

            ChangeTimer = 0f;
            Timer = (int)TargetChangeTime;

            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}: Reset Target", "BountyHunter");

            List<PlayerControl> cTargets = new(Main.AllAlivePlayerControls.Where(pc => !pc.Is(CustomRoleTypes.Impostor)));

            if (cTargets.Count >= 2 && Target != byte.MaxValue) cTargets.RemoveAll(x => x.PlayerId == Target);

            if (cTargets.Count == 0)
            {
                Logger.Warn("No Targets Available", "BountyHunter");
                return 0xff;
            }

            PlayerControl target = cTargets.RandomElement();
            byte targetId = target.PlayerId;
            Target = targetId;
            if (ShowTargetArrow) TargetArrow.Add(playerId, targetId);

            Logger.Info($"{player.GetNameWithRole().RemoveHtmlTags()}'s New Target Is {target.GetNameWithRole().RemoveHtmlTags()}", "BountyHunter");

            SendRPC();
            return targetId;
        }

        public override void AfterMeetingTasks()
        {
            foreach (byte id in PlayerIdList.ToArray())
            {
                if (!Main.PlayerStates[id].IsDead)
                {
                    ChangeTimer = 0f;
                    Timer = (int)TargetChangeTime;

                    if (Utils.GetPlayerById(id).GetCustomRole() == CustomRoles.BountyHunter)
                    {
                        Main.AllPlayerKillCooldown[id] = Options.DefaultKillCooldown;
                        Utils.GetPlayerById(id).SyncSettings();
                    }
                }
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
        {
            return GetTargetText(seer, target, hud) + GetTargetArrow(seer, target);
        }

        private static string GetTargetText(PlayerControl bounty, PlayerControl tar, bool hud)
        {
            if (GameStates.IsMeeting || bounty.PlayerId != tar.PlayerId) return string.Empty;

            if (Main.PlayerStates[bounty.PlayerId].Role is not BountyHunter bh) return string.Empty;

            byte targetId = bh.GetTarget(bounty);
            return targetId != 0xff ? $"<color=#00ffa5>{(hud ? GetString("BountyCurrentTarget") : GetString("Target"))}:</color> <b>{Main.AllPlayerNames[targetId].RemoveHtmlTags().Replace("\r\n", string.Empty)}</b>" : string.Empty;
        }

        private static string GetTargetArrow(PlayerControl seer, PlayerControl target = null)
        {
            if (target != null && seer.PlayerId != target.PlayerId) return string.Empty;

            if (!ShowTargetArrow || GameStates.IsMeeting) return string.Empty;

            if (Main.PlayerStates[seer.PlayerId].Role is not BountyHunter bh) return string.Empty;

            byte targetId = bh.GetTarget(seer);
            return $"<color=#ffffff> {TargetArrow.GetArrows(seer, targetId)}</color>";
        }
    }
}