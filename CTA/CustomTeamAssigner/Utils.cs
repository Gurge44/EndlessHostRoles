using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace CustomTeamAssigner
{
    internal static class Utils
    {
        public static HashSet<Team> Teams = [];

        public const string OutputFileName = "CTA_Data.txt";

        public static Color ToColor(this string htmlColor) => (Color)ColorConverter.ConvertFromString(htmlColor);

        public static void Do<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (var item in enumerable)
            {
                action(item);
            }
        }
    }
}
