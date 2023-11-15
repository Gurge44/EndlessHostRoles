using AmongUs.GameOptions;
using System.Linq;
using TOHE.Roles.Impostor;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Translator;
using static TOHE.Utils;

namespace TOHE.Roles.Crewmate
{
    public static class Farseer
    {
        private static readonly int Id = 9700;

        private static readonly string fontSize = "1.6";

        public static OptionItem FarseerCooldown;
        public static OptionItem FarseerRevealTime;
        public static OptionItem Vision;

        private static readonly System.Collections.Generic.List<CustomRoles> randomRolesForTrickster = new()
        {
            CustomRoles.Snitch,
            CustomRoles.Luckey,
            CustomRoles.Needy,
            CustomRoles.SuperStar,
            CustomRoles.CyberStar,
            CustomRoles.Demolitionist,
            CustomRoles.Ventguard,
            CustomRoles.Express,
            CustomRoles.NiceEraser,
            CustomRoles.TaskManager,
            CustomRoles.GuessManager,
            CustomRoles.Benefactor,
            CustomRoles.Speedrunner,
            CustomRoles.Altruist,
            CustomRoles.Transmitter,
            CustomRoles.Autocrat,
            CustomRoles.Nightmare,
            CustomRoles.CameraMan,
            CustomRoles.NiceHacker,
            CustomRoles.Aid,
            CustomRoles.DonutDelivery,
            CustomRoles.Escort,
            CustomRoles.Tether,
            CustomRoles.Spy,
            CustomRoles.Ricochet,
            CustomRoles.Doormaster,
            CustomRoles.Mayor,
            CustomRoles.Paranoia,
            CustomRoles.Psychic,
            CustomRoles.Lookout,
            CustomRoles.SabotageMaster,
            CustomRoles.Snitch,
            CustomRoles.Marshall,
            CustomRoles.Monitor,
            CustomRoles.ParityCop,
       //     CustomRoles.SpeedBooster,
            CustomRoles.Dictator,
            CustomRoles.Doctor,
            CustomRoles.Detective,
            CustomRoles.NiceGuesser,
            CustomRoles.Transporter,
            CustomRoles.TimeManager,
            CustomRoles.Veteran,
            CustomRoles.Bodyguard,
            CustomRoles.Grenadier,
            CustomRoles.Lighter,
            CustomRoles.SecurityGuard,
            CustomRoles.Ventguard,
            CustomRoles.Divinator,
            CustomRoles.Oracle,
            CustomRoles.Tracefinder,
      //      CustomRoles.Glitch,
            CustomRoles.Judge,
            CustomRoles.NiceSwapper,
            CustomRoles.Mortician,
            CustomRoles.Mediumshiper,
            CustomRoles.Observer,
            CustomRoles.DovesOfNeace,
            CustomRoles.Bloodhound,
            CustomRoles.Retributionist,
            CustomRoles.Guardian,
            CustomRoles.Spiritualist,
            CustomRoles.Tracker,
        };

        public static System.Collections.Generic.Dictionary<int, string> RandomRole = new();

