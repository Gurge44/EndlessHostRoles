using AmongUs.GameOptions;
using EHR.Modules;
using Hazel;

namespace EHR.Impostor;

public class Venerer : RoleBase
{
    public static bool On;

    public override bool IsEnable => On;

    public static OptionItem AbilityCooldown;
    private static OptionItem AbilityDuration;
    private static OptionItem IncreasedSpeed;
    private static OptionItem DecreasedSpeed;
    private static OptionItem FreezeRadius;

    private int Stage;
    private bool ChangedSkin;
    private byte VenererId;

    public override void SetupCustomOption()
    {
        StartSetup(654100)
            .AutoSetupOption(ref AbilityCooldown, 30, new IntegerValueRule(1, 120, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref AbilityDuration, 10, new IntegerValueRule(1, 90, 1), OptionFormat.Seconds)
            .AutoSetupOption(ref IncreasedSpeed, 1.75f, new FloatValueRule(0.05f, 3f, 0.05f), OptionFormat.Multiplier)
            .AutoSetupOption(ref DecreasedSpeed, 0.5f, new FloatValueRule(0.05f, 3f, 0.05f), OptionFormat.Multiplier)
            .AutoSetupOption(ref FreezeRadius, 4f, new FloatValueRule(0.5f, 10f, 0.5f), OptionFormat.Multiplier);
    }

    public override void Init()
    {
        On = false;
    }

    public override void Add(byte playerId)
    {
        On = true;
        Stage = 0;
        ChangedSkin = false;
        VenererId = playerId;
    }

    public override void OnMurder(PlayerControl killer, PlayerControl target)
    {
        Stage++;
        if (Stage > 3) Stage = 3;
        else Utils.SendRPC(CustomRPC.SyncRoleData, killer.PlayerId, Stage);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        if (Options.UsePhantomBasis.GetBool())
        {
            AURoleOptions.PhantomCooldown = AbilityCooldown.GetInt();
            AURoleOptions.PhantomDuration = 1f;
        }
        else
        {
            if (Options.UsePets.GetBool()) return;

            AURoleOptions.ShapeshifterCooldown = AbilityCooldown.GetInt();
            AURoleOptions.ShapeshifterDuration = 1f;
        }
    }

    public override void OnPet(PlayerControl pc)
    {
        UseAbility(pc);
    }

    public override bool OnVanish(PlayerControl pc)
    {
        UseAbility(pc);
        return false;
    }

    public override bool OnShapeshift(PlayerControl shapeshifter, PlayerControl target, bool shapeshifting)
    {
        if (shapeshifting) UseAbility(shapeshifter);
        return false;
    }

    private void UseAbility(PlayerControl pc)
    {
        switch (Stage)
        {
            case 1:
                var outfit = Camouflage.PlayerSkins[pc.PlayerId];
                Utils.RpcChangeSkin(pc, new NetworkedPlayerInfo.PlayerOutfit().Set("", 15, "", "", "", "", ""));
                ChangedSkin = true;
                LateTask.New(() =>
                {
                    if (!ChangedSkin || pc == null || !pc.IsAlive()) return;
                    Utils.RpcChangeSkin(pc, outfit);
                    ChangedSkin = false;
                }, AbilityDuration.GetInt(), log: false);
                break;
            case 2:
                Main.AllPlayerSpeed[pc.PlayerId] = IncreasedSpeed.GetFloat();
                pc.MarkDirtySettings();
                LateTask.New(() =>
                {
                    if (pc == null || !pc.IsAlive()) return;
                    Main.AllPlayerSpeed[pc.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                    if (GameStates.IsInTask && !ExileController.Instance && !AntiBlackout.SkipTasks) pc.MarkDirtySettings();
                }, AbilityDuration.GetInt(), log: false);
                goto case 1;
            case 3:
                Utils.GetPlayersInRadius(FreezeRadius.GetFloat(), pc.Pos()).Without(pc).Do(x =>
                {
                    Main.AllPlayerSpeed[x.PlayerId] = DecreasedSpeed.GetFloat();
                    x.MarkDirtySettings();
                    LateTask.New(() =>
                    {
                        if (x == null || !x.IsAlive()) return;
                        Main.AllPlayerSpeed[x.PlayerId] = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);
                        if (GameStates.IsInTask && !ExileController.Instance && !AntiBlackout.SkipTasks) x.MarkDirtySettings();
                    }, AbilityDuration.GetInt(), log: false);
                });
                goto case 2;
        }
    }

    public override void OnReportDeadBody()
    {
        if (ChangedSkin)
        {
            ChangedSkin = false;
            PlayerControl pc = VenererId.GetPlayer();
            if (pc == null || !pc.IsAlive()) return;
            Utils.RpcChangeSkin(pc, Camouflage.PlayerSkins[PlayerControl.LocalPlayer.PlayerId]);
        }
    }

    public void ReceiveRPC(MessageReader reader)
    {
        Stage = reader.ReadPackedInt32();
    }

    public override string GetProgressText(byte playerId, bool comms)
    {
        return base.GetProgressText(playerId, comms) + $" ({Stage}/3)";
    }
}