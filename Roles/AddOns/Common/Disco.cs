using System.Collections.Generic;
using EHR.Roles.Impostor;
using static EHR.Options;

namespace EHR.Roles.AddOns.Common
{
    internal class Disco : IAddon
    {
        public AddonTypes Type => AddonTypes.Mixed;

        public void SetupCustomOption()
        {
            SetupAdtRoleOptions(15430, CustomRoles.Disco, canSetNum: true);
            DiscoChangeInterval = IntegerOptionItem.Create(15433, "DiscoChangeInterval", new(1, 90, 1), 5, TabGroup.Addons)
                .SetParent(CustomRoleSpawnChances[CustomRoles.Disco])
                .SetValueFormat(OptionFormat.Seconds);
        }

        private static readonly Dictionary<byte, long> LastChange = [];

        private static void ChangeColor(PlayerControl pc)
        {
            int colorId = IRandom.Instance.Next(0, 18);

            pc.SetColor(colorId);

            try
            {
                pc.RpcSetColor((byte)colorId);
            }
            catch
            {
                var sender = CustomRpcSender.Create(name: $"Disco.ChangeColor({pc.Data.PlayerName})");
                sender.AutoStartRpc(pc.NetId, (byte)RpcCalls.SetColor)
                    .Write((byte)colorId)
                    .EndRpc();
                sender.SendMessage();
            }
        }

        public static void OnFixedUpdate(PlayerControl pc)
        {
            if (!pc.Is(CustomRoles.Disco) || !GameStates.IsInTask || pc.IsShifted() || Camouflager.IsActive || (Utils.IsActive(SystemTypes.Comms) && CommsCamouflage.GetBool())) return;
            long now = Utils.TimeStamp;
            if (LastChange.TryGetValue(pc.PlayerId, out var change) && change + DiscoChangeInterval.GetInt() > now) return;
            ChangeColor(pc);
            LastChange[pc.PlayerId] = now;
        }
    }
}