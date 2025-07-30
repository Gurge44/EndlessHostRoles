using System.Collections.Generic;
using EHR.Modules;

namespace EHR.Crewmate;

public class Imitator : RoleBase
{
    public static bool On;
    private static List<byte> PlayerIdList = [];
    public static Dictionary<byte, CustomRoles> ImitatingRole = [];

    public override bool IsEnable => On;


    public override void SetupCustomOption()
    {
        StartSetup(653190);
    }

    public override void Init()
    {
        if (GameStates.InGame && !Main.HasJustStarted) return;
        On = false;
        PlayerIdList = [];
        ImitatingRole = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        ImitatingRole[playerId] = CustomRoles.Imitator;
        PlayerIdList.Add(playerId);
    }

    public static void SetRoles()
    {
        foreach (byte id in PlayerIdList)
        {
            PlayerControl pc = id.GetPlayer();

            if (pc != null && pc.IsAlive() && ImitatingRole.TryGetValue(id, out CustomRoles role) && !pc.Is(role))
            {
                Main.AbilityUseLimit.Remove(pc.PlayerId);
                Utils.SendRPC(CustomRPC.RemoveAbilityUseLimit, pc.PlayerId);
                pc.RpcChangeRoleBasis(role);
                pc.RpcSetCustomRole(role);
            }
        }
    }
}