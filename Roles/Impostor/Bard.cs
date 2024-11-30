namespace EHR.Impostor
{
    public class Bard : RoleBase
    {
        public static int BardCreations;
        public static bool On;
        public override bool IsEnable => On;

        public override void Add(byte playerId)
        {
            On = true;
        }

        public override void Init()
        {
            On = false;
            BardCreations = 0;
        }

        public override void SetupCustomOption() { }

        public static void OnMeetingHudDestroy(ref string name)
        {
            BardCreations++;

            try
            {
                name = ModUpdater.Get("https://v1.hitokoto.cn/?encode=text");
            }
            catch
            {
                name = Translator.GetString("ByBardGetFailed");
            }

            name += "\n\t\t——" + Translator.GetString("ByBard");
        }
    }
}