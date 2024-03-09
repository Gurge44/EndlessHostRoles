using AmongUs.GameOptions;
using System.Collections.Generic;
using System.Text;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

public class Postman : RoleBase
{
    private const int Id = 641400;
    public static List<byte> playerIdList = [];

    private static OptionItem KillCooldown;
    public static OptionItem CanVent;
    private static OptionItem HasImpostorVision;
    private static OptionItem DieWhenTargetDies;

    public bool IsFinished;
    public byte Target;
    private List<byte> wereTargets = [];

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Postman);
        KillCooldown = FloatOptionItem.Create(Id + 10, "DeliverCooldown", new(0f, 180f, 2.5f), 10f, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Postman])
            .SetValueFormat(OptionFormat.Seconds);
        CanVent = BooleanOptionItem.Create(Id + 11, "CanVent", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Postman]);
        HasImpostorVision = BooleanOptionItem.Create(Id + 13, "ImpostorVision", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Postman]);
        DieWhenTargetDies = BooleanOptionItem.Create(Id + 12, "PostmanDiesWhenTargetDies", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Postman]);
    }

    public override void Init()
    {
        playerIdList = [];
        Target = byte.MaxValue;
        IsFinished = false;
        wereTargets = [];
    }

    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        _ = new LateTask(SetNewTarget, 8f, "Set Postman First Target");

        Target = byte.MaxValue;
        IsFinished = false;
        wereTargets = [];

        if (!AmongUsClient.Instance.AmHost) return;
        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }

    public override bool IsEnable => playerIdList.Count > 0;
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt, byte id) => opt.SetVision(HasImpostorVision.GetBool());
    public override bool CanUseKillButton(PlayerControl pc) => !IsFinished;
    public override bool CanUseImpostorVentButton(PlayerControl pc) => CanVent.GetBool();

    public static void CheckAndResetTargets(PlayerControl deadPc, bool isDeath = false)
    {
        foreach (var id in playerIdList)
        {
            if (Main.PlayerStates[id].Role is Postman { IsEnable: true } pm && pm.Target == deadPc.PlayerId)
            {
                if (isDeath && DieWhenTargetDies.GetBool())
                {
                    Utils.GetPlayerById(id).Suicide();
                }
                else
                {
                    pm.SetNewTarget();
                    if (!isDeath) continue;
                    pm.NotifyPostman(Utils.GetPlayerById(id), GetString("PostmanTargetDied"));
                }
            }
        }
    }

    void SetNewTarget()
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

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!IsEnable) return false;
        if (killer == null) return false;
        if (target == null) return false;
        if (IsFinished) return false;
        if (Target == byte.MaxValue)
        {
            SetNewTarget();
            return false;
        }

        if (target.PlayerId == Target)
        {
            SetNewTarget();
            killer.SetKillCooldown();
            NotifyPostman(killer, GetString("PostmanCorrectDeliver"));
        }
        else
        {
            killer.Suicide();
        }

        return false;
    }

    void NotifyPostman(PlayerControl pc, string baseText)
    {
        if (!IsEnable) return;
        var sb = new StringBuilder();

        sb.Append("\r\n\r\n");
        sb.AppendLine(baseText);
        sb.Append(!IsFinished ? string.Format(GetString("PostmanGetNewTarget"), Utils.GetPlayerById(Target).GetRealName()) : GetString("PostmanDone"));

        pc.Notify(sb.ToString());
    }

    public static string GetHudText(PlayerControl pc)
    {
        if (Main.PlayerStates[pc.PlayerId].Role is not Postman { IsEnable: true } pm) return string.Empty;
        return !pm.IsFinished ? string.Format(GetString("PostmanTarget"), Utils.GetPlayerById(pm.Target).GetRealName()) : GetString("PostmanDone");
    }

    public static string TargetText(byte id)
    {
        if (Main.PlayerStates[id].Role is not Postman { IsEnable: true } pm) return string.Empty;
        return !pm.IsFinished ? string.Format(GetString("PostmanTarget"), Utils.GetPlayerById(pm.Target).GetRealName()) : "<color=#00ff00>✓</color>";
    }
}