using System;

namespace EHR.Modules
{
    internal class LogHandler(string tag) : ILogHandler
    {
        public string Tag { get; } = tag;

        public void Info(string text)
        {
            Logger.Info(text, Tag);
        }

        public void Warn(string text)
        {
            Logger.Warn(text, Tag);
        }

        public void Error(string text)
        {
            Logger.Error(text, Tag);
        }

        public void Fatal(string text)
        {
            Logger.Fatal(text, Tag);
        }

        public void Msg(string text)
        {
            Logger.Msg(text, Tag);
        }

        public void Exception(Exception ex)
        {
            Logger.Exception(ex, Tag);
        }
    }
}