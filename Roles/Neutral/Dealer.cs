using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;
using UnityEngine;

namespace EHR.Neutral;

public class Dealer : RoleBase
{
    public static bool On;

    private static OptionItem AbilityCooldown;
    private static OptionItem AddonAppears;
    private static OptionItem AssignNeedToWin;

    private static readonly string[] AddonAppearsOptions =
    [
        "Dealer.AAO.Instantly",
        "Dealer.AAO.After5s",
        "Dealer.AAO.OnMeeting"
    ];

    private int AssignedNum;

    private Dictionary<byte, List<CustomRoles>> ScheduledAssigns = [];

    public override bool IsEnable => On;

    public bool IsWon => AssignedNum >= AssignNeedToWin.GetInt();

    public override void SetupCustomOption()
    {
        StartSetup(651800)
            .AutoSetupOption(ref AbilityCooldown, 15f, new FloatValueRule(0f, 120f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref AddonAppears, 2, AddonAppearsOptions)
            .AutoSetupOption(ref AssignNeedToWin, 5, new IntegerValueRule(1, 30, 1));
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        ScheduledAssigns = [];
        AssignedNum = 0;
    }

    public override bool CanUseKillButton(PlayerControl pc)
    {
        return pc.IsAlive();
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = AbilityCooldown.GetFloat();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        var randomAddon = Options.GroupedAddons.Values.Flatten().Where(x => !x.IsNotAssignableMidGame() && x.GetMode() != 0).RandomElement();

        switch (AddonAppears.GetValue())
        {
            case 0:
                target.RpcSetCustomRole(randomAddon);
                break;
            case 1:
                LateTask.New(() =>
                {
                    if (GameStates.IsMeeting || GameStates.IsEnded || GameStates.IsLobby) return;
                    target.RpcSetCustomRole(randomAddon);
                    ScheduledAssigns[target.PlayerId].Remove(randomAddon);
                }, 5f);
                goto case 2;
            case 2:
                ScheduledAssigns.TryAdd(target.PlayerId, []);
                ScheduledAssigns[target.PlayerId].Add(randomAddon);
                break;
        }

        AssignedNum++;
        Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, AssignedNum);
        killer.SetKillCooldown(AbilityCooldown.GetFloat());
        return false;
    }

    public override void OnReportDeadBody()
    {
        foreach ((byte id, List<CustomRoles> list) in ScheduledAssigns)
        {
            var pc = id.GetPlayer();
            if (pc == null) continue;

            list.ForEach(x => pc.RpcSetCustomRole(x));
        }

        ScheduledAssigns = [];
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        var color = IsWon ? Color.green : Color.white;
        return $"{base.GetProgressText(playerId, comms)} {Utils.ColorString(color, $"{AssignedNum}/{AssignNeedToWin.GetInt()}")}";
    }

    public void ReceiveRPC(MessageReader reader)
    {
        AssignedNum = reader.ReadPackedInt32();
    }
}