using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using static EHR.Options;
using static EHR.Translator;
using static EHR.Utils;

namespace EHR.Roles;

public class Duellist : RoleBase
{
    private const int Id = 642850;
    private static List<byte> PlayerIdList = [];
    private static Dictionary<byte, byte> DuelPair = [];
    private static OptionItem SSCD;

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

            Main.Instance.StartCoroutine(Coroutine());

            System.Collections.IEnumerator Coroutine()
            {
                while (GameStates.IsInTask && duellist.IsAlive() && target.IsAlive()) yield return null;
                if (!GameStates.IsInTask) yield break;
                
                DuelPair.Remove(duellist.PlayerId);
                if (duellist.IsAlive()) duellist.TPToRandomVent();
                if (target.IsAlive()) target.TPToRandomVent();
            }
        }
        else
            duellist.Notify(GetString("TargetCannotBeTeleported"));

        return false;
    }
}