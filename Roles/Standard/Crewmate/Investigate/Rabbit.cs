using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;

namespace EHR.Roles;

internal class Rabbit : RoleBase
{
    private static Dictionary<byte, RabbitState> RabbitStates;
    private static OptionItem OptionTaskTrigger;
    private static int TaskTrigger;

    private static int Id => 643330;
    public override bool IsEnable => RabbitStates is { Count: > 0 };

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Rabbit);

        OptionTaskTrigger = new IntegerOptionItem(Id + 2, "RabbitMinTasks", new(0, 90, 1), 3, TabGroup.CrewmateRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Rabbit]);

        Options.OverrideTasksData.Create(Id + 3, TabGroup.CrewmateRoles, CustomRoles.Rabbit);
    }

    public override void Init()
    {
        RabbitStates = null;
        TaskTrigger = OptionTaskTrigger.GetInt();
    }

    public override void Add(byte playerId)
    {
        RabbitStates ??= [];
        RabbitStates[playerId] = new(Utils.GetPlayerById(playerId));
    }

    public override void Remove(byte playerId)
    {
        RabbitStates?.Remove(playerId);
    }

    public static void ReceiveRPCStatic(MessageReader reader)
    {
        byte id = reader.ReadByte();
        bool hasArrow = reader.ReadBoolean();
        RabbitStates ??= [];
        RabbitStates[id].ReceiveRPC(hasArrow);
    }

    public override void OnTaskComplete(PlayerControl pc, int completedTaskCount, int totalTaskCount)
    {
        if (RabbitStates == null || !RabbitStates.TryGetValue(pc.PlayerId, out RabbitState state)) return;

        state.OnTaskComplete();
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || RabbitStates == null || !RabbitStates.TryGetValue(seer.PlayerId, out RabbitState state)) return string.Empty;

        string suffix = state.Suffix;
        return hud ? $"<size=200%>{suffix}</size>" : suffix;
    }

    private class RabbitState(PlayerControl player)
    {
        private bool HasArrow;
        private PlayerControl Player => player;
        private TaskState MyTaskState => Player.GetTaskState();

        public string Suffix => !GameStates.IsInTask || !HasArrow
            ? string.Empty
            : CustomRoles.Rabbit.ColoredTextByRole(LocateArrow.GetArrows(Player));

        public void OnTaskComplete()
        {
            if (!Player.IsAlive() || (MyTaskState.CompletedTasksCount < TaskTrigger && !MyTaskState.IsTaskFinished)) return;

            PlayerControl[] impostors = Main.EnumerateAlivePlayerControls().Where(pc => pc.Is(CustomRoleTypes.Impostor)).ToArray();
            PlayerControl target = impostors.RandomElement();
            if (target == null) return;

            Vector2 pos = target.Pos();
            LocateArrow.Add(Player.PlayerId, pos);
            HasArrow = true;
            SendRPC();
            Utils.NotifyRoles(SpecifySeer: Player, SpecifyTarget: Player);
            Logger.Info($"{Player.GetNameWithRole()}'s target: {target.GetNameWithRole()}", "Rabbit");

            LateTask.New(() =>
            {
                LocateArrow.Remove(Player.PlayerId, pos);
                HasArrow = false;
                SendRPC();
                Utils.NotifyRoles(SpecifySeer: Player, SpecifyTarget: Player);
            }, 5f, "Rabbit ShowArrow Empty");
        }

        private void SendRPC()
        {
            MessageWriter writer = Utils.CreateRPC(CustomRPC.SyncRabbit);
            writer.Write(Player.PlayerId);
            writer.Write(HasArrow);
            Utils.EndRPC(writer);
        }

        public void ReceiveRPC(bool hasArrow)
        {
            HasArrow = hasArrow;
        }
    }
}