using System.Collections.Generic;
using System.Linq;

namespace EHR
{
    public class DevUser(string code = "", string color = "null", string tag = "null", bool isUp = false, bool deBug = false)
    {
        public string Code { get; } = code;
        private string Color { get; } = color;
        private string Tag { get; } = tag;
        public bool IsUp { get; } = isUp;
        public bool DeBug { get; } = deBug;

        public bool HasTag()
        {
            return Tag != "null";
        }

        public string GetTag()
        {
            return Color == "null" ? $"<size=1.4>{Tag}</size>\r\n" : $"<color={Color}><size=1.4>{(Tag == "#Dev" ? Translator.GetString("Developer") : Tag)}</size></color>\r\n";
        }
    }

    public static class DevManager
    {
        private static readonly DevUser DefaultDevUser = new();
        private static List<DevUser> DevUserList = [];

        public static void Init()
        {
            DevUserList =
            [
                // Dev
                new("actorour#0029", "#ffc0cb", "TOHE/TONX Developer", true, true), // KARPED1EM
                new("pinklaze#1776", null, "null", true, true), // NCSIMON
                new("keepchirpy#6354", "#1FF3C6", "TOHE Developer", true, false), // TommyXL //Tommy-XL
                new("motelchief#4112", "#eb57af", "<alpha=#CC>Twilight Raven<alpha=#FF>", true, true), // Drakos //Drakos
                new("eagergaol#1562", "#5534eb", "Drakos Alt", true, true), // Drakos //Drakos Steam Alt
                new("timedapper#9496", null, null, false, false), //阿龍
                new("sofaagile#3120", "null", "null", false, true), //天寸
                new("keyscreech#2151", "null", null, false, false), //Endrmen40409

                // Up
                new("primether#5348", "null", "<color=#FF0000>YouTuber</color>/<color=#8800FF>Streamer</color>", true, false), // AnonWorks
                new("truantwarm#9165", "null", "null", true, false), // 萧暮不姓萧
                new("drilldinky#1386", "null", "null", true, false), // 爱玩AU的河豚
                new("farardour#6818", "null", "null", true, false), // -提米SaMa-
                new("vealused#8192", "null", "null", true, false), // lag丶xy
                new("storyeager#0815", "null", "null", true, false), // 航娜丽莎
                new("versegame#3885", "null", "null", true, false), // 柴唔cw
                new("closegrub#6217", "null", "null", true, false), // 警长不会玩
                new("frownnatty#7935", "null", "null", true, false), // 鬼灵official
                new("veryscarf#5368", "null", "null", true, false), // 小武同学102
                new("sparklybee#0275", "null", "null", true, false), // --红包SaMa--
                new("endingyon#3175", "null", "null", true, false), // 游侠开摆
                new("firmine#0232", "null", "null", true, false), // YH永恒_
                new("storkfey#3570", "null", "null", true, false), // Calypso
                new("fellowsand#1003", "null", "null", true, false), // C-Faust
                new("jetsafe#8512", "null", "null", true, false), // Hoream是好人
                new("spoonkey#0792", "null", "null", true, false), // 没好康的
                new("beakedmire#6099", "null", "null", true, false), // 茄-au
                new("doggedsize#7892", "null", "null", true, false), // TronAndRey
                new("openlanded#9533", "#9e2424", "God Of Death Love Apples", true, true), // ryuk
                new("unlikecity#4086", "#eD2F91", "Ward", true, false), // Ward
                new("iconicdrop#2727", "null", "null", true, false), // jackler


                new("goneria#8334", "#FFFF00", "The Dev on Phone", true, true), // [Developer] The 200IQ guy // Me on phone
                new("neatnet#5851", "#FFFF00", "[Developer] The 200IQ guy", true, true), // [Developer] The 200IQ guy
                new("contenthue#0404", "#FFFF00", "[Developer] The 200IQ guy", true, true), // [Developer] The 200IQ guy
                new("theseform#5686", "null", "<color=#7800FF>PTBR-Translator</color>/<color=#FF0000>Role Idea Creator</color>", true, false), // Tomix
                new("heavyclod#2286", "#FFFF00", "小叨.exe已停止运行", true, false), // 小叨院长
                new("storeroan#0331", "#FF0066", "Night_瓜", true, false), // Night_瓜
                new("teamelder#5856", "#1379bf", "屑Slok（没信誉的鸽子）", true, false), // Slok7565

                new("radarright#2509", "null", "null", false, true),

                // EHR players
                new("ravenknurl#4562", "#008000", "Moderador do FH", false, false), // RicardoFumante
                new("crustzonal#9589", "#00FFFF", "Translator PT-BR", true, false), // artyleague01
                new("tinedpun#6584", "#0000ff", "Translator PT-BR", true, false), // Dechis
                new("swiftlord#8072", "null", "null", true, false), // But What About
                new("ovalinstep#2984", "null", "null", true, false), // Seleneous
                new("seleneous#6930", "null", "null", true, false), // Seleneous
                new("innerruler#4140", "null", "null", true, false), // thewhiskas27
                new("urbanecalf#4975", "#420773", "Streamer", true, false), // Gugutsik
                new("crustzonal#9589", "null", "null", true, false), // aviiiv0102
                new("tubedilute#9062", "null", "null", true, false), // Xcl4udioX
                new("ponyholey#5532", "#0000FF", "desenvolvedor", true, false), // DrawingsZz
                new("akinlaptop#2206", "null", "null", true, false), // MR Carr
                new("fursilty#4676", "#0000ff", "arthurzin", true, false), // arthurzin
                new("stonefuzzy#8673", "#ff0062", "<size=1.6>Ru Translator</size>", true, true), // HyperAtill
                new("frizzytram#2508", "#1C87FF", "RafaelBIT50", false, false), // RafaelBIT50
                new("ruefulscar#0287", "null", "null", true, false), // Zendena
                new("foggyzing#6238", "null", "null", true, false), // LdZinnn
                new("onsideblur#3929", "#fc3a51", "YouTuber", true, false), // Manelzin
                new("modestspan#7071", "null", "null", true, false), // Shark
                new("divotbusy#0624", "null", "null", true, false), // PreCeptorBR
                new("pinsrustic#5496", "#fc3a51", "YouTuber", true, false), // FHgameplay
                new("opaquedot#5610", "null", "null", true, false), // Deh_66
                new("furpolitic#3380", "#a020f0", "EuOncologico o impostor", true, false), // EuOncologico
                new("pithfierce#5073", "null", "null", true, false), // OrigeTv
                new("cannylink#0564", "null", "null", true, false), // SpicyPoops
                new("ghostapt#7243", "#a48d6b", "AUME", true, false), // MasterKy
                new("planegame#5847", "#44fff7", "Kopp56", true, false), // Kopp56 PRO
                new("clovesorry#6973", "#191970", "Master", true, false), // MAT
                new("ivorywish#3580", "#ff0000", "YouTuber", true, false), // Erik Carr
                new("cleardress#6310", "#ffffff", "<#00BFFF>一</color><#48D1CC>个</color><#7CFC00>热狗</color><#32CD32>uwu</color>", true, false), // ABoringCat
                new("rainypearl#9545", "#A020F0", "YouTuber", true, false), // bielModzs
                new("kiltedbill#4145", "null", "null", true, false), // tsaki84
                new("onlyfax#3941", "#ff0000", "YouTuber", true, false), // The Nick AG
                new("somewallet#5521", "#ffff00", "Artist", true, false), // YoshiBertil
                new("pagersane#4064", "null", "null", true, false), // Bluejava3
                new("pocketdoor#9080", "null", "null", true, false), // Imnot
                new("motorstack#2287", "#E34234", "Foxfire", true, false), // Esrazraft
                new("motorlace#4741", "#DFB722", "\u2756 Assistant Tester \u2756", true, false), // PEPPERcula
                new("keyrunning#8720", "#00ffff", "Miku", true, false), // EverMortal_1455
                new("yellowjoy#3138", "#FFE87C", "Sunshine", true, false), // Becksy

                // Sponsor
                new("recentduct#6068", "#FF00FF", "高冷男模法师", false, true),
                new("canneddrum#2370", "#fffcbe", "我是喜唉awa", false, false),
                new("dovefitted#5329", "#1379bf", "不要首刀我", false, false),
                new("luckylogo#7352", "#f30000", "林@林", false, false),
                new("axefitful#8788", "#8e8171", "寄才是真理", false, false),
                new("raftzonal#8893", "#8e8171", "寄才是真理", false, false),
                new("twainrobin#8089", "#0000FF", "啊哈修maker", false, false),
                new("mallcasual#6075", "#f89ccb", "波奇酱", false, false),
                new("beamelfin#9478", "#6495ED", "Amaster-1111", false, false),
                new("lordcosy#8966", "#FFD6EC", "HostEHR", false, false), // K
                new("honestsofa#2870", "#D381D9", "Discord: SolarFlare#0700", true, false), // SolarFlare //SolarFlare
                new("caseeast#7194", "#1c2451", "disc.gg/maul", false, false), // laikrai
                // Other
                new("peakcrown#8292", "null", "null", true, false) // Hakaka
            ];
        }

        private static bool IsDevUser(this string code)
        {
            return DevUserList.Any(x => x.Code == code);
        }

        public static DevUser GetDevUser(this string code)
        {
            return code.IsDevUser() ? DevUserList.Find(x => x.Code == code) : DefaultDevUser;
        }
    }
}