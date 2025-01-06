using System.Linq;

namespace EHR.Coven;

public abstract class Coven : RoleBase
{
    public enum NecronomiconReceivePriorities
    {
        First,
        Random,
        Never
    }

    public abstract NecronomiconReceivePriorities NecronomiconReceivePriority { get; }

    public bool HasNecronomicon { get; set; }

    public virtual void OnReceiveNecronomicon() { }

    public static void GiveNecronomicon()
    {
        var covenPlayers = Main.PlayerStates.Values.Select(x => x.Role as Coven).Where(x => x != null).ToList();
        covenPlayers.RemoveAll(x => x.HasNecronomicon || x.NecronomiconReceivePriority == NecronomiconReceivePriorities.Never);

        var receiver = covenPlayers.Find(x => x.NecronomiconReceivePriority == NecronomiconReceivePriorities.First) ?? covenPlayers.RandomElement();
        receiver.HasNecronomicon = true;
        receiver.OnReceiveNecronomicon();
    }
}