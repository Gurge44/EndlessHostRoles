using System.Collections.Generic;

namespace EHR.Roles;

public class Talkative : IAddon
{
    public AddonTypes Type => AddonTypes.Harmful;

    private static OptionItem MaxMessagesPerMeeting;

    public static Dictionary<byte, int> NumMessagesThisMeeting = [];

    public void SetupCustomOption()
    {
        Options.SetupAdtRoleOptions(658050, CustomRoles.Talkative, canSetNum: true, teamSpawnOptions: true);

        MaxMessagesPerMeeting = new IntegerOptionItem(658060, "Talkative.MaxMessagesPerMeeting", new(1, 100, 1), 5, TabGroup.Addons)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Talkative])
            .SetValueFormat(OptionFormat.Times);
    }

    public static void OnMessageSend(PlayerControl player)
    {
        if (NumMessagesThisMeeting.TryGetValue(player.PlayerId, out int sent) && sent >= MaxMessagesPerMeeting.GetInt())
        {
            player.RpcGuesserMurderPlayer();
            Utils.SendMessage(string.Format(Translator.GetString("Talkative.Died"), player.PlayerId.ColoredPlayerName()), title: CustomRoles.Talkative.ToColoredString());
            return;
        }
        
        NumMessagesThisMeeting[player.PlayerId] = ++sent;
    }
}