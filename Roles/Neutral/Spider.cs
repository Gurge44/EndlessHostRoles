using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Neutral;

public class Spider : RoleBase
{
    public static bool On;
    private static List<Spider> Instances = [];

    public static OptionItem AbilityCooldown;
    private static OptionItem WebTrapRange;
    private static OptionItem LowerVisionBy;
    private static OptionItem TrappedDuration;
    private static OptionItem KillCooldown;
    private static OptionItem CanVent;
    private static OptionItem ImpostorVision;

    public override bool IsEnable => On;

    private Dictionary<Vector2, Dictionary<byte, long>> Webs = [];
    private long LastNotifyTS;
    private bool NameDirty;
    private byte SpiderId;

    public override void SetupCustomOption()
    {
        StartSetup(657500)
            .AutoSetupOption(ref AbilityCooldown, 30f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref WebTrapRange, 2f, new FloatValueRule(0.05f, 10f, 0.05f), OptionFormat.Multiplier)
            .AutoSetupOption(ref LowerVisionBy, 0.5f, new FloatValueRule(0f, 3f, 0.05f), OptionFormat.Multiplier)
            .AutoSetupOption(ref TrappedDuration, 20f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref KillCooldown, 10f, new FloatValueRule(0f, 180f, 0.5f), OptionFormat.Seconds)
            .AutoSetupOption(ref CanVent, true)
            .AutoSetupOption(ref ImpostorVision, true);
    }

    public override void Init()
    {
        On = false;
        Instances = [];
    }

    public override void Add(byte playerId)
    {
        On = true;
        Webs = [];
        LastNotifyTS = 0;
        NameDirty = false;
        SpiderId = playerId;
        Instances.Add(this);
    }

    public override void Remove(byte playerId)
    {
        Instances.Remove(this);
    }

    public override void SetKillCooldown(byte id)
    {
        Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    }

    public override void ApplyGameOptions(IGameOptions opt, byte id)
    {
        opt.SetVision(ImpostorVision.GetBool());

        if (Options.UsePhantomBasis.GetBool() && Options.UsePhantomBasisForNKs.GetBool())
        {
            AURoleOptions.PhantomCooldown = AbilityCooldown.GetFloat();
            AURoleOptions.PhantomDuration = 1f;
        }
        else if (!Options.UsePets.GetBool())
        {
            AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetFloat();
            AURoleOptions.ShapeshifterDuration = 1f;
        }
    }

    public override bool CanUseImpostorVentButton(PlayerControl pc)
    {
        return CanVent.GetBool();
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (!shapeshifting) return true;
        UseAbility(shapeshifter);
        return false;
    }

    public override bool OnVanish(PlayerControl pc)
    {
        UseAbility(pc);
        return false;
    }

    public override void OnPet(PlayerControl pc)
    {
        UseAbility(pc);
    }

    void UseAbility(PlayerControl player)
    {
        Vector2 pos = player.Pos();
        if (Webs.Keys.Any(x => Vector2.Distance(x, pos) <= WebTrapRange.GetFloat() * 2f)) return;
        Webs[pos] = [];
        player.RPCPlayCustomSound("Line");
        LocateArrow.Add(player.PlayerId, pos);
        player.Notify(Translator.GetString("MarkDone"));
    }

    public override bool OnCheckMurder(PlayerControl killer, PlayerControl target)
    {
        return Webs.Any(x => x.Value.ContainsKey(target.PlayerId));
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        Main.PlayerStates[target.PlayerId].deathReason = PlayerState.DeathReason.Eaten;
        Webs.DoIf(x => x.Value.Remove(target.PlayerId), x => Utils.SendRPC(CustomRPC.SyncRoleData, SpiderId, 3, x.Key, target.PlayerId));
    }

    public override void OnCheckPlayerPosition(PlayerControl pc)
    {
        if (pc.PlayerId == SpiderId) return;
        
        Vector2 pos = pc.Pos();

        if (Webs.FindFirst(x => Vector2.Distance(x.Key, pos) <= WebTrapRange.GetFloat() && x.Value.TryAdd(pc.PlayerId, Utils.TimeStamp + TrappedDuration.GetInt()), out KeyValuePair<Vector2, Dictionary<byte, long>> kvp))
        {
            RPC.PlaySoundRPC(SpiderId, Sounds.TaskUpdateSound);
            pc.RPCPlayCustomSound("FlashBang");
            pc.MarkDirtySettings();
            Utils.SendRPC(CustomRPC.SyncRoleData, SpiderId, 1, kvp.Key, pc.PlayerId, kvp.Value.Last().Value);
        }
    }

    public override void OnFixedUpdate(PlayerControl pc)
    {
        long now = Utils.TimeStamp;
        List<Vector2> toRemove = [];
        
        foreach ((Vector2 pos, Dictionary<byte, long> trapped) in Webs)
        {
            if (trapped.Count > 0)
                NameDirty = true;
            
            if (trapped.Values.Min() <= now)
            {
                toRemove.Add(pos);
                LateTask.New(() =>
                {
                    trapped.Keys.ToValidPlayers().Do(x =>
                    {
                        RPC.PlaySoundRPC(x.PlayerId, Sounds.TaskComplete);
                        Main.AllPlayerSpeed[x.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                        x.MarkDirtySettings();
                    });
                }, 0.2f, log: false);
            }
        }
        
        toRemove.ForEach(x =>
        {
            Webs.Remove(x);
            LocateArrow.Remove(pc.PlayerId, x);
            Utils.SendRPC(CustomRPC.SyncRoleData, SpiderId, 2, x);
        });

        if (NameDirty && now != LastNotifyTS)
        {
            NameDirty = false;
            LastNotifyTS = now;
            Utils.NotifyRoles(SpecifySeer: pc, SpecifyTarget: pc);
        }
    }

    public override void OnReportDeadBody()
    {
        Webs.SetAllValues([]);
    }

    public static void OnAnyoneApplyGameOptions(IGameOptions opt, byte id)
    {
        foreach (Spider instance in Instances)
        {
            if (instance.Webs.Any(x => x.Value.ContainsKey(id)))
            {
                float vision = Main.DefaultCrewmateVision - LowerVisionBy.GetFloat();
                opt.SetFloat(FloatOptionNames.CrewLightMod, vision);
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, vision);
                Main.AllPlayerSpeed[id] = Main.MinSpeed;
            }
        }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                Vector2 pos = reader.ReadVector2();
                byte trappedId = reader.ReadByte();
                long releaseTS = long.Parse(reader.ReadString());
                Webs.TryAdd(pos, []);
                Webs[pos][trappedId] = releaseTS;
                break;
            case 2:
                Webs.Remove(reader.ReadVector2());
                break;
            case 3:
                Vector2 webPos = reader.ReadVector2();
                byte deadId = reader.ReadByte();
                Webs.GetValueOrDefault(webPos, []).Remove(deadId);
                break;
        }
    }

    public override string GetSuffix(PlayerControl seer, PlayerControl target, bool hud = false, bool meeting = false)
    {
        if (seer.PlayerId != SpiderId || seer.PlayerId != target.PlayerId || (seer.IsModdedClient() && !hud) || meeting) return string.Empty;
        return string.Join('\n', Webs.Where(x => x.Value.Count > 0).Select(x => $"{LocateArrow.GetArrow(seer, x.Key)} {x.Value.Count} ({Math.Max(0, x.Value.Values.Min() - Utils.TimeStamp)}s)"));
    }
}
