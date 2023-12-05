using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Text;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public static class Postman
{
    private static readonly int Id = 641400;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem DieWhenTargetDies;

    public static bool IsFinished;
    public static byte Target;
    private static List<byte> wereTargets = [];

    public static void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Postman, 1, zeroOne: false);
        KillCooldown = FloatOptionItem.Create(Id + 10, "DeliverCooldown", new(0f, 180f, 2.5f), 10f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Postman])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Postman]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Postman]);
        DieWhenTargetDies = BooleanOptionItem.Create(Id + 12, "PostmanDiesWhenTargetDies", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Postman]);
    }
    public static void Init()
    {
        playerIdList = [];
        Target = byte.MaxValue;
        IsFinished = false;
        wereTargets = [];
    }
    public static void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        _ = new LateTask(SetNewTarget, 8f, "Set Postman First Target");

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public static bool IsEnable => playerIdList.Count > 0;
    public static void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public static void ApplyGameOptions(IGameOptions opt) => opt.SetVision(HasImpostorVision.GetBool());
    public static void SetNewTarget()
    {
        if (!IsEnable) return;
        byte tempTarget = byte.MaxValue;

        foreach (PlayerControl pc in Main.AllAlivePlayerControls)
        {
            if (wereTargets.Contains(pc.PlayerId) || pc.Is(CustomRoles.Postman)) continue;
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
        if (!IsEnable) return;
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
            killer.SetKillCooldown();
            killer.NotifyPostman(GetString("PostmanCorrectDeliver"));
        }
        else
        {
            killer.Suicide();
        }
    }

    public static void OnTargetDeath()
    {
        if (!IsEnable) return;
        if (IsFinished) return;
        var postman = Utils.GetPlayerById(playerIdList[0]);

        if (DieWhenTargetDies.GetBool())
        {
            postman.Suicide();
        }
        else
        {
            SetNewTarget();
            postman.NotifyPostman(GetString("PostmanTargetDied"));
        }
    }

    private static void NotifyPostman(this PlayerControl pc, string baseText)
    {
        if (!IsEnable) return;
        var sb = new StringBuilder();

        sb.Append("\r\n\r\n");
        sb.AppendLine(baseText);
        if (!IsFinished) sb.Append(string.Format(GetString("PostmanGetNewTarget"), Utils.GetPlayerById(Target).GetRealName()));
        else sb.Append(GetString("PostmanDone"));

        pc.Notify(sb.ToString());
    }

    public static string GetHudText(PlayerControl pc) => !IsFinished ? string.Format(GetString("PostmanTarget"), Utils.GetPlayerById(Target).GetRealName()) : GetString("PostmanDone");

    public static string TargetText => !IsFinished ? string.Format(GetString("PostmanTarget"), Utils.GetPlayerById(Target).GetRealName()) : "<color=#00ff00>✓</color>";
}
