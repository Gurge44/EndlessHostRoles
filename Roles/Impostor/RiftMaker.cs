using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Neutral;
using Hazel;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Impostor;

public class RiftMaker : RoleBase
{
    private const int Id = 640900;
    public static List<byte> PlayerIdList = [];

    public static OptionItem KillCooldown;
    public static OptionItem ShapeshiftCooldown;
    public long LastTP = TimeStamp;

    public List<Vector2> Marks = [];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.RiftMaker);

        KillCooldown = new FloatOptionItem(Id + 10, "KillCooldown", new(0f, 180f, 0.5f), 25f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.RiftMaker])
            .SetValueFormat(OptionFormat.Seconds);

        ShapeshiftCooldown = new FloatOptionItem(Id + 11, "ShapeshiftCooldown", new(0f, 180f, 0.5f), 10f, TabGroup.ImpostorRoles).SetParent(CustomRoleSpawnChances[CustomRoles.RiftMaker])
            .SetValueFormat(OptionFormat.Seconds);
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

        if (Pelican.IsEaten(player.PlayerId) || !player.IsAlive()) return;

        if (!player.Is(CustomRoles.RiftMaker)) return;

        if (Marks.Count != 2) return;

        if (Vector2.Distance(Marks[0], Marks[1]) <= 4f)
        {
            player.Notify(GetString("IncorrectMarks"));
            Marks.Clear();
            SendRPC(CustomRPC.SyncRoleData, player.PlayerId, 2);
            return;
        }

        if (LastTP + 5 > TimeStamp) return;

        Vector2 position = player.Pos();

        var isTP = false;
        Vector2 from = Marks[0];

        foreach (Vector2 mark in Marks)
        {
            float dis = Vector2.Distance(mark, position);
            if (dis > 2f) continue;

            isTP = true;
            from = mark;
        }

        if (isTP)
        {
            LastTP = TimeStamp;

            if (from == Marks[0])
                player.TP(Marks[1]);
            else if (from == Marks[1])
                player.TP(Marks[0]);
            else
                Logger.Error($"Teleport failed - from: {from}", "RiftMakerTP");
        }
    }

    public override void OnReportDeadBody()
    {
        LastTP = TimeStamp;
    }

    public override void OnEnterVent(PlayerControl player, Vent vent)
    {
        Marks.Clear();
        SendRPC(CustomRPC.SyncRoleData, player.PlayerId, 2);
        player.Notify(GetString("MarksCleared"));
        player.MyPhysics?.RpcExitVent(vent.Id);
    }

    public override bool OnShapeshift(PlayerControl player, PlayerControl target, bool shapeshifting)
    {
        if (player == null) return false;

        if (!shapeshifting) return true;

        if (Marks.Count >= 2) return false;

        Mark(player);

        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (pc == null) return false;

        if (Marks.Count >= 2) return false;

        Mark(pc);

        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        if (pc == null) return;

        if (Marks.Count >= 2) return;

        Mark(pc);
    }

    private void Mark(PlayerControl player)
    {
        Vector2 pos = player.Pos();
        Marks.Add(pos);
        SendRPC(CustomRPC.SyncRoleData, player.PlayerId, 1, pos);
        if (Marks.Count == 2) LastTP = TimeStamp;
        player.Notify(GetString("MarkDone"));
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