using System.Linq;
using EHR.Gamemodes;
using EHR.Modules.Extensions;

namespace EHR.Roles;

public class Detector : RoleBase, IHideAndSeekRole
{
    public static bool On;

    private static OptionItem InfoFrequency;
    private static OptionItem Vision;
    private static OptionItem Speed;

    public override bool IsEnable => On;
    public Team Team => Team.Crewmate;
    public int Chance => CustomRoles.Detector.GetMode();
    public int Count => CustomRoles.Detector.GetCount();
    public float RoleSpeed => Speed.GetFloat();
    public float RoleVision => Vision.GetFloat();

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(69_211_601, TabGroup.CrewmateRoles, CustomRoles.Detector, CustomGameMode.HideAndSeek);

        Vision = new FloatOptionItem(69_211_603, "DetectorVision", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(66, 221, 245, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Detector]);

        Speed = new FloatOptionItem(69_213_604, "DetectorSpeed", new(0.05f, 5f, 0.05f), 1.25f, TabGroup.CrewmateRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Multiplier)
            .SetColor(new(66, 221, 245, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Detector]);

        InfoFrequency = new IntegerOptionItem(69_213_605, "DetectorFrequency", new(0, 60, 1), 15, TabGroup.CrewmateRoles)
            .SetGameMode(CustomGameMode.HideAndSeek)
            .SetValueFormat(OptionFormat.Seconds)
            .SetColor(new(66, 221, 245, byte.MaxValue))
            .SetParent(Options.CustomRoleSpawnChances[CustomRoles.Detector]);
    }

    public override void Add(byte playerId)
    {
        On = true;
        int infoFrequency = InfoFrequency.GetInt();
        _ = new CountdownTimer(8 + infoFrequency, OnElapsed, cancelOnMeeting: false);
        return;

        void OnElapsed()
        {
            PlayerControl pc = Utils.GetPlayerById(playerId);
            if (pc == null || !pc.IsAlive()) return;
            
            PlayerControl[] imps = CustomHnS.PlayerRoles.Where(x => x.Value.Interface.Team == Team.Impostor).Select(x => Utils.GetPlayerById(x.Key)).Where(x => x != null && x.GetPlainShipRoom() != null).ToArray();

            if (imps.Length > 0)
            {
                PlayerControl imp = imps.RandomElement();
                string room = Translator.GetString($"{imp.GetPlainShipRoom().RoomId}");
                pc.Notify(string.Format(Translator.GetString("DetectorNotify"), room));
            }
            
            _ = new CountdownTimer(infoFrequency, OnElapsed, cancelOnMeeting: false);
        }
    }

    public override void Init()
    {
        On = false;
    }
}