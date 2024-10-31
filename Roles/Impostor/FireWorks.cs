using System;
using System.Collections.Generic;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using UnityEngine;
using static EHR.Translator;

namespace EHR.Impostor
{
    public class FireWorks : RoleBase
    {
        [Flags]
        public enum FireWorksState
        {
            Initial = 1,
            SettingFireWorks = 2,
            WaitTime = 4,
            ReadyFire = 8,
            FireEnd = 16,
            CanUseKill = Initial | FireEnd
        }

        private const int Id = 2800;
        private static OptionItem FireWorksCountOpt;
        private static OptionItem FireWorksRadiusOpt;
        private static OptionItem CanKill;
        private static OptionItem KillCooldown;
        private static OptionItem CanIgniteBeforePlacingAllFireworks;

        public static bool On;
        private static int FireWorksCount = 1;
        private static float FireWorksRadius = 1;
        private List<Vector3> fireWorksPosition = [];

        public int nowFireWorksCount;
        private FireWorksState state;

        public override bool IsEnable => On;

        public override void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.FireWorks);

            FireWorksCountOpt = new IntegerOptionItem(Id + 10, "FireWorksMaxCount", new(1, 10, 1), 3, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.FireWorks])
                .SetValueFormat(OptionFormat.Pieces);

            FireWorksRadiusOpt = new FloatOptionItem(Id + 11, "FireWorksRadius", new(0.5f, 5f, 0.5f), 2f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.FireWorks])
                .SetValueFormat(OptionFormat.Multiplier);

            CanKill = new BooleanOptionItem(Id + 12, "CanKill", true, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.FireWorks]);

            KillCooldown = new FloatOptionItem(Id + 13, "KillCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.FireWorks])
                .SetValueFormat(OptionFormat.Seconds);

            CanIgniteBeforePlacingAllFireworks = new BooleanOptionItem(Id + 14, "CanIgniteBeforePlacingAllFireworks", false, TabGroup.ImpostorRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.FireWorks]);
        }

        public override void Init()
        {
            On = false;
            nowFireWorksCount = 0;
            fireWorksPosition = [];
            state = FireWorksState.Initial;
        }

        public override void Add(byte playerId)
        {
            On = true;
            FireWorksCount = FireWorksCountOpt.GetInt();
            FireWorksRadius = FireWorksRadiusOpt.GetFloat();
            nowFireWorksCount = FireWorksCount;
            fireWorksPosition = [];
            state = FireWorksState.Initial;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }

        private void SendRPC(byte playerId)
        {
            if (!On || !Utils.DoRPC) return;

            Logger.Info($"Player{playerId}:SendRPC", "FireWorks");
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SendFireWorksState, SendOption.Reliable);
            writer.Write(playerId);
            writer.Write(nowFireWorksCount);
            writer.Write((int)state);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public void ReceiveRPC(int count, FireWorksState newState)
        {
            nowFireWorksCount = count;
            state = newState;
        }

        public override bool CanUseKillButton(PlayerControl pc)
        {
            if (pc == null || pc.Data.IsDead) return false;

            try
            {
                return CanKill.GetBool() || (state & FireWorksState.CanUseKill) != 0;
            }
            catch
            {
                return false;
            }
        }

        public override void OnPet(PlayerControl pc)
        {
            FireWorksState beforeState = state;
            if (CanIgniteBeforePlacingAllFireworks.GetBool()) state = FireWorksState.ReadyFire;

            OnShapeshift(pc, null, true);

            if (beforeState == FireWorksState.ReadyFire) return;

            state = beforeState;
        }

        public override bool OnShapeshift(PlayerControl pc, PlayerControl _, bool shapeshifting)
        {
            Logger.Info("FireWorks ShapeShift", "FireWorks");
            if (pc == null || pc.Data.IsDead || (!shapeshifting && !Options.UseUnshiftTrigger.GetBool()) || Pelican.IsEaten(pc.PlayerId)) return false;

            UseAbility(pc);

            return false;
        }

        public override bool OnVanish(PlayerControl pc)
        {
            Logger.Info("FireWorks Vanish", "FireWorks");
            if (pc == null || pc.Data.IsDead || Pelican.IsEaten(pc.PlayerId)) return false;

            UseAbility(pc);

            return false;
        }

        private void UseAbility(PlayerControl pc)
        {
            switch (state)
            {
                case FireWorksState.Initial:
                case FireWorksState.SettingFireWorks:
                    Logger.Info("Install Firework", "FireWorks");
                    fireWorksPosition.Add(pc.Pos());
                    nowFireWorksCount--;

                    state = nowFireWorksCount == 0
                        ? Main.AliveImpostorCount <= 1 ? FireWorksState.ReadyFire : FireWorksState.WaitTime
                        : FireWorksState.SettingFireWorks;

                    break;
                case FireWorksState.ReadyFire:
                    Logger.Info("Explode fireworks", "FireWorks");
                    var suicide = false;

                    foreach (PlayerControl target in Main.AllAlivePlayerControls)
                    {
                        foreach (Vector3 pos in fireWorksPosition)
                        {
                            float dis = Vector2.Distance(pos, target.transform.position);
                            if (dis > FireWorksRadius) continue;

                            if (target == pc)
                                suicide = true;
                            else
                                target.Suicide(PlayerState.DeathReason.Bombed, pc);
                        }
                    }

                    if (suicide)
                    {
                        int totalAlive = Main.AllAlivePlayerControls.Length;
                        if (totalAlive != 1) pc.Suicide();
                    }

                    state = FireWorksState.FireEnd;
                    break;
            }

            SendRPC(pc.PlayerId);
            Utils.NotifyRoles(ForceLoop: true);
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
        {
            var retText = string.Empty;
            if (seer == null || seer.Data.IsDead || seer.PlayerId != target.PlayerId) return retText;

            if (Main.PlayerStates[seer.PlayerId].Role is not FireWorks fw) return retText;

            if (fw.state == FireWorksState.WaitTime && Main.AliveImpostorCount <= 1)
            {
                fw.state = FireWorksState.ReadyFire;
                fw.SendRPC(seer.PlayerId);
                Utils.NotifyRoles(SpecifySeer: seer, SpecifyTarget: seer);
            }

            switch (fw.state)
            {
                case FireWorksState.Initial:
                case FireWorksState.SettingFireWorks:
                    retText = string.Format(GetString("FireworksPutPhase"), fw.nowFireWorksCount);
                    break;
                case FireWorksState.WaitTime:
                    retText = GetString("FireworksWaitPhase");
                    break;
                case FireWorksState.ReadyFire:
                    retText = GetString("FireworksReadyFirePhase");
                    break;
                case FireWorksState.FireEnd:
                    break;
            }

            return retText;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.AbilityButton?.OverrideText(nowFireWorksCount == 0 ? GetString("FireWorksExplosionButtonText") : GetString("FireWorksInstallAtionButtonText"));
        }
    }
}