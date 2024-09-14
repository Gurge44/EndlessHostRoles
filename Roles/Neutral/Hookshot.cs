﻿using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Utils;

namespace EHR.Neutral
{
    internal class Hookshot : RoleBase
    {
        private static OptionItem KillCooldown;
        private static OptionItem HasImpostorVision;
        private static OptionItem CanVent;
        
        private byte HookshotId = byte.MaxValue;
        public byte MarkedPlayerId = byte.MaxValue;

        private bool ToTargetTP = true;
        private static int Id => 643230;

        private PlayerControl Hookshot_ => GetPlayerById(HookshotId);

        public override bool IsEnable => HookshotId != byte.MaxValue;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Hookshot);
            KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Hookshot])
                .SetValueFormat(OptionFormat.Seconds);
            HasImpostorVision = new BooleanOptionItem(Id + 3, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Hookshot]);
            CanVent = new BooleanOptionItem(Id + 4, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Hookshot]);
        }

        public override void Init()
        {
            HookshotId = byte.MaxValue;
            MarkedPlayerId = byte.MaxValue;
        }

        public override void Add(byte playerId)
        {
            HookshotId = playerId;
            ToTargetTP = true;
            MarkedPlayerId = byte.MaxValue;
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();
        public override bool CanUseSabotage(PlayerControl pc) => base.CanUseSabotage(pc) || (pc.IsAlive() && !(UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool()));

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            opt.SetVision(HasImpostorVision.GetBool());
            if (UsePhantomBasis.GetBool() && UsePhantomBasisForNKs.GetBool())
                AURoleOptions.PhantomCooldown = 1f;
            if (UseUnshiftTrigger.GetBool() && UseUnshiftTriggerForNKs.GetBool())
                AURoleOptions.ShapeshifterCooldown = 1f;
        }

        void SendRPC()
        {
            if (!IsEnable || !DoRPC) return;
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncHookshot, SendOption.Reliable);
            writer.Write(HookshotId);
            writer.Write(ToTargetTP);
            writer.Write(MarkedPlayerId);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            var playerId = reader.ReadByte();
            if (Main.PlayerStates[playerId].Role is not Hookshot hs) return;
            hs.ToTargetTP = reader.ReadBoolean();
            hs.MarkedPlayerId = reader.ReadByte();
        }

        public override void OnPet(PlayerControl pc)
        {
            ExecuteAction();
        }

        public override bool OnSabotage(PlayerControl pc)
        {
            ExecuteAction();
            return false;
        }

        public override bool OnVanish(PlayerControl pc)
        {
            ExecuteAction();
            return false;
        }

        public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
        {
            if (!shapeshifting && !UseUnshiftTrigger.GetBool()) return true;
            ExecuteAction();
            return false;
        }

        void ExecuteAction()
        {
            if (MarkedPlayerId == byte.MaxValue) return;

            var markedPlayer = GetPlayerById(MarkedPlayerId);
            if (markedPlayer == null)
            {
                MarkedPlayerId = byte.MaxValue;
                SendRPC();
                return;
            }

            bool isTPsuccess = ToTargetTP ? Hookshot_.TP(markedPlayer) : markedPlayer.TP(Hookshot_);

            if (isTPsuccess)
            {
                MarkedPlayerId = byte.MaxValue;
                SendRPC();
            }
        }

        public override void OnEnterVent(PlayerControl pc, Vent vent)
        {
            ToTargetTP = !ToTargetTP;
            SendRPC();
            NotifyRoles(SpecifySeer: Hookshot_, SpecifyTarget: Hookshot_);
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (target == null) return false;

            return Hookshot_.CheckDoubleTrigger(target, () =>
            {
                MarkedPlayerId = target.PlayerId;
                SendRPC();
                Hookshot_.SetKillCooldown(5f);
            });
        }

        public override void OnReportDeadBody()
        {
            MarkedPlayerId = byte.MaxValue;
            SendRPC();
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false) => seer.PlayerId == target.PlayerId && seer.PlayerId == HookshotId ? $"<#00ffa5>{Translator.GetString("Mode")}:</color> <#ffffff>{(ToTargetTP ? Translator.GetString("HookshotTpToTarget") : Translator.GetString("HookshotPullTarget"))}</color>" : string.Empty;
    }
}