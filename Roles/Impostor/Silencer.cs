using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Crewmate;
using EHR.Modules;
using Epic.OnlineServices.PlayerDataStorage;
using Hazel;
using UnityEngine;
using static EHR.Options;

namespace EHR.Impostor;

public class Silencer : RoleBase
{
    private const int Id = 643050;
    private static List<byte> PlayerIdList = [];

    public static OptionItem SkillCooldown;
    public static OptionItem SilenceMode;
    public static OptionItem MaxPlayersAliveForSilencedToVote;

    public static List<byte> ForSilencer = [];

    private static int LocalPlayerTotalSilences;

    private static readonly string[] SilenceModes =
    [
        "EKill",
        "Shapeshift",
        "Vanish"
    ];

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupSingleRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Silencer);

        SkillCooldown = new FloatOptionItem(Id + 5, "AbilityCooldown", new(2.5f, 60f, 0.5f), 30f, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Silencer])
            .SetValueFormat(OptionFormat.Seconds);

        SilenceMode = new StringOptionItem(Id + 4, "SilenceMode", SilenceModes, 1, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Silencer]);
        
        MaxPlayersAliveForSilencedToVote = new IntegerOptionItem(Id + 6, "MaxPlayersAliveForSilencedToVote", new(1, 15, 1), 5, TabGroup.ImpostorRoles)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Silencer])
            .SetValueFormat(OptionFormat.Players);
    }

    public override void Init()
    {
        PlayerIdList = [];
        ForSilencer = [];

        LocalPlayerTotalSilences = 0;
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
        switch (SilenceMode.GetValue())
        {
            case 1:
                AURoleOptions.ShapeshifterCooldown = SkillCooldown.GetFloat();
                AURoleOptions.ShapeshifterDuration = 1f;
                break;
            case 2:
                AURoleOptions.PhantomCooldown = SkillCooldown.GetFloat();
                AURoleOptions.PhantomDuration = 1f;
                break;
        }
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (SilenceMode.GetValue() >= 1) return true;

        return killer.CheckDoubleTrigger(target, () =>
        {
            ForSilencer = [target.PlayerId];
            Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, 1, target.PlayerId);
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            killer.SetKillCooldown(3f);

            if (killer.AmOwner)
            {
                LocalPlayerTotalSilences++;
                if (LocalPlayerTotalSilences >= 5) Achievements.Type.Censorship.Complete();

                if (target.Is(CustomRoles.Snitch) && Snitch.IsExposed.TryGetValue(target.PlayerId, out bool exposed) && exposed)
                    Achievements.Type.YouWontTellAnyone.Complete();
            }
        });
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (SilenceMode.GetValue() == 1&& shapeshifter.PlayerId != target.PlayerId)
        {
            ForSilencer = [target.PlayerId];
            Utils.SendRPC(CustomRPC.SyncRoleData, shapeshifter.PlayerId, 1, target.PlayerId);

            if (shapeshifter.AmOwner)
            {
                LocalPlayerTotalSilences++;
                if (LocalPlayerTotalSilences >= 5) Achievements.Type.Censorship.Complete();

                if (target.Is(CustomRoles.Snitch) && Snitch.IsExposed.TryGetValue(target.PlayerId, out bool exposed) && exposed)
                    Achievements.Type.YouWontTellAnyone.Complete();
            }
        }

        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        if (SilenceMode.GetValue() == 2)
        {
            var pos = pc.Pos();
            var killRange = NormalGameOptionsV10.KillDistances[Mathf.Clamp(Main.NormalOptions.KillDistance, 0, 2)];
            var nearPlayers = Main.AllAlivePlayerControls.Without(pc).Select(x => (pc: x, distance: Vector2.Distance(x.Pos(), pos))).Where(x => x.distance <= killRange).ToArray();
            PlayerControl target = nearPlayers.Length == 0 ? null : nearPlayers.MinBy(x => x.distance).pc;
            if (target == null) return false;
            
            ForSilencer = [target.PlayerId];
            Utils.SendRPC(CustomRPC.SyncRoleData, pc.PlayerId, 1, target.PlayerId);
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: target);

            if (pc.AmOwner)
            {
                LocalPlayerTotalSilences++;
                if (LocalPlayerTotalSilences >= 5) Achievements.Type.Censorship.Complete();

                if (target.Is(CustomRoles.Snitch) && Snitch.IsExposed.TryGetValue(target.PlayerId, out bool exposed) && exposed)
                    Achievements.Type.YouWontTellAnyone.Complete();
            }
        }

        return false;
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                ForSilencer = [reader.ReadByte()];
                break;
            case 2:
                ForSilencer = [];
                break;
        }
    }

    public override void AfterMeetingTasks()
    {
        if (ForSilencer.Count == 0) return;
        ForSilencer.Clear();
        PlayerIdList.ForEach(x => Utils.SendRPC(CustomRPC.SyncRoleData, x, 2));
    }
}