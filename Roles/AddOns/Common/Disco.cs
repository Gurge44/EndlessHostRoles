using System.Collections.Generic;

namespace TOHE.Roles.AddOns.Common
{
    internal class Disco
    {
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
            if (!pc.Is(CustomRoles.Disco) || !GameStates.IsInTask) return;
            long now = Utils.GetTimeStamp();
            if (LastChange.TryGetValue(pc.PlayerId, out var change) && change + Options.DiscoChangeInterval.GetInt() > now) return;
            ChangeColor(pc);
            LastChange[pc.PlayerId] = now;
        }
    }
}
