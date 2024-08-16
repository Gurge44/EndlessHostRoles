using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR;

public static class AntiBlackout
{
    private static Dictionary<byte, (bool isDead, bool Disconnected)> IsDeadCache = [];
    private static readonly LogHandler Logger = EHR.Logger.Handler("AntiBlackout");
    public static bool OverrideExiledPlayer = false;

    ///<summary>
    ///Whether to override the ejection process
    ///</summary>
    public static bool IsOverride => IsRequired && (IsSingleImpostor || Diff_CrewImp == 1 || Main.AliveImpostorCount == 1);

    ///<summary>
    ///Is there only one impostor?
    ///</summary>
    public static bool IsSingleImpostor => Main.RealOptionsData != null ? Main.RealOptionsData.GetInt(Int32OptionNames.NumImpostors) <= 1 : Main.NormalOptions.NumImpostors <= 1;

    ///<summary>
    ///Whether processing within AntiBlackout is required
    ///</summary>
    public static bool IsRequired => Options.NoGameEnd.GetBool() || Main.AllPlayerControls.Any(x => x.GetCountTypes() is not CountTypes.Crew and not CountTypes.Impostor and not CountTypes.OutOfGame);

    //|| Pirate.IsEnable;
    ///<summary>
    ///Difference between the number of non-impostors and the number of impostors
    ///</summary>
    public static int Diff_CrewImp
    {
        get
        {
            int numImpostors = 0;
            int numCrewmates = 0;
            foreach (PlayerControl pc in Main.AllPlayerControls)
            {
                if (pc.Data.Role.IsImpostor) numImpostors++;
                else numCrewmates++;
            }

            return numCrewmates - numImpostors;
        }
    }

    private static bool IsCached { get; set; }

    public static void SetIsDead(bool doSend = true, [CallerMemberName] string callerMethodName = "")
    {
        Logger.Info($"SetIsDead is called from {callerMethodName}");
        if (IsCached)
        {
            Logger.Info("Please run RestoreIsDead before running SetIsDead again.");
            return;
        }

        IsDeadCache.Clear();
        foreach (var info in GameData.Instance.AllPlayers)
        {
            if (info == null) continue;
            IsDeadCache[info.PlayerId] = (info.IsDead, info.Disconnected);
            info.IsDead = false;
            info.Disconnected = false;
        }

        IsCached = true;
        if (doSend) SendGameData();
    }

    public static void RestoreIsDead(bool doSend = true, [CallerMemberName] string callerMethodName = "")
    {
        Logger.Info($"RestoreIsDead is called from {callerMethodName}");
        foreach (var info in GameData.Instance.AllPlayers)
        {
            if (info == null) continue;
            if (IsDeadCache.TryGetValue(info.PlayerId, out var val))
            {
                info.IsDead = val.isDead;
                info.Disconnected = val.Disconnected;
            }
        }

        IsDeadCache.Clear();
        IsCached = false;
        if (doSend) SendGameData();
    }

    public static void SendGameData([CallerMemberName] string callerMethodName = "")
    {
        Logger.Info($"SendGameData is called from {callerMethodName}");
        foreach (var playerinfo in GameData.Instance.AllPlayers)
        {
            MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
            writer.StartMessage(5); //0x05 GameData
            {
                writer.Write(AmongUsClient.Instance.GameId);
                writer.StartMessage(1); //0x01 Data
                {
                    writer.WritePacked(playerinfo.NetId);
                    playerinfo.Serialize(writer, true);
                }
                writer.EndMessage();
            }
            writer.EndMessage();

            AmongUsClient.Instance.SendOrDisconnect(writer);
            writer.Recycle();
        }

        AmongUsClient.Instance.SendAllStreamedObjects();
    }

    public static void OnDisconnect(NetworkedPlayerInfo player)
    {
        // Execution conditions: client is host, IsDead is overwritten, player is disconnected
        if (!AmongUsClient.Instance.AmHost || !IsCached || !player.Disconnected) return;
        IsDeadCache[player.PlayerId] = (true, true);
        player.IsDead = player.Disconnected = false;
        SendGameData();
    }

    /*
        ///<summary>
        ///Run the code with IsDead temporarily restored to its original value
        ///<param name="action">Execution details</param>
        ///</summary>
        public static void TempRestore(Action action)
        {
            Logger.Info("==Temp Restore==");
            //Whether TempRestore was executed with IsDead overwritten
            bool before_IsCached = IsCached;
            try
            {
                if (before_IsCached) RestoreIsDead(doSend: false);
                action();
            }
            catch (Exception ex)
            {
                Logger.Warn("An exception occurred within AntiBlackout.TempRestore");
                Logger.Exception(ex);
            }
            finally
            {
                if (before_IsCached) SetIsDead(doSend: false);
                Logger.Info("==/Temp Restore==");
            }
        }
    */

    public static void Reset()
    {
        Logger.Info("==Reset==");
        IsDeadCache ??= [];
        IsDeadCache.Clear();
        IsCached = false;
    }
}