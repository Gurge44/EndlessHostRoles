using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public static class Postman
{
    private static readonly int Id = 641400;
    public static List<byte> playerIdList = new();

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem DieWhenTargetDies;

    public static bool IsFinished;
    public static byte Target;
    private static List<byte> wereTargets = new();

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Postman, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "DeliverCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Postman])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Postman]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", true, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Postman]);
        DieWhenTargetDies = BooleanOptionItem.Create(Id + 12, "PostmanDiesWhenTargetDies", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Postman]);
    }
    public static void Init()
    {
        playerIdList = new();
        Target = byte.MaxValue;
        IsFinished = false;
        wereTargets = new();
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        _ = new LateTask(SetNewTarget, 8f, "Set Postman First Target");

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Any();
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
    public static void SetNewTarget()
    {
        byte tempTarget = byte.MaxValue;

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (wereTargets.Contains(pc.PlayerId)) continue;
            tempTarget = pc.PlayerId;
            break;
        }

        if (tempTarget == byte.MaxValue)
        {
            IsFinished = true;
            Target = byte.MaxValue;
            return;
        }

        Target = tempTarget;
        wereTargets.Add(Target);
    }

    public static void OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (killer == null) return;
        if (target == null) return;
        if (IsFinished) return;
        if (Target == byte.MaxValue)
        {
            SetNewTarget();
            return;
        }

        if (target.PlayerId == Target)
        {
            SetNewTarget();
            killer.NotifyPostman(GetString("PostmanCorrectDeliver"));
        }
        else
        {
            killer.Kill(killer);
            Main.PlayerStates[killer.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
        }
    }

    public static void OnTargetDeath()
    {
        if (IsFinished) return;
        var postman = Utils.GetPlayerById(playerIdList[0]);

        if (DieWhenTargetDies.GetBool())
        {
            postman.Kill(postman);
            Main.PlayerStates[postman.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
        }
        else
        {
            SetNewTarget();
            postman.NotifyPostman(GetString("PostmanTargetDied"));
        }
    }

    private static void NotifyPostman(this PlayerControl pc, string baseText)
    {
        var sb = new StringBuilder();

        sb.AppendLine(baseText);
        if (!IsFinished) sb.AppendLine(string.Format(GetString("PostmanGetNewTarget"), Utils.GetPlayerById(Target).GetRealName()));
        else sb.AppendLine(GetString("PostmanDone"));

        pc.Notify(sb.ToString());
    }

    public static string GetHudText(PlayerControl pc)
    {
        var sb = new StringBuilder();

        if (!IsFinished) sb.AppendLine(string.Format(GetString("PostmanTarget"), Utils.GetPlayerById(Target).GetRealName()));
        else sb.AppendLine(GetString("PostmanDone"));

        return sb.ToString();
    }

    public static string GetProgressText(byte playerId)
    {
        return !IsFinished ? string.Format(GetString("PostmanTarget"), Utils.GetPlayerById(Target).GetRealName()) : "<color=#00ff00>✓</color>";
    }
}
