using HarmonyLib;
using Hazel;

namespace TOHE.Modules
{
    public static class KeepProtection
    {
        public static long LastFixedUpdate = 0;
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

            long now = Utils.GetTimeStamp();
            if (LastFixedUpdate + 24 >= now) return;
            LastFixedUpdate = now;

            foreach (var pc in Main.AllAlivePlayerControls)
            {
                if (!pc.AmOwner && !pc.IsProtected())
                {
                    pc.Protect();
                }
            }
            PlayerControl.LocalPlayer.Protect();
        }

        public static void ProtectEveryone()
        {
            if (Main.UseVersionProtocol.Value) return;
            LastFixedUpdate = Utils.GetTimeStamp();
            Main.AllAlivePlayerControls.Do(x => x.Protect());
        }
    }
}
