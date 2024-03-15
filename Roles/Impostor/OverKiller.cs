using Hazel;
using InnerNet;
using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Crewmate;
using TOHE.Roles.Neutral;
using UnityEngine;

namespace TOHE.Roles.Impostor
{
    internal class OverKiller : RoleBase
    {
        public static bool On;
        public override bool IsEnable => On;

        public static void SetupCustomOption()
        {
            Options.SetupRoleOptions(16900, TabGroup.OtherRoles, CustomRoles.OverKiller);
        }

        public static List<byte> OverDeadPlayerList = [];

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
        }

        public override void SetButtonTexts(HudManager hud, byte id)
        {
            hud.KillButton?.OverrideText(Translator.GetString("OverKillerButtonText"));
        }

        public override void SetKillCooldown(byte id)
        {
            Main.AllPlayerKillCooldown[id] = 0;
        }

        public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
        {
            if (killer.PlayerId != target.PlayerId)
            {
                Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Dismembered;
                _ = new LateTask(() =>
                {
                    if (!OverDeadPlayerList.Contains(target.PlayerId)) OverDeadPlayerList.Add(target.PlayerId);
                    if (target.Is(CustomRoles.Avanger))
                    {
                        foreach (var pc in Main.AllAlivePlayerControls)
                        {
                            pc.Suicide(PlayerState.DeathReason.Revenge, target);
                        }

                        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
                        return;
                    }

                    var ops = target.Pos();
                    var originPos = killer.Pos();
                    var rd = IRandom.Instance;
                    for (int i = 0; i < 20; i++)
                    {
                        Vector2 location = new(ops.x + ((float)(rd.Next(0, 201) - 100) / 100), ops.y + ((float)(rd.Next(0, 201) - 100) / 100));
                        location += new Vector2(0, 0.3636f);

                        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(target.NetTransform.NetId, (byte)RpcCalls.SnapTo, SendOption.None);
                        NetHelpers.WriteVector2(location, writer);
                        writer.Write(target.NetTransform.lastSequenceId);
                        AmongUsClient.Instance.FinishRpcImmediately(writer);

                        target.NetTransform.SnapTo(location);
                        killer.MurderPlayer(target, ExtendedPlayerControl.ResultFlags);

                        if (target.Is(CustomRoles.Avanger))
                        {
                            var pcList = Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId || Pelican.IsEaten(x.PlayerId) || Medic.ProtectList.Contains(x.PlayerId) || target.Is(CustomRoles.Pestilence)).ToArray();
                            var rp = pcList[IRandom.Instance.Next(0, pcList.Length)];
                            rp.Suicide(PlayerState.DeathReason.Revenge, target);
                        }

                        MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(killer.NetId, (byte)RpcCalls.MurderPlayer, SendOption.None);
                        messageWriter.WriteNetObject(target);
                        messageWriter.Write((byte)ExtendedPlayerControl.ResultFlags);
                        AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
                    }

                    killer.TP(originPos);
                }, 0.05f, "OverKiller Murder");
            }

            return base.OnCheckMurder(killer, target);
        }
    }
}