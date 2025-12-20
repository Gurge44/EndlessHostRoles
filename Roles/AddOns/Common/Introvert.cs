using System.Collections.Generic;
using System.Linq;
using EHR.Modules;
using Hazel;

namespace EHR.AddOns.Common;

public class Introvert : IAddon
{
    private static OptionItem Radius;
    private static OptionItem Time;

    public static Dictionary<byte, long> TeleportAwayDelays = [];
    public AddonTypes Type => AddonTypes.Mixed;

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(645740, CustomRoles.Introvert, canSetNum: true, teamSpawnOptions: true);

        Radius = new FloatOptionItem(645748, "Introvert.Radius", new(0.1f, 10f, 0.1f), 2f, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Introvert])
            .SetValueFormat(OptionFormat.Multiplier);

        Time = new IntegerOptionItem(645749, "Introvert.Time", new(0, 30, 1), 8, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Introvert])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public static void OnFixedUpdate(PlayerControl pc)
    {
        if (Main.HasJustStarted || !Main.IntroDestroyed || AntiBlackout.SkipTasks || ExileController.Instance)
        {
            TeleportAwayDelays = [];
            return;
        }

        Vector2 pos = pc.Pos();
        float radius = Radius.GetFloat();
        bool anyoneNear = Main.AllAlivePlayerControls.Without(pc).Any(x => Vector2.Distance(pos, x.Pos()) <= radius);

        if (!anyoneNear)
        {
            if (TeleportAwayDelays.Remove(pc.PlayerId))
            {
                Utils.SendRPC(CustomRPC.SyncIntrovert, 1, pc.PlayerId);
                Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
            }

            return;
        }

        if (!TeleportAwayDelays.TryGetValue(pc.PlayerId, out long endTS))
        {
            int time = Time.GetInt();

            if (time == 0)
            {
                pc.TPToRandomVent();
                return;
            }

            endTS = Utils.TimeStamp + time;
            TeleportAwayDelays[pc.PlayerId] = endTS;
            Utils.SendRPC(CustomRPC.SyncIntrovert, 2, pc.PlayerId, endTS);
            return;
        }

        if (endTS <= Utils.TimeStamp && !pc.inVent)
        {
            pc.RPCPlayCustomSound("Teleport");
            pc.TPToRandomVent();
            TeleportAwayDelays.Remove(pc.PlayerId);
            Utils.SendRPC(CustomRPC.SyncIntrovert, 1, pc.PlayerId);
        }

        Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
    }

    public static void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
            {
                byte id = reader.ReadByte();
                TeleportAwayDelays.Remove(id);
                break;
            }
            case 2:
            {
                byte id = reader.ReadByte();
                long endTS = long.Parse(reader.ReadString());
                TeleportAwayDelays[id] = endTS;
                break;
            }
        }
    }

    public static string GetSelfSuffix(PlayerControl seer)
    {
        if (!seer.IsAlive() || !TeleportAwayDelays.TryGetValue(seer.PlayerId, out long endTS)) return string.Empty;
        return string.Format(Translator.GetString("Introvert.Suffix"), endTS - Utils.TimeStamp);
    }
}