        public static void SetupCustomOption()
        {
            SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Farseer);
            FarseerCooldown = FloatOptionItem.Create(Id + 10, "FarseerRevealCooldown", new(0f, 60f, 2.5f), 15f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Farseer])
                .SetValueFormat(OptionFormat.Seconds);
            FarseerRevealTime = FloatOptionItem.Create(Id + 11, "FarseerRevealTime", new(0f, 30f, 1f), 10f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Farseer])
                .SetValueFormat(OptionFormat.Seconds);
            Vision = FloatOptionItem.Create(Id + 12, "FarseerVision", new(0f, 1f, 0.05f), 0.25f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Farseer])
                .SetValueFormat(OptionFormat.Multiplier);
        }
        public static void Init()
        {
            isEnable = false;
        }
        public static void Add(byte playerId)
        {
            isEnable = true;
            if (!AmongUsClient.Instance.AmHost) return;
            if (!Main.ResetCamPlayerList.Contains(playerId))
                Main.ResetCamPlayerList.Add(playerId);
        }
        public static bool isEnable;

        public static void SetCooldown(byte id) => Main.AllPlayerKillCooldown[id] = FarseerCooldown.GetFloat();

        public static void OnPostFix(PlayerControl player)
        {
            if (GameStates.IsInTask && Main.FarseerTimer.ContainsKey(player.PlayerId))//アーソニストが誰かを塗っているとき
            {
                if (!player.IsAlive() || Pelican.IsEaten(player.PlayerId))
                {
                    Main.FarseerTimer.Remove(player.PlayerId);
                    NotifyRoles(SpecifySeer: player);
                    RPC.ResetCurrentRevealTarget(player.PlayerId);
                }
                else
                {
                    var ar_target = Main.FarseerTimer[player.PlayerId].Item1;//塗られる人
                    var ar_time = Main.FarseerTimer[player.PlayerId].Item2;//塗った時間
                    if (!ar_target.IsAlive())
                    {
                        Main.FarseerTimer.Remove(player.PlayerId);
                    }
                    else if (ar_time >= FarseerRevealTime.GetFloat())//時間以上一緒にいて塗れた時
                    {
                        player.SetKillCooldown();
                        Main.FarseerTimer.Remove(player.PlayerId);//塗が完了したのでDictionaryから削除
                        Main.isRevealed[(player.PlayerId, ar_target.PlayerId)] = true;//塗り完了
                        player.RpcSetRevealtPlayer(ar_target, true);
                        NotifyRoles(SpecifySeer: player);//名前変更
                        RPC.ResetCurrentRevealTarget(player.PlayerId);
                    }
                    else
                    {

                        float range = NormalGameOptionsV07.KillDistances[Mathf.Clamp(player.Is(CustomRoles.Reach) ? 2 : Main.NormalOptions.KillDistance, 0, 2)] + 0.5f;
                        float dis = Vector2.Distance(player.transform.position, ar_target.transform.position);//距離を出す
                        if (dis <= range)//一定の距離にターゲットがいるならば時間をカウント
                        {
                            Main.FarseerTimer[player.PlayerId] = (ar_target, ar_time + Time.fixedDeltaTime);
                        }
                        else//それ以外は削除
                        {
                            Main.FarseerTimer.Remove(player.PlayerId);
                            NotifyRoles(SpecifySeer: player, SpecifyTarget: ar_target, ForceLoop: true);
                            RPC.ResetCurrentRevealTarget(player.PlayerId);

                            Logger.Info($"Canceled: {player.GetNameWithRole().RemoveHtmlTags()}", "Arsonist");
                        }
                    }
                }
            }
        }

        public static string GetRandomCrewRoleString()
        {
            var rd = IRandom.Instance;
            var randomRole = randomRolesForTrickster[rd.Next(0, randomRolesForTrickster.Count)];

            return $"<size={fontSize}>{ColorString(GetRoleColor(randomRole), GetString(randomRole.ToString()))}</size>";
        }

        public static string GetTaskState()
        {
            var playersWithTasks = Main.PlayerStates.Where(a => a.Value.GetTaskState().hasTasks).ToArray();
            if (playersWithTasks.Length == 0)
            {
                return "\r\n";
            }

            var rd = IRandom.Instance;
            var randomPlayer = playersWithTasks[rd.Next(0, playersWithTasks.Length)];
            var taskState = randomPlayer.Value.GetTaskState();

            Color TextColor;
            var TaskCompleteColor = Color.green;
            var NonCompleteColor = Color.yellow;
            var NormalColor = taskState.IsTaskFinished ? TaskCompleteColor : NonCompleteColor;

            TextColor = Camouflager.IsActive ? Color.gray : NormalColor;
            string Completed = Camouflager.IsActive ? "?" : $"{taskState.CompletedTasksCount}";

            return $" <size={fontSize}>" + ColorString(TextColor, $"({Completed}/{taskState.AllTasksCount})") + "</size>\r\n";
        }
    }
}
