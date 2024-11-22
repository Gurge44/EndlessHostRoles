using System.Collections.Generic;
using EHR.Impostor;
using static EHR.Options;

namespace EHR.AddOns.Common
{
    internal class Disco : IAddon
    {
        private static readonly Dictionary<byte, long> LastChange = [];
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15430, CustomRoles.Disco, canSetNum: true, teamSpawnOptions: true);

            DiscoChangeInterval = new IntegerOptionItem(15437, "DiscoChangeInterval", new(1, 90, 1), 5, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Disco])
                .SetValueFormat(OptionFormat.Seconds);
        }

        public static void ChangeColor(PlayerControl pc)
        {
            int colorId = IRandom.Instance.Next(0, 18);

            pc.SetColor(colorId);

            try
            {
                pc.RpcSetColor((byte)colorId);
            }
            catch
            {
                var sender = CustomRpcSender.Create($"Disco.ChangeColor({pc.Data.PlayerName})");

                sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetColor)
                    .Write(pc.Data.NetId)
                    .Write((byte)colorId)
                    .EndRpc();

                sender.SendMessage();
            }
        }

        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (!pc.Is(CustomRoles.Disco) || !GameStates.IsInTask || pc.IsShifted() || Camouflage.IsCamouflage || pc.inVent || pc.MyPhysics.Animations.IsPlayingEnterVentAnimation() || pc.walkingToVent || pc.onLadder || pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation() || pc.inMovingPlat) return;

            long now = Utils.TimeStamp;
            if (LastChange.TryGetValue(pc.PlayerId, out long change) && change + DiscoChangeInterval.GetInt() > now) return;

            ChangeColor(pc);
            LastChange[pc.PlayerId] = now;
        }
    }
}