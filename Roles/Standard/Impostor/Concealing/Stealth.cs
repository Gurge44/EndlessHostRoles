using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;
using Hazel;
using UnityEngine;

namespace EHR.Roles;

public sealed class Stealth : RoleBase
{
    private static readonly LogHandler Logger = EHR.Logger.Handler(nameof(Stealth));

    private static OptionItem OptionExcludeImpostors;
    public static OptionItem OptionDarkenDuration;
    public static OptionItem UseLegacyVersion;
    private static OptionItem OptionBlindingRadius;
    public static OptionItem AbilityCooldown;

    public static bool On;
    public PlayerControl[] darkenedPlayers;
    private SystemTypes? darkenedRoom;

    private float darkenDuration;
    private bool excludeImpostors;
    private bool useLegacyVersion;
    private float blindingRadius;
    private int abilityCooldown;
    
    private PlayerControl StealthPC;
    
    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        StartSetup(641900)
            .AutoSetupOption(ref OptionExcludeImpostors, true)
            .AutoSetupOption(ref OptionDarkenDuration, 5f, new FloatValueRule(0.5f, 30f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref UseLegacyVersion, false)
            .AutoSetupOption(ref OptionBlindingRadius, 5f, new FloatValueRule(0.25f, 10f, 0.25f), OptionFormat.Multiplier)
            .AutoSetupOption(ref AbilityCooldown, 30, new IntegerValueRule(1, 120, 1), OptionFormat.Seconds);
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!base.OnCheckMurder(killer, target) || !useLegacyVersion) return true;

        IEnumerable<PlayerControl> playersToDarken = FindPlayersInSameRoom(target);

        if (playersToDarken == null)
        {
            Logger.Info("No players to darken");
            return true;
        }

        if (excludeImpostors) playersToDarken = playersToDarken.Where(player => !player.Is(CustomRoleTypes.Impostor));

        DarkenPlayers(playersToDarken);
        return true;
    }

    private IEnumerable<PlayerControl> FindPlayersInSameRoom(PlayerControl killedPlayer)
    {
        PlainShipRoom room = killedPlayer.GetPlainShipRoom();
        if (room == null) return null;

        Collider2D roomArea = room.roomArea;
        SystemTypes roomName = room.RoomId;
        RpcDarken(roomName);
        return Main.EnumerateAlivePlayerControls().Where(player => player != StealthPC && roomArea.bounds.Contains2D(player.Pos()));
    }

    private IEnumerable<PlayerControl> FindPlayersInRange()
    {
        var pos = StealthPC.Pos();
        var inRange = FastVector2.GetPlayersInRange(pos, blindingRadius).Without(StealthPC);
        if (excludeImpostors) inRange = inRange.Where(p => !p.Is(CustomRoleTypes.Impostor));
        return inRange;
    }

    private void DarkenPlayers(IEnumerable<PlayerControl> playersToDarken)
    {
        darkenedPlayers = playersToDarken.ToArray();

        foreach (PlayerControl player in darkenedPlayers)
        {
            Main.PlayerStates[player.PlayerId].IsBlackOut = true;
            player.MarkDirtySettings();
        }
        
        LateTask.New(() =>
        {
            if (ReportDeadBodyPatch.MeetingStarted || !GameStates.IsInTask || darkenedPlayers == null) return;
            ResetDarkenState(false);
        }, darkenDuration);

        if (!useLegacyVersion && Utils.DoRPC)
        {
            var w = Utils.CreateRPC(CustomRPC.SyncRoleData);
            w.Write(StealthPC.PlayerId);
            w.WritePacked(darkenedPlayers.Length);
            darkenedPlayers.Do(x => w.Write(x.PlayerId));
            Utils.EndRPC(w);
        }
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePhantomBasis.GetBool())
        {
            AURoleOptions.PhantomCooldown = abilityCooldown;
            AURoleOptions.PhantomDuration = 1f;
        }
        else if (!Options.UsePets.GetBool())
        {
            AURoleOptions.ShapeshifterCooldown = abilityCooldown;
            AURoleOptions.ShapeshifterDuration = 1f;
        }
    }

    public override void Init()
    {
        On = false;
        excludeImpostors = OptionExcludeImpostors.GetBool();
        darkenDuration = OptionDarkenDuration.GetFloat();
        useLegacyVersion = UseLegacyVersion.GetBool();
        blindingRadius = OptionBlindingRadius.GetFloat();
        abilityCooldown = AbilityCooldown.GetInt();
    }

    public override void Add(byte playerId)
    {
        On = true;
        StealthPC = playerId.GetPlayer();
    }

    public override void OnReportDeadBody()
    {
        if (AmongUsClient.Instance.AmHost) ResetDarkenState(true);
    }

    private void RpcDarken(SystemTypes? roomType)
    {
        Logger.Info($"Darkened room set to {roomType?.ToString() ?? "null"}");
        darkenedRoom = roomType;
        Utils.SendRPC(CustomRPC.SyncRoleData, StealthPC.PlayerId, (byte?)roomType ?? byte.MaxValue);
    }

    public void ReceiveRPC(MessageReader reader)
    {
        if (!useLegacyVersion)
        {
            darkenedPlayers = null;
            int count = reader.ReadPackedInt32();
            
            if (count > 0)
            {
                List<byte> ids = [];
                Loop.Times(count, _ => ids.Add(reader.ReadByte()));
                darkenedPlayers = ids.ToValidPlayers().ToArray();
            }

            return;
        }
        
        byte roomId = reader.ReadByte();
        darkenedRoom = roomId == byte.MaxValue ? null : (SystemTypes)roomId;
    }

    private void ResetDarkenState(bool meeting)
    {
        if (darkenedPlayers != null)
        {
            if (!meeting)
            {
                foreach (PlayerControl player in darkenedPlayers)
                {
                    Main.PlayerStates[player.PlayerId].IsBlackOut = false;
                    player.MarkDirtySettings();
                }
            }

            darkenedPlayers = null;
        }

        if (!useLegacyVersion)
        {
            Utils.SendRPC(CustomRPC.SyncRoleData, StealthPC.PlayerId, 0);
            
            if (!meeting && (!Options.UsePets.GetBool() || Options.UsePhantomBasis.GetBool()))
                StealthPC.RpcResetAbilityCooldown();
        }
        else
            RpcDarken(null);
        
        if (meeting) return;
        Utils.NotifyRoles(SpecifySeer: StealthPC);
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting || useLegacyVersion) return true;
        DarkenPlayers(FindPlayersInRange());
        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (useLegacyVersion) return true;
        DarkenPlayers(FindPlayersInRange());
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        if (useLegacyVersion) return;
        DarkenPlayers(FindPlayersInRange());
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl seen, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting || seer != StealthPC || seen != StealthPC || !darkenedRoom.HasValue) return base.GetSuffix(seer, seen, isForMeeting, isForHud);

        return string.Format(Translator.GetString("StealthDarkened"), TranslationController.Instance.GetString(darkenedRoom.Value));
    }
}