using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Roles;

public class RiftMaker : RoleBase
{
    private const int Id = 640900;
    private static List<byte> PlayerIdList = [];

    private static OptionItem KillCooldown;
    private static OptionItem ShapeshiftCooldown;
    private static OptionItem CanVent;

    private long LastTP = TimeStamp;
    private List<Vector2> Marks = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.RiftMaker);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 25f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.RiftMaker])
            .SetValueFormat(OptionFormat.Seconds);

        ShapeshiftCooldown = new FloatOptionItem(Id + 11, "ShapeshiftCooldown", new(0f, 180f, 0.5f), 10f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.RiftMaker])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(Id + 12, "CanVent", true, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.RiftMaker]);
    }

    public override void Init()
    {
        PlayerIdList = [];
        Marks = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
        LastTP = TimeStamp;
        Marks = [];
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (UsePhantomBasis.GetBool())
            AURoleOptions.PhantomCooldown = ShapeshiftCooldown.GetFloat();
        else
        {
            if (UsePets.GetBool()) return;

            AURoleOptions.ShapeshifterCooldown = ShapeshiftCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
            AURoleOptions.ShapeshifterLeaveSkin = true;
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!GameStates.IsInTask || ExileController.Instance || AntiBlackout.SkipTasks) return;
        if (Marks.Count != 2) return;
        if (!player.IsAliveWithConditions()) return;

        var marks = Marks;
        Vector2 mark0 = marks[0], mark1 = marks[1];

        if (FastVector2.DistanceWithinRange(mark0, mark1, 4f))
        {
            player.Notify(GetString("IncorrectMarks"));
            Marks.Clear();
            SendRPC(CustomRPC.SyncRoleData, player.PlayerId, 2);
            return;
        }

        long now = TimeStamp;
        if (now - LastTP < 5) return;

        Vector2 pos = player.Pos();
        var near0 = FastVector2.DistanceWithinRange(mark0, pos, 1f);
        var near1 = FastVector2.DistanceWithinRange(mark1, pos, 1f);
        if (!near0 && !near1) return;

        Vector2 target = near0 ? mark1 : mark0;
        player.TP(target);
        LastTP = now;
    }

    public override void OnReportDeadBody()
    {
        LastTP = TimeStamp;
    }

    public override void AfterMeetingTasks()
    {
        LastTP = TimeStamp;
    }

    public override bool OnShapeshift(PlayerControl player, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;
        UseAbility(player);
        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        UseAbility(pc);
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        UseAbility(pc);
    }

    private void UseAbility(PlayerControl player)
    {
        if (Marks.Count < 2)
        {
            Vector2 pos = player.Pos();
            Marks.Add(pos);
            SendRPC(CustomRPC.SyncRoleData, player.PlayerId, 1, pos);
            if (Marks.Count == 2) LastTP = TimeStamp;
            player.Notify(GetString("MarkDone"));
        }
        else
        {
            Marks.Clear();
            SendRPC(CustomRPC.SyncRoleData, player.PlayerId, 2);
            player.Notify(GetString("MarksCleared"));
        }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                Marks.Add(reader.ReadVector2());
                break;
            case 2:
                Marks.Clear();
                break;
        }
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return $" <color=#777777>-</color> {(Marks.Count == 2 ? "<color=#00ff00>" : "<color=#777777>")}{Marks.Count}/2</color>";
    }
}