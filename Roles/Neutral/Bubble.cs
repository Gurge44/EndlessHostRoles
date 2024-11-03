using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Utils;

namespace EHR.Neutral
{
    internal class Bubble : RoleBase
    {
        private static OptionItem KillCooldown;
        private static OptionItem HasImpostorVision;
        public static OptionItem CanVent;
        public static OptionItem NotifyDelay;
        private static OptionItem ExplodeDelay;
        private static OptionItem BubbleDiesIfInRange;
        private static OptionItem ExplosionRadius;

        public static readonly Dictionary<byte, long> EncasedPlayers = [];
        private static readonly Dictionary<byte, long> LastUpdates = [];
        private byte BubbleId = byte.MaxValue;
        private static int Id => 643220;

        private PlayerControl BubblePC => GetPlayerById(BubbleId);

        public override bool IsEnable => BubbleId != byte.MaxValue;

        public override void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Bubble);

            KillCooldown = new FloatOptionItem(Id + 2, "BubbleCD", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bubble])
                .SetValueFormat(OptionFormat.Seconds);

            HasImpostorVision = new BooleanOptionItem(Id + 6, "ImpostorVision", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bubble]);

            NotifyDelay = new IntegerOptionItem(Id + 3, "BubbleTargetNotifyDelay", new(0, 60, 1), 3, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bubble])
                .SetValueFormat(OptionFormat.Seconds);

            ExplodeDelay = new IntegerOptionItem(Id + 4, "BubbleExplosionDelay", new(0, 60, 1), 10, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bubble])
                .SetValueFormat(OptionFormat.Seconds);

            BubbleDiesIfInRange = new BooleanOptionItem(Id + 5, "BubbleDiesIfInRange", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bubble]);

            ExplosionRadius = new FloatOptionItem(Id + 7, "BubbleExplosionRadius", new(0.1f, 5f, 0.1f), 3f, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bubble])
                .SetValueFormat(OptionFormat.Multiplier);

            CanVent = new BooleanOptionItem(Id + 8, "CanVent", true, TabGroup.NeutralRoles)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Bubble]);
        }

        public override void Init()
        {
            BubbleId = byte.MaxValue;
            EncasedPlayers.Clear();
            LastUpdates.Clear();
        }

        public override void Add(byte playerId)
        {
            BubbleId = playerId;
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
        }

        public override void ApplyGameOptions(IGameOptions opt, byte id)
        {
            opt.SetVision(HasImpostorVision.GetBool());
        }

        public override bool CanUseImpostorVentButton(PlayerControl pc)
        {
            return CanVent.GetBool();
        }

        private void SendRPC(byte id = byte.MaxValue, bool remove = false, bool clear = false)
        {
            if (!IsEnable || !DoRPC) return;

            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncBubble, SendOption.Reliable);
            writer.Write(remove);
            writer.Write(clear);
            writer.Write(id);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
        }

        public static void ReceiveRPC(MessageReader reader)
        {
            bool remove = reader.ReadBoolean();
            bool clear = reader.ReadBoolean();
            byte id = reader.ReadByte();

            if (clear)
                EncasedPlayers.Clear();
            else if (remove)
                EncasedPlayers.Remove(id);
            else
                EncasedPlayers.Add(id, TimeStamp);
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (!IsEnable || target == null) return false;

            EncasedPlayers.Add(target.PlayerId, TimeStamp);
            SendRPC(target.PlayerId);
            BubblePC.SetKillCooldown();
            return false;
        }

        public override void OnGlobalFixedUpdate(PlayerControl encasedPc, bool lowLoad)
        {
            if (lowLoad || !IsEnable || !GameStates.IsInTask || !EncasedPlayers.TryGetValue(encasedPc.PlayerId, out long ts)) return;

            long now = TimeStamp;
            byte id = encasedPc.PlayerId;

            if (ts + ExplodeDelay.GetInt() < now)
            {
                if (!encasedPc.IsAlive())
                {
                    EncasedPlayers.Remove(id);
                    SendRPC(id, true);
                    return;
                }

                IEnumerable<PlayerControl> players = GetPlayersInRadius(ExplosionRadius.GetFloat(), encasedPc.Pos());

                int numDied = 0;

                foreach (PlayerControl pc in players)
                {
                    if (pc == null) continue;

                    if (pc.PlayerId == BubbleId)
                    {
                        if (BubbleDiesIfInRange.GetBool())
                        {
                            LateTask.New(() =>
                            {
                                if (GameStates.IsInTask) pc.Suicide(PlayerState.DeathReason.Bombed);
                            }, 0.5f, log: false);
                        }

                        continue;
                    }

                    pc.Suicide(PlayerState.DeathReason.Bombed, BubblePC);
                    numDied++;
                }

                if (BubbleId == PlayerControl.LocalPlayer.PlayerId && numDied >= 5)
                    Achievements.Type.SorryToBurstYourBubble.CompleteAfterGameEnd();

                EncasedPlayers.Remove(id);
                SendRPC(id, true);
                return;
            }

            if (ts + NotifyDelay.GetInt() < now)
            {
                Main.AllAlivePlayerControls.Where(x => (!LastUpdates.TryGetValue(x.PlayerId, out long la) || la != now) && Vector2.Distance(x.Pos(), encasedPc.Pos()) < 5f).Do(x =>
                {
                    NotifyRoles(SpecifySeer: x, SpecifyTarget: encasedPc);
                    LastUpdates[x.PlayerId] = now;
                });
            }
        }

        public override void OnReportDeadBody()
        {
            if (!IsEnable) return;

            foreach (PlayerControl pc in EncasedPlayers.Keys.Select(x => GetPlayerById(x)).Where(x => x != null && x.IsAlive())) pc.Suicide(PlayerState.DeathReason.Bombed, BubblePC);

            EncasedPlayers.Clear();
            SendRPC(clear: true);
        }

        public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
        {
            if (target == null || !EncasedPlayers.TryGetValue(target.PlayerId, out long ts) || (ts + NotifyDelay.GetInt() >= TimeStamp && !seer.Is(CustomRoles.Bubble))) return string.Empty;

            return ColorString(GetRoleColor(CustomRoles.Bubble), $"⚠ {ExplodeDelay.GetInt() - (TimeStamp - ts) + 1}");
        }
    }
}