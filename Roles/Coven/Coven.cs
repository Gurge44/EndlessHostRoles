using System.Linq;
using HarmonyLib;

namespace EHR.Coven;

public abstract class Coven : RoleBase
{
    public enum NecronomiconReceivePriorities
    {
        First,
        Random,
        Never
    }

    protected abstract NecronomiconReceivePriorities NecronomiconReceivePriority { get; }

    protected bool HasNecronomicon { get; set; }

    protected virtual void OnReceiveNecronomicon() { }

    private static void GiveNecronomicon()
    {
        var covenPlayers = Main.PlayerStates.Values.Select(x => x.Role as Coven).Where(x => x != null).ToList();
        covenPlayers.RemoveAll(x => x.HasNecronomicon || x.NecronomiconReceivePriority == NecronomiconReceivePriorities.Never);

        var receiver = covenPlayers.Find(x => x.NecronomiconReceivePriority == NecronomiconReceivePriorities.First) ?? covenPlayers.RandomElement();
        receiver.HasNecronomicon = true;
        receiver.OnReceiveNecronomicon();
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
    public static class CovenMeetingStartPatch
    {
        public static int MeetingNum;

        public static void Postfix()
        {
            if (AmongUsClient.Instance.AmHost && ++MeetingNum >= Options.CovenReceiveNecronomiconAfterNumMeetings.GetInt())
                GiveNecronomicon();
        }
    }
}