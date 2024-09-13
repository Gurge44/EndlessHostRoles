﻿using System;

namespace EHR.Crewmate
{
    internal class Mathematician : RoleBase
    {
        public static (bool AskedQuestion, int Answer, byte ProtectedPlayerId, byte MathematicianPlayerId) State = (false, int.MaxValue, byte.MaxValue, byte.MaxValue);
        public static bool On;
        private static int Id => 643370;

        public override bool IsEnable => On;
        public override void SetupCustomOption() => Options.SetupSingleRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Mathematician);

        public override void Init()
        {
            On = false;
            State = (false, int.MaxValue, byte.MaxValue, byte.MaxValue);
        }

        public override void Add(byte playerId)
        {
            On = true;
            State = (false, int.MaxValue, byte.MaxValue, byte.MaxValue);
        }

        public static void Ask(PlayerControl pc, string num1Str, string num2Str)
        {
            try
            {
                if (pc == null || !pc.IsAlive() || !GameStates.IsMeeting || State.AskedQuestion || !int.TryParse(num1Str, out var num1) || !int.TryParse(num2Str, out var num2)) return;

                State.AskedQuestion = true;
                State.Answer = num1 + num2;
                State.MathematicianPlayerId = pc.PlayerId;

                string question = string.Format(Translator.GetString("MathematicianQuestionString"), num1, num2);
                ChatManager.SendPreviousMessagesToAll();
                LateTask.New(() => Utils.SendMessage(question, title: Translator.GetString("Mathematician")), 0.2f, log: false);
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }
        }

        public static void Reply(PlayerControl pc, string answerStr)
        {
            try
            {
                if (pc == null || !pc.IsAlive() || !GameStates.IsMeeting || !State.AskedQuestion || State.MathematicianPlayerId == pc.PlayerId || !int.TryParse(answerStr, out var answer)) return;

                if (answer == State.Answer)
                {
                    State.ProtectedPlayerId = pc.PlayerId;
                    Utils.SendMessage(string.Format(Translator.GetString("MathematicianAnsweredString"), pc.GetRealName(), answer), title: Translator.GetString("Mathematician"));
                    State.AskedQuestion = false;
                }
            }
            catch (Exception e)
            {
                Utils.ThrowException(e);
            }
        }

        public override void OnReportDeadBody()
        {
            State = (false, int.MaxValue, byte.MaxValue, byte.MaxValue);
        }
    }
}