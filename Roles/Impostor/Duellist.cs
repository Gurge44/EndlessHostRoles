using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using EHR.Neutral;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Impostor;

public class Duellist : RoleBase
{
    private const int Id = 642850;
    private static List<byte> PlayerIdList = [];
    private static Dictionary<byte, byte> DuelPair = [];
    private static OptionItem SSCD;

    private int Count;

    public override bool IsEnable => PlayerIdList.Count > 0 || Randomizer.Exists;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Duellist);

        SSCD = new FloatOptionItem(Id + 5, "ShapeshiftCooldown", new(0f, 60f, 0.5f), 15f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Duellist])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
        DuelPair = [];
    }

    public override void Add(byte playerId)
    {
        PlayerIdList.Add(playerId);
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        AURoleOptions.ShapeshifterCooldown = SSCD.GetFloat();
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton?.OverrideText(GetString("SoulCatcherButtonText"));
    }

    public override bool OnShapeshift(PlayerControl duellist, PlayerControl target, bool shapeshifting)
    {
        if (!IsEnable) return false;

        if (duellist == null || target == null) return false;

        Vector2 pos = Pelican.GetBlackRoomPS();

        if (target.TP(pos))
        {
            if (Main.KillTimers[duellist.PlayerId] < 1f) duellist.SetKillCooldown(1f); // Give the other player a chance to kill

            if (DuelPair.TryGetValue(duellist.PlayerId, out byte previousTargetId))
            {
                PlayerControl previousTarget = GetPlayerById(previousTargetId);
                if (previousTarget != null) previousTarget.TPToRandomVent();
            }

            duellist.TP(pos);
            DuelPair[duellist.PlayerId] = target.PlayerId;

            duellist.RPCPlayCustomSound("Teleport");
            target.RPCPlayCustomSound("Teleport");
        }
        else
            duellist.Notify(GetString("TargetCannotBeTeleported"));

        return false;
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        if (DuelPair.Count == 0) return;

        if (pc.IsAlive() && Count++ < 40) return;

        Count = 0;

        foreach (KeyValuePair<byte, byte> pair in DuelPair)
        {
            PlayerControl duellist = GetPlayerById(pair.Key);
            PlayerControl target = GetPlayerById(pair.Value);
            bool DAlive = duellist.IsAlive();
            bool TAlive = target.IsAlive();

            switch (DAlive)
            {
                case false when !TAlive:
                    DuelPair.Remove(pair.Key);
                    break;
                case true when !TAlive:
                    DuelPair.Remove(pair.Key);
                    LateTask.New(() => duellist.TPToRandomVent(), 0.5f, log: false);
                    break;
                case false:
                    DuelPair.Remove(pair.Key);
                    LateTask.New(() => target.TPToRandomVent(), 0.5f, log: false);
                    break;
            }
        }
    }
}