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
                new("pinklaze#1776", isUp: true), // NCSIMON
                new("keepchirpy#6354", "#1FF3C6", "TOHEN Developer", true), // TommyXL
                new("motelchief#4112", "#eb57af", "<alpha=#CC>Twilight Raven<alpha=#FF>", true, true), // Drakos
                new("eagergaol#1562", "#5534eb", "Drakos Alt", true, true), // Drakos Steam Alt
                new("timedapper#9496"), //阿龍
                new("sofaagile#3120"), //天寸
                new("keyscreech#2151"), //Endrmen40409

                // Up
                new("primether#5348", tag: "<color=#FF0000>YouTuber</color>/<color=#8800FF>Streamer</color>", isUp: true), // AnonWorks
                new("truantwarm#9165", isUp: true), // 萧暮不姓萧
                new("drilldinky#1386", isUp: true), // 爱玩AU的河豚
                new("farardour#6818", isUp: true), // -提米SaMa-
                new("vealused#8192", isUp: true), // lag丶xy
                new("storyeager#0815", isUp: true), // 航娜丽莎
                new("versegame#3885", isUp: true), // 柴唔cw
                new("closegrub#6217", isUp: true), // 警长不会玩
                new("frownnatty#7935", isUp: true), // 鬼灵official
                new("veryscarf#5368", isUp: true), // 小武同学102
                new("sparklybee#0275", isUp: true), // --红包SaMa--
                new("endingyon#3175", isUp: true), // 游侠开摆
                new("firmine#0232", isUp: true), // YH永恒_
                new("storkfey#3570", isUp: true), // Calypso
                new("fellowsand#1003", isUp: true), // C-Faust
                new("jetsafe#8512", isUp: true), // Hoream是好人
                new("spoonkey#0792", isUp: true), // 没好康的
                new("beakedmire#6099", isUp: true), // 茄-au
                new("doggedsize#7892", isUp: true), // TronAndRey
                new("openlanded#9533", "#9e2424", "God Of Death Love Apples", true), // ryuk
                new("unlikecity#4086", "#eD2F91", "Ward", true), // Ward
                new("iconicdrop#2727", isUp: true), // jackler


                new("goneria#8334", "#FFFF00", "The Dev on Phone", true, true), // [Developer] The 200IQ guy // Me on phone
                new("neatnet#5851", "#FFFF00", "[Developer] The 200IQ guy", true, true), // [Developer] The 200IQ guy
                new("contenthue#0404", "#FFFF00", "[Developer] The 200IQ guy", true, true), // [Developer] The 200IQ guy
                new("theseform#5686", tag: "<color=#7800FF>PTBR-Translator</color>/<color=#FF0000>Role Idea Creator</color>", isUp: true), // Tomix
                new("heavyclod#2286", "#FFFF00", "小叨.exe已停止运行", true), // 小叨院长
                new("storeroan#0331", "#FF0066", "Night_瓜", true), // Night_瓜
                new("teamelder#5856", "#1379bf", "屑Slok（没信誉的鸽子）", true), // Slok7565

                new("radarright#2509"),

                // EHR players
                new("ravenknurl#4562", "#008000", "Moderador do FH", true), // RicardoFumante
                new("crustzonal#9589", "#00FFFF", "Translator PT-BR", true), // artyleague01
                new("tinedpun#6584", "#0000ff", "Translator PT-BR", true), // Dechis
                new("swiftlord#8072", isUp: true), // But What About
                new("ovalinstep#2984", isUp: true), // Seleneous
                new("seleneous#6930", isUp: true), // Seleneous
                new("innerruler#4140", isUp: true), // thewhiskas27
                new("urbanecalf#4975", "#420773", "Streamer", true), // Gugutsik
                new("crustzonal#9589", isUp: true), // aviiiv0102
                new("tubedilute#9062", isUp: true), // Xcl4udioX
                new("ponyholey#5532", "#0000FF", "desenvolvedor", true), // DrawingsZz
                new("akinlaptop#2206", isUp: true), // MR Carr
                new("fursilty#4676", "#0000ff", "arthurzin", true), // arthurzin
                new("stonefuzzy#8673", "#ff0062", "<size=1.6>Ru Translator</size>", true, true), // HyperAtill
                new("momenthale#7626", "#6f3bd9", "Amiran", true), // Amiran
                new("valuecubic#5819", "#6f3bd9", "Kate", true), // Kate Cat
                new("frizzytram#2508", "#1C87FF", "RafaelBIT50", true), // RafaelBIT50
                new("ruefulscar#0287", isUp: true), // Zendena
                new("foggyzing#6238", isUp: true), // LdZinnn
                new("onsideblur#3929", "#fc3a51", "YouTuber", isUp: true), // Manelzin
                new("modestspan#7071", "null", "null", true), // Shark
                new("divotbusy#0624", isUp: true), // PreCeptorBR
                new("pinsrustic#5496", "#fc3a51", "YouTuber", isUp: true), // FHgameplay
                new("opaquedot#5610", isUp: true), // Deh_66
                new("furpolitic#3380", "#a020f0", "EuOncologico o impostor", isUp: true), // EuOncologico
                new("pithfierce#5073", isUp: true), // OrigeTv
                new("cannylink#0564", isUp: true), // SpicyPoops
                new("ghostapt#7243", "#a48d6b", "AUME", true), // MasterKy
                new("planegame#5847", "#44fff7", "Kopp56", true), // Kopp56 PRO
                new("clovesorry#6973", "#191970", "Master", true), // MAT
                new("ivorywish#3580", "#ff0000", "YouTuber", true), // Erik Carr
                new("cleardress#6310", "#ffffff", "<#00BFFF>一</color><#48D1CC>个</color><#7CFC00>热狗</color><#32CD32>uwu</color>", true), // ABoringCat
                new("rainypearl#9545", "#A020F0", "YouTuber", true), // bielModzs
                new("kiltedbill#4145", isUp: true), // tsaki84
                new("onlyfax#3941", "#ff0000", "YouTuber", true), // The Nick AG
                new("somewallet#5521", "#ffff00", "Artist", true), // YoshiBertil
                new("pagersane#4064", isUp: true), // Bluejava3
                new("pocketdoor#9080", isUp: true), // Imnot
                new("motorstack#2287", "#E34234", "Foxfire", true), // Esrazraft
                new("motorlace#4741", "#DFB722", "\u2756 Assistant Tester \u2756", true), // PEPPERcula
                new("kohlcuboid#5702", "#DFB722", "\u2756 Assistant Tester \u2756", true), // PEPPERcula
                new("keyrunning#8720", "#00ffff", "Miku", true), // EverMortal_1455
                new("yellowjoy#3138", "#FFE87C", "Sunshine", true), // Becksy
                new("stuckclaw#5717", "#FF474C", "The biggest SUS", true), // BIG SUS
                new("funnytiger#8420", "#ffd4ec", "Registered Bozo", true), // Differntperson
                new("raritykey#6021", "#08f638", "DESPAIR", true), // Junko
                new("stiltedgap#2406", "#ffff00", "Youtuber", true), // Marcopolo1982

                // Sponsor
                new("recentduct#6068", "#FF00FF", "高冷男模法师", false, true),
                new("canneddrum#2370", "#fffcbe", "我是喜唉awa"),
                new("dovefitted#5329", "#1379bf", "不要首刀我"),
                new("luckylogo#7352", "#f30000", "林@林"),
                new("axefitful#8788", "#8e8171", "寄才是真理"),
                new("raftzonal#8893", "#8e8171", "寄才是真理"),
                new("twainrobin#8089", "#0000FF", "啊哈修maker"),
                new("mallcasual#6075", "#f89ccb", "波奇酱"),
                new("beamelfin#9478", "#6495ED", "Amaster-1111"),
                new("lordcosy#8966", "#FFD6EC", "HostEHR"), // K
                new("honestsofa#2870", "#D381D9", "Discord: SolarFlare#0700", true), // SolarFlare
                new("caseeast#7194", "#1c2451", "disc.gg/maul"), // laikrai

                // Other
                new("peakcrown#8292", isUp: true) // Hakaka
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