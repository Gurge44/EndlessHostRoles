using AmongUs.GameOptions;
using Hazel;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TOHE.Modules;
using TOHE.Roles.Neutral;

namespace TOHE;

public static class AntiBlackout
{
    ///<summary>
    ///Whether to override the ejection process
    ///</summary>
    public static bool OverrideExiledPlayer => IsRequired && (IsSingleImpostor || Diff_CrewImp == 1);
    ///<summary>
    ///Is there only one impostor?
    ///</summary>
    public static bool IsSingleImpostor => Main.RealOptionsData != null ? Main.RealOptionsData.GetInt(Int32OptionNames.NumImpostors) <= 1 : Main.NormalOptions.NumImpostors <= 1;
    ///<summary>
    ///Whether processing within AntiBlackout is required
    ///</summary>
    public static bool IsRequired => Options.NoGameEnd.GetBool()
        || Jackal.IsEnable || Pelican.IsEnable || Magician.IsEnable || Enderman.IsEnable
        || Gamer.IsEnable || BloodKnight.IsEnable || WeaponMaster.IsEnable
        || Succubus.IsEnable || Poisoner.IsEnable || Reckless.IsEnable || Bubble.IsEnable
        || Infectious.IsEnable || Juggernaut.IsEnable || HeadHunter.IsEnable
        || Ritualist.IsEnable || Virus.IsEnable || Vengeance.IsEnable || Doppelganger.IsEnable
        || Wraith.IsEnable || HexMaster.IsEnable || Imitator.IsEnable() || Mycologist.IsEnable
        || Traitor.IsEnable || Pickpocket.IsEnable || Werewolf.IsEnable || Sprayer.IsEnable
        || NSerialKiller.IsEnable || RuthlessRomantic.IsEnable || Hookshot.IsEnable
        || Maverick.IsEnable || Jinx.IsEnable || Eclipse.IsEnable || PlagueDoctor.IsEnable
        || Medusa.IsEnable || Spiritcaller.IsEnable || Pyromaniac.IsEnable
        || PlagueBearer.IsEnable || CustomRoles.Sidekick.RoleExist(true)
        || CustomRoles.Pestilence.RoleExist(true);
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
    public static bool IsCached { get; private set; }
    private static Dictionary<byte, (bool isDead, bool Disconnected)> isDeadCache = [];
    private readonly static LogHandler logger = Logger.Handler("AntiBlackout");

    public static void SetIsDead(bool doSend = true, [CallerMemberName] string callerMethodName = "")
    {
        logger.Info($"SetIsDead is called from {callerMethodName}");
        if (IsCached)
        {
            logger.Info("Please run RestoreIsDead before running SetIsDead again.");
            return;
        }
        isDeadCache.Clear();
        foreach (var info in GameData.Instance.AllPlayers)
        {
            if (info == null) continue;
            isDeadCache[info.PlayerId] = (info.IsDead, info.Disconnected);
            info.IsDead = false;
            info.Disconnected = false;
        }
        IsCached = true;
        if (doSend) SendGameData();
    }
    public static void RestoreIsDead(bool doSend = true, [CallerMemberName] string callerMethodName = "")
    {
        logger.Info($"RestoreIsDead is called from {callerMethodName}");
        foreach (var info in GameData.Instance.AllPlayers)
        {
            if (info == null) continue;
            if (isDeadCache.TryGetValue(info.PlayerId, out var val))
            {
                info.IsDead = val.isDead;
                info.Disconnected = val.Disconnected;
            }
        }
        isDeadCache.Clear();
        IsCached = false;
        if (doSend) SendGameData();
    }

    public static void SendGameData([CallerMemberName] string callerMethodName = "")
    {
        logger.Info($"SendGameData is called from {callerMethodName}");
        MessageWriter writer = MessageWriter.Get(SendOption.Reliable);
        // {} is for readability.
        writer.StartMessage(5); //0x05 GameData
        {
            writer.Write(AmongUsClient.Instance.GameId);
            writer.StartMessage(1); //0x01 Data
            {
                writer.WritePacked(GameData.Instance.NetId);
                GameData.Instance.Serialize(writer, true);
            }
            writer.EndMessage();
        }
        writer.EndMessage();

        AmongUsClient.Instance.SendOrDisconnect(writer);
        writer.Recycle();
    }
    public static void OnDisconnect(GameData.PlayerInfo player)
    {
        // Execution conditions: client is host, IsDead is overwritten, player is disconnected
        if (!AmongUsClient.Instance.AmHost || !IsCached || !player.Disconnected) return;
        isDeadCache[player.PlayerId] = (true, true);
        player.IsDead = player.Disconnected = false;
        SendGameData();
    }

    ///<summary>
    ///Run the code with IsDead temporarily restored to its original value
    ///<param name="action">Execution details</param>
    ///</summary>
    public static void TempRestore(Action action)
    {
        logger.Info("==Temp Restore==");
        //Whether TempRestore was executed with IsDead overwritten
        bool before_IsCached = IsCached;
        try
        {
            if (before_IsCached) RestoreIsDead(doSend: false);
            action();
        }
        catch (Exception ex)
        {
            logger.Warn("An exception occurred within AntiBlackout.TempRestore");
            logger.Exception(ex);
        }
        finally
        {
            if (before_IsCached) SetIsDead(doSend: false);
            logger.Info("==/Temp Restore==");
        }
    }

    public static void Reset()
    {
        logger.Info("==Reset==");
        isDeadCache ??= [];
        isDeadCache.Clear();
        IsCached = false;
    }
}