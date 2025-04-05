using System.Collections;
using System.Collections.Generic;

namespace EHR.Impostor;

internal class OverKiller : RoleBase
{
    public static bool On;

    public static List<byte> OverDeadPlayerList = [];
    private static OptionItem KillCooldown;
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(16900, TabGroup.ImpostorRoles, CustomRoles.OverKiller);

        KillCooldown = new FloatOptionItem(16902, "KillCooldown", new(0f, 180f, 0.5f), 30f, TabGroup.ImpostorRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.OverKiller])
            .SetValueFormat(OptionFormat.Seconds);
    }

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
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!killer.RpcCheckAndMurder(target, true)) return false;

        if (killer.PlayerId != target.PlayerId && !target.Is(CustomRoles.Unreportable))
        {
            Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Dismembered;

            LateTask.New(() =>
            {
                if (!OverDeadPlayerList.Contains(target.PlayerId))
                    OverDeadPlayerList.Add(target.PlayerId);

                if (target.Is(CustomRoles.Avanger))
                {
                    target.Suicide(PlayerState.DeathReason.Dismembered, killer);

                    foreach (PlayerControl pc in Main.AllAlivePlayerControls)
                        pc.Suicide(PlayerState.DeathReason.Revenge, target);

                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.None);
                    return;
                }

                Vector2 ops = target.Pos();
                Vector2 originPos = killer.Pos();
                var rd = IRandom.Instance;

                Main.Instance.StartCoroutine(SpawnFakeDeadBodies());
                return;

                IEnumerator SpawnFakeDeadBodies()
                {
                    var sender = CustomRpcSender.Create("Butcher kill");

                    for (var i = 0; i < 26; i++)
                    {
                        Vector2 location = new(ops.x + ((float)(rd.Next(0, 201) - 100) / 100), ops.y + ((float)(rd.Next(0, 201) - 100) / 100));
                        location += new Vector2(0, 0.3636f);

                        sender.AutoStartRpc(target.NetTransform.NetId, (byte)RpcCalls.SnapTo);
                        sender.WriteVector2(location);
                        sender.Write(target.NetTransform.lastSequenceId);
                        sender.EndRpc();

                        target.NetTransform.SnapTo(location);
                        killer.MurderPlayer(target, ExtendedPlayerControl.ResultFlags);

                        sender.AutoStartRpc(killer.NetId, (byte)RpcCalls.MurderPlayer);
                        sender.WriteNetObject(target);
                        sender.Write((byte)ExtendedPlayerControl.ResultFlags);
                        sender.EndRpc();

                        if (i % 6 == 0) yield return null;
                    }

                    yield return null;
                    sender.TP(killer, originPos);
                    sender.SendMessage();
                }
            }, 0.05f, "OverKiller Murder");
        }

        return base.OnCheckMurder(killer, target);
    }
}