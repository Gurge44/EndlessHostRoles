﻿using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral
{
    public class RouleteGrandeur : RoleBase
    {
        private const int Id = 647500;
        private const int BulletCount = 5;
        private const char HitIcon = '\u29bf';
        private const char NoHitIcon = '\u25ef';

        public static bool On;

        private static OptionItem KillCooldown;
        private static OptionItem HasImpostorVision;
        private static OptionItem KCDReduction;

        private int Bullets;
        private float KCD;
        private long LastRoll;

        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.RouleteGrandeur);
            KillCooldown = new FloatOptionItem(Id + 2, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.RouleteGrandeur])
                .SetValueFormat(OptionFormat.Seconds);
            HasImpostorVision = new BooleanOptionItem(Id + 3, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.RouleteGrandeur]);
            KCDReduction = new FloatOptionItem(Id + 4, "KCDReduction", new(0f, 100f, 0.5f), 3.5f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.RouleteGrandeur])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
            Bullets = 1;
            LastRoll = 0;
            KCD = KillCooldown.GetFloat();
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KCD;
        public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
        public override bool CanUseImpostorVentButton(PlayerControl pc) => true;
        public override bool CanUseSabotage(PlayerControl pc) => true;

        public override void OnExitVent(PlayerControl pc, Vent vent)
        {
            Bullets = Bullets >= BulletCount - 1 ? 1 : Bullets + 1;
            Utils.SendRPC(CustomRPC.SyncRouleteGrandeur, pc.PlayerId, 0, Bullets);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }

        public override bool OnSabotage(PlayerControl pc)
        {
            Roll(pc);
            return false;
        }

        public override void OnPet(PlayerControl pc)
        {
            Roll(pc);
        }

        void Roll(PlayerControl pc)
        {
            long now = Utils.TimeStamp;
            if (now - LastRoll < 5) return;
            LastRoll = now;
            Utils.SendRPC(CustomRPC.SyncRouleteGrandeur, pc.PlayerId, 1, LastRoll);

            string result = new(NoHitIcon, BulletCount);
            result = $"<#ffffff>{result}</color>";
            HashSet<int> takenSlots = [];
            for (int i = 0; i < Bullets; i++)
            {
                int slot;
                do slot = IRandom.Instance.Next(BulletCount);
                while (!takenSlots.Add(slot));
                result = result.Remove(slot, 1).Insert(slot, HitIcon.ToString());
            }

            int hitSlot = IRandom.Instance.Next(BulletCount);
            char icon = result[hitSlot];
            result = result.Remove(hitSlot, 1).Insert(hitSlot, $"<color=#ff0000>{icon}</color>");

            bool hit = icon == HitIcon;

            string str;
            if (hit)
            {
                pc.Suicide(PlayerState.DeathReason.BadLuck);
                str = Translator.GetString("RG.Hit");
            }
            else
            {
                float reduction = (float)Math.Round(Bullets * KCDReduction.GetFloat(), 1);
                str = string.Format(Translator.GetString("RG.NoHit"), Bullets, reduction);
                KCD -= reduction;
                pc.ResetKillCooldown();
                pc.MarkDirtySettings();
            }

            pc.Notify($"<size=80%>{str}</size>\n{result}", 15f);
        }

        public void ReceiveRPC(MessageReader reader)
        {
            switch (reader.ReadPackedInt32())
            {
                case 0:
                    Bullets = reader.ReadPackedInt32();
                    break;
                case 1:
                    LastRoll = long.Parse(reader.ReadString());
                    break;
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            if (seer.PlayerId != target.PlayerId || !seer.Is(CustomRoles.RouleteGrandeur) || isMeeting || (seer.IsModClient() && !isHUD) || (!isHUD && Utils.TimeStamp - LastRoll < 15)) return string.Empty;
            return string.Format(Translator.GetString("RG.Suffix"), Bullets);
        }
    }
}