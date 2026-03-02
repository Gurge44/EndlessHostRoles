using System.Collections.Generic;
using AmongUs.GameOptions;
using EHR.Modules;
using EHR.Modules.Extensions;

namespace EHR.Roles;

using static Options;

public class Addict : RoleBase
{
    private const int Id = 5200;
    private static List<byte> PlayerIdList = [];

    private static OptionItem VentCooldown;
    private static OptionItem TimeLimit;
    private static OptionItem ImmortalTimeAfterVent;
    private static OptionItem SpeedWhileImmortal;
    private static OptionItem FreezeTimeAfterImmortal;

    private CountdownTimer ImmortalTimer;
    private CountdownTimer SuicideTimer;
    private byte AddictId;

    public override bool IsEnable => PlayerIdList.Count > 0;

    public override void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Addict);

        VentCooldown = new FloatOptionItem(Id + 11, "VentCooldown", new(5f, 70f, 1f), 40f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
            .SetValueFormat(OptionFormat.Seconds);

        TimeLimit = new FloatOptionItem(Id + 12, "MercenaryLimit", new(5f, 75f, 1f), 45f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
            .SetValueFormat(OptionFormat.Seconds);

        ImmortalTimeAfterVent = new FloatOptionItem(Id + 13, "AddictInvulnerbilityTimeAfterVent", new(0f, 30f, 1f), 10f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
            .SetValueFormat(OptionFormat.Seconds);

        SpeedWhileImmortal = new FloatOptionItem(Id + 14, "AddictSpeedWhileInvulnerble", new(0.25f, 5f, 0.25f), 1.75f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
            .SetValueFormat(OptionFormat.Multiplier);

        FreezeTimeAfterImmortal = new FloatOptionItem(Id + 15, "AddictFreezeTimeAfterInvulnerbility", new(0f, 10f, 1f), 3f, TabGroup.CrewmateRoles).SetParent(CustomRoleSpawnChances[CustomRoles.Addict])
            .SetValueFormat(OptionFormat.Seconds);
    }

    public override void Init()
    {
        PlayerIdList = [];
    }

    public override void Add(byte playerId)
    {
        AddictId = playerId;
        PlayerIdList.Add(playerId);
        SuicideTimer = new CountdownTimer(TimeLimit.GetFloat(), () =>
        {
            SuicideTimer = null;
            
            var player = playerId.GetPlayer();
            if (player == null || !player.IsAlive()) return;
            
            player.Suicide();

            if (player.AmOwner)
                Achievements.Type.OutOfTime.Complete();
        }, onTick: () =>
        {
            var player = playerId.GetPlayer();
            
            if (player == null || !player.IsAlive())
            {
                SuicideTimer.Dispose();
                SuicideTimer = null;
                return;
            }

            if (SuicideTimer.Remaining is { Minutes: 0, Seconds: 8 })
                player.Notify(Translator.GetString("AddictWarning"), 8f);
        }, onCanceled: () => SuicideTimer = null);
        ImmortalTimer = null;
    }

    public override void Remove(byte playerId)
    {
        PlayerIdList.Remove(playerId);
    }

    public override void OnReportDeadBody()
    {
        PlayerIdList.ForEach(x => Main.AllPlayerSpeed[x] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod));
    }

    public override void AfterMeetingTasks()
    {
        SuicideTimer?.Dispose();
        SuicideTimer = new CountdownTimer(TimeLimit.GetFloat(), () =>
        {
            SuicideTimer = null;
            
            var player = AddictId.GetPlayer();
            if (player == null || !player.IsAlive()) return;
            
            player.Suicide();

            if (player.AmOwner)
                Achievements.Type.OutOfTime.Complete();
        }, onTick: () =>
        {
            var player = AddictId.GetPlayer();
            
            if (player == null || !player.IsAlive())
            {
                SuicideTimer.Dispose();
                SuicideTimer = null;
                return;
            }

            if (SuicideTimer.Remaining is { Minutes: 0, Seconds: 8 })
                player.Notify(Translator.GetString("AddictWarning"), 8f);
        }, onCanceled: () => SuicideTimer = null);
        ImmortalTimer?.Dispose();
        ImmortalTimer = null;
    }

    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        return ImmortalTimer == null;
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.EngineerCooldown = VentCooldown.GetFloat();
        AURoleOptions.EngineerInVentMaxTime = 1f;
    }

    public override void OnEnterVent(PlayerControl pc, Vent vent)
    {
        SuicideTimer?.Dispose();
        SuicideTimer = new CountdownTimer(TimeLimit.GetFloat(), () =>
        {
            pc.Suicide();
            SuicideTimer = null;

            if (pc.AmOwner)
                Achievements.Type.OutOfTime.Complete();
        }, onTick: () =>
        {
            if (pc == null || !pc.IsAlive())
            {
                SuicideTimer.Dispose();
                SuicideTimer = null;
                return;
            }
            
            if (SuicideTimer.Remaining is { Minutes: 0, Seconds: 8 })
                pc.Notify(Translator.GetString("AddictWarning"), 8f);
        }, onCanceled: () => SuicideTimer = null);
        ImmortalTimer?.Dispose();
        ImmortalTimer = new CountdownTimer(ImmortalTimeAfterVent.GetFloat(), () =>
        {
            ImmortalTimer = null;
            if (FreezeTimeAfterImmortal.GetFloat() > 0) AddictGetDown(pc);
        }, onCanceled: () => ImmortalTimer = null);

        Main.AllPlayerSpeed[pc.PlayerId] = SpeedWhileImmortal.GetFloat();
        pc.MarkDirtySettings();
    }

    private static void AddictGetDown(PlayerControl addict)
    {
        Main.AllPlayerSpeed[addict.PlayerId] = Main.MinSpeed;
        ReportDeadBodyPatch.CanReport[addict.PlayerId] = false;
        addict.MarkDirtySettings();

        LateTask.New(() =>
        {
            Main.AllPlayerSpeed[addict.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
            ReportDeadBodyPatch.CanReport[addict.PlayerId] = true;
            addict.MarkDirtySettings();
        }, FreezeTimeAfterImmortal.GetFloat(), "AddictGetDown");
    }

    public override void SetButtonTexts(HudManager hud, byte id)
    {
        hud.AbilityButton?.OverrideText(Translator.GetString("AddictVentButtonText"));
    }

    public override bool CanUseVent(PlayerControl pc, int ventId)
    {
        return !IsThisRole(pc) || pc.Is(CustomRoles.Nimble) || pc.GetClosestVent()?.Id == ventId;
    }
}