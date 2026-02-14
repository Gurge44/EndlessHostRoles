using System.Collections.Generic;
using System.Linq;

namespace EHR.Roles;

public abstract class CovenBase : RoleBase
{
    public enum NecronomiconReceivePriorities
    {
        First,
        Random,
        Never
    }

    protected abstract NecronomiconReceivePriorities NecronomiconReceivePriority { get; }

    public bool HasNecronomicon { get; set; }

    public virtual void OnReceiveNecronomicon() { }

    private static void GiveNecronomicon()
    {
        Dictionary<byte, PlayerState> psDict = Main.PlayerStates.Where(x => x.Value.Role is CovenBase { HasNecronomicon: false } coven && coven.NecronomiconReceivePriority != NecronomiconReceivePriorities.Never).ToDictionary(x => x.Key, x => x.Value);
        if (psDict.Count == 0) return;

        KeyValuePair<byte, PlayerState> receiver = psDict.Shuffle().OrderByDescending(x => !x.Value.IsDead).ThenBy(x => ((CovenBase)x.Value.Role).NecronomiconReceivePriority).First();

        var covenRole = (CovenBase)receiver.Value.Role;
        covenRole.HasNecronomicon = true;
        covenRole.OnReceiveNecronomicon();

        LateTask.New(() =>
        {
            string[] holders = Main.PlayerStates.Where(s => s.Value.Role is CovenBase { HasNecronomicon: true }).Select(s => s.Key.ColoredPlayerName()).ToArray();
            Main.EnumeratePlayerControls().Where(x => x.Is(Team.Coven)).Select(x => x.PlayerId).Without(receiver.Key).Do(x => Utils.SendMessage("\n", x, string.Format(Translator.GetString("PlayerReceivedTheNecronomicon"), receiver.Key.ColoredPlayerName(), Main.CovenColor, holders.Length, string.Join(", ", holders))));
            Utils.SendMessage("\n", receiver.Key, string.Format(Translator.GetString("YouReceivedTheNecronomicon"), Main.CovenColor), importance: MessageImportance.High);
        }, 12f, log: false);
    }

    //[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
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