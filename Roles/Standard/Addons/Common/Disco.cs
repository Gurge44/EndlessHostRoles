using System.Collections.Generic;
using static EHR.Options;

namespace EHR.Roles;

internal class Disco : IAddon
{
    private static readonly Dictionary<byte, long> LastChange = [];
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        SetupAdtRoleOptions(652000, CustomRoles.Disco, canSetNum: true, teamSpawnOptions: true);

        DiscoChangeInterval = new IntegerOptionItem(652010, "DiscoChangeInterval", new(1, 90, 1), 5, TabGroup.Addons)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Disco])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public static void ChangeColor(PlayerControl pc)
    {
        int colorId = IRandom.Instance.Next(0, 18);

        pc.SetColor(colorId);

        if (GameStates.CurrentServerType != GameStates.ServerType.Vanilla)
            pc.RpcSetColor((byte)colorId);
        else
        {
            var sender = CustomRpcSender.Create($"Disco.ChangeColor({pc.Data.PlayerName})");

            sender.AutoStartRpc(pc.NetId, RpcCalls.SetColor)
                .Write(pc.Data.NetId)
                .Write((byte)colorId)
                .EndRpc();

            sender.SendMessage();
        }
    }

    public static void OnFixedUpdate(PlayerControl pc)
    {
        if (!pc.Is(CustomRoles.Disco) || !GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks || pc.IsShifted() || Camouflage.IsCamouflage || pc.inVent || pc.MyPhysics.Animations.IsPlayingEnterVentAnimation() || pc.walkingToVent || pc.onLadder || pc.MyPhysics.Animations.IsPlayingAnyLadderAnimation() || pc.inMovingPlat) return;

        long now = Utils.TimeStamp;
        if (LastChange.TryGetValue(pc.PlayerId, out long change) && change + DiscoChangeInterval.GetInt() > now) return;

        ChangeColor(pc);
        LastChange[pc.PlayerId] = now;
    }
}
