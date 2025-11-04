using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral;

public class Beehive : RoleBase
{
    public static bool On;

    private static OptionItem Distance;
    private static OptionItem Time;
    private static OptionItem StingCooldown;
    private static OptionItem StungPlayersDieOnMeeting;
    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem HasImpostorVision;

    private byte BeehiveId;
    public Dictionary<byte, (long TimeStamp, Vector2 InitialPosition)> StungPlayers = [];

    public override bool IsEnable => On;

    public override void SetupCustomOption()
    {
        var id = 647350;
        Options.SetupRoleOptions(id++, TabGroup.NeutralRoles, CustomRoles.Beehive);

        Distance = new FloatOptionItem(++id, "Beehive.Distance", new(0f, 20f, 0.5f), 15f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beehive]);

        Time = new FloatOptionItem(++id, "Beehive.Time", new(0f, 30f, 0.5f), 10f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beehive])
            .SetValueFormat(OptionFormat.Seconds);

        StingCooldown = new FloatOptionItem(++id, "Beehive.StingCooldown", new(0f, 180f, 0.5f), 5f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beehive])
            .SetValueFormat(OptionFormat.Seconds);

        StungPlayersDieOnMeeting = new BooleanOptionItem(++id, "Beehive.StungPlayersDieOnMeeting", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beehive]);

        KillCooldown = new FloatOptionItem(++id, "KillCooldown", new(0f, 180f, 0.5f), 22.5f, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beehive])
            .SetValueFormat(OptionFormat.Seconds);

        CanVent = new BooleanOptionItem(++id, "CanVent", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beehive]);

        HasImpostorVision = new BooleanOptionItem(++id, "ImpostorVision", true, TabGroup.NeutralRoles)
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Beehive]);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        BeehiveId = playerId;
        StungPlayers = [];
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(HasImpostorVision.GetBool());
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        if (!base.OnCheckMurder(killer, target)) return false;

        return killer.CheckDoubleTrigger(target, () =>
        {
            Vector2 pos = target.Pos();
            StungPlayers[target.PlayerId] = (Utils.TimeStamp, pos);
            killer.SetKillCooldown(StingCooldown.GetFloat());
            Utils.NotifyRoles(SpecifySeer: killer, SpecifyTarget: target);
            Utils.SendRPC(CustomRPC.SyncRoleData, BeehiveId, 1, target.PlayerId, pos);
            target.Notify(string.Format(Translator.GetString("Beehive.Notify"), Math.Round(Distance.GetFloat(), 1), Math.Round(Time.GetFloat(), 1)), 8f);
        });
    }

    public override void OnGlobalFixedUpdate(PlayerControl pc, bool lowLoad)
    {
        if (lowLoad || !pc.IsAlive() || !GameStates.IsInTask || ExileController.Instance) return;

        if (StungPlayers.TryGetValue(pc.PlayerId, out (long TimeStamp, Vector2 InitialPosition) sp))
        {
            if (Utils.TimeStamp - sp.TimeStamp >= Time.GetFloat())
            {
                StungPlayers.Remove(pc.PlayerId);
                Utils.SendRPC(CustomRPC.SyncRoleData, BeehiveId, 2, pc.PlayerId);

                if (Vector2.Distance(pc.Pos(), sp.InitialPosition) < Distance.GetFloat())
                {
                    pc.Suicide(deathReason: PlayerState.DeathReason.Stung, realKiller: Utils.GetPlayerById(BeehiveId));

                    if (pc.AmOwner)
                        Achievements.Type.OutOfTime.Complete();
                }
            }

            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }
    }

    public override void OnReportDeadBody()
    {
        if (StungPlayersDieOnMeeting.GetBool())
        {
            StungPlayers.Keys.Select(x => Utils.GetPlayerById(x)).DoIf(
                x => x != null && x.IsAlive(),
                x => x.Suicide(deathReason: PlayerState.DeathReason.Stung, realKiller: Utils.GetPlayerById(BeehiveId)));
        }

        StungPlayers.Clear();
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                StungPlayers[reader.ReadByte()] = (Utils.TimeStamp, reader.ReadVector2());
                break;
            case 2:
                StungPlayers.Remove(reader.ReadByte());
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != target.PlayerId || meeting || hud || !StungPlayers.TryGetValue(seer.PlayerId, out (long TimeStamp, Vector2 InitialPosition) sp)) return string.Empty;

        double walked = Math.Round(Vector2.Distance(seer.Pos(), sp.InitialPosition), 1);
        double distance = Math.Round(Distance.GetFloat(), 1);
        long time = Time.GetInt() - (Utils.TimeStamp - sp.TimeStamp);
        string color = walked >= distance ? "<#00ffa5>" : "<#ffa500>";
        string color2 = walked >= distance ? "<#00ffff>" : "<#ffff00>";
        return $"{color}{walked}</color>{color2}/{distance}</color> <#ffffff>({time}s)</color>";
    }
}