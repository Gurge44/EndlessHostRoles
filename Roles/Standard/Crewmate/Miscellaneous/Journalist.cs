using System;
using System.Collections.Generic;
using System.Linq;

namespace EHR.Roles;

public class Journalist : RoleBase
{
    public static bool On;
    private byte JournalistId;
    private List<string> Notes;
    private bool Sent;

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(647450);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Notes = [];
        Sent = false;
        JournalistId = playerId;
    }

    public static void OnReceiveCommand(PlayerControl pc, string[] args)
    {
        if (Main.PlayerStates[pc.PlayerId].Role is not Journalist journalist) return;

        var error = string.Empty;

        try
        {
            switch (args[1])
            {
                case "add":
                    journalist.Notes.Add(string.Join(' ', args[2..]));
                    break;
                case "remove":
                    if (args.Length == 2 || !int.TryParse(args[2], out int index))
                        index = journalist.Notes.Count;
                    
                    index--;
                    journalist.Notes.RemoveAt(index);
                    break;
                case "clear":
                    journalist.Notes.Clear();
                    break;
                case "view":
                    Utils.SendMessage(string.Join('\n', journalist.Notes.Select((x, i) => $"{i + 1}. {x}")), pc.PlayerId, Translator.GetString("JournalistNotesTitle"));
                    break;
                default:
                    error = Translator.GetString("JournalistError.InvalidArgument");
                    break;
            }
        }
        catch (Exception e)
        {
            string usage = Translator.GetString("Journalist.CommandUsage");

            switch (e)
            {
                case IndexOutOfRangeException: // Not enough arguments
                    error = $"{Translator.GetString("JournalistError.IndexOutOfRange")}\n{usage}";
                    break;
                case ArgumentOutOfRangeException: // Invalid index for remove
                    error = $"{Translator.GetString("JournalistError.ArgumentOutOfRange")}\n{usage}";
                    break;
                default:
                    error = $"{Translator.GetString("JournalistError.Unknown")}\n{usage}";
                    Utils.ThrowException(e);
                    break;
            }
        }

        if (error != string.Empty) Utils.SendMessage(error, pc.PlayerId);
    }

    public override void OnReportDeadBody()
    {
        if (Sent || JournalistId.GetPlayer()?.IsAlive() == true) return;

        LateTask.New(() => Utils.SendMessage(string.Join('\n', Notes), title: Translator.GetString("JournalistNotesTitle"), importance: MessageImportance.High), 10f, "Send Journalist Notes");
        Sent = true;
    }
}