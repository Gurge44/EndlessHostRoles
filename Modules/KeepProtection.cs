using HarmonyLib;
using Hazel;
using System.Linq;

namespace TOHE.Modules
{
    public static class KeepProtection
    {
        public static long LastFixUpdate = 0;
        public static void Protect(this PlayerControl target)
        {
            if (Main.UseVersionProtocol.Value || target.Data.IsDead) return;

            // Host
            if (!target.AmOwner) target.ProtectPlayer(target, 18);

            // Client
            var sender = CustomRpcSender.Create("KeepProtectSender", sendOption: SendOption.Reliable);
            sender.AutoStartRpc(PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.ProtectPlayer)
                .WriteNetObject(target)
                .Write(18)
                .EndRpc();
            sender.SendMessage();
        }
        public static void OnFixedUpdate()
        {
            if (Main.UseVersionProtocol.Value) return;
            if (LastFixUpdate + 24 < Utils.GetTimeStamp())
            {
                LastFixUpdate = Utils.GetTimeStamp();
                Main.AllAlivePlayerControls.ToArray()
                    .Where(x => !x.AmOwner && !x.IsProtected())
                    .Do(x => x.Protect());
                PlayerControl.LocalPlayer.Protect();
            }
        }

        public static void ProtectEveryone()
        {
            if (Main.UseVersionProtocol.Value) return;
            LastFixUpdate = Utils.GetTimeStamp();
            Main.AllAlivePlayerControls.ToArray().Do(x => x.Protect());
        }
    }
}
