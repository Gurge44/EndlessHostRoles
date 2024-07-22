using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using UnityEngine;

namespace EHR.Neutral
{
    public class Beehive : RoleBase
    {
        public static bool On;

        private static OptionItem Distance;
        private static OptionItem Time;
        private static OptionItem StingCooldown;
        private static OptionItem KillCooldown;
        private static OptionItem CanVent;
        private static OptionItem HasImpostorVision;
        private byte BeehiveId;
        public Dictionary<byte, (long TimeStamp, Vector2 InitialPosition)> StungPlayers = [];

        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            int id = 647350;
            Options.SetupRoleOptions(id++, TabGroup.NeutralRoles, CustomRoles.Beehive);
            Distance = new FloatOptionItem(++id, "Beehive.Distance", new(0f, 20f, 0.5f), 15f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beehive]);
            Time = new FloatOptionItem(++id, "Beehive.Time", new(0f, 30f, 0.5f), 10f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beehive])
                .SetValueFormat(OptionFormat.Seconds);
            StingCooldown = new FloatOptionItem(++id, "Beehive.StingCooldown", new(0f, 180f, 0.5f), 5f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beehive])
                .SetValueFormat(OptionFormat.Seconds);
            KillCooldown = new FloatOptionItem(++id, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beehive])
                .SetValueFormat(OptionFormat.Seconds);
            CanVent = new BooleanOptionItem(++id, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beehive]);
            HasImpostorVision = new BooleanOptionItem(++id, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beehive]);
        }

        public override void Init()
        {
            On = false;
        }

        public override void Add(byte playerId)
        {
            On = true;
            BeehiveId = playerId;
            StungPlayers = [];
        }

        public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
        public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!base.OnCheckMurder(killer, target)) return false;

            return killer.CheckDoubleTrigger(target, () =>
            {
                Vector2 pos = target.Pos();
                StungPlayers[target.PlayerId] = (Utils.TimeStamp, pos);
                killer.SetKillCooldown(time: StingCooldown.GetFloat());
                Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
                Utils.SendRPC(CustomRPC.SyncRoleData, BeehiveId, 1, target.PlayerId, pos);
                target.Notify(string.Format(Translator.GetString("Beehive.Notify"), Math.Round(Distance.GetFloat(), 1), Math.Round(Time.GetFloat(), 1)), 8f);
            });
        }

        public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
        {
            if (lowLoad || !pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance) return;

            if (StungPlayers.TryGetValue(pc.PlayerId, out var sp))
            {
                if (Utils.TimeStamp - sp.TimeStamp >= Time.GetFloat())
                {
                    StungPlayers.Remove(pc.PlayerId);
                    Utils.SendRPC(CustomRPC.SyncRoleData, BeehiveId, 2, pc.PlayerId);
                    if (Vector2.Distance(pc.Pos(), sp.InitialPosition) < Distance.GetFloat())
                        pc.Suicide(realKiller: Utils.GetPlayerById(BeehiveId));
                }

                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }
        }

        public void ReceiveRPC(Hazel.MessageReader reader)
        {
            switch (reader.ReadPackedInt32())
            {
                case 1:
                    StungPlayers[reader.ReadByte()] = (Utils.TimeStamp, reader.ReadVector2());
                    break;
                case 2:
                    StungPlayers.Remove(reader.ReadByte());
                    break;
            }
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool isHUD = false, bool isMeeting = false)
        {
            if (seer.PlayerId != target.PlayerId || isMeeting || isHUD || !StungPlayers.TryGetValue(seer.PlayerId, out var sp)) return string.Empty;

            var walked = Math.Round(Vector2.Distance(seer.Pos(), sp.InitialPosition), 1);
            var distance = Math.Round(Distance.GetFloat(), 1);
            var time = Time.GetInt() - (Utils.TimeStamp - sp.TimeStamp);
            var color = walked >= distance ? "<#00ffa5>" : "<#ffa500>";
            var color2 = walked >= distance ? "<#00ffff>" : "<#ffff00>";
            return $"{color}{walked}</color>{color2}/{distance}</color> <#ffffff>({time}s)</color>";
        }
    }
}