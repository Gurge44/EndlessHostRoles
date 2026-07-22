using System;
using System.Collections.Generic;
// ReSharper disable InconsistentNaming

namespace EHR.ArabicSupport
{
    public static class ArabicFixer
    {
        public static string Fix(string str, bool rtl)
        {
            if (rtl)
            {
                return Fix(str);
            }

            string[] words = str.Split(' ');
            string result = "";
            string arabicToIgnore = "";
            foreach (string word in words)
            {
                if (word.Length > 0 && char.IsLower(word.ToLower()[word.Length / 2]))
                {
                    result += Fix(arabicToIgnore) + word + " ";
                    arabicToIgnore = "";
                }
                else
                {
                    arabicToIgnore += word + " ";
                }
            }
            if (arabicToIgnore != "")
                result += Fix(arabicToIgnore);
            return result;
        }

        // ReSharper disable once MethodOverloadWithOptionalParameter
        private static string Fix(string str, bool showTashkeel = false, bool useHinduNumbers = true)
        {
            ArabicFixerTool.showTashkeel = showTashkeel;
            ArabicFixerTool.useHinduNumbers = useHinduNumbers;

            if (str.Contains('\n'))
                str = str.Replace("\n", Environment.NewLine);

            if (str.Contains(Environment.NewLine))
            {
                string[] stringSeparators = [Environment.NewLine];
                string[] strSplit = str.Split(stringSeparators, StringSplitOptions.None);
                if (strSplit.Length <= 1)
                    return ArabicFixerTool.FixLine(str);

                string outputString = ArabicFixerTool.FixLine(strSplit[0]);
                int iteration = 1;
                while (iteration < strSplit.Length)
                {
                    outputString += Environment.NewLine + ArabicFixerTool.FixLine(strSplit[iteration]);
                    iteration++;
                }
                return outputString;
            }

            return ArabicFixerTool.FixLine(str);
        }
    }

    internal enum IsolatedArabicLetters
    {
        Hamza = 0xFE80, Alef = 0xFE8D, AlefHamza = 0xFE83, WawHamza = 0xFE85,
        AlefMaksoor = 0xFE87, AlefMaksora = 0xFBFC, HamzaNabera = 0xFE89,
        Ba = 0xFE8F, Ta = 0xFE95, Tha2 = 0xFE99, Jeem = 0xFE9D, H7aa = 0xFEA1,
        Khaa2 = 0xFEA5, Dal = 0xFEA9, Thal = 0xFEAB, Ra2 = 0xFEAD, Zeen = 0xFEAF,
        Seen = 0xFEB1, Sheen = 0xFEB5, S9a = 0xFEB9, Dha = 0xFEBD, T6a = 0xFEC1,
        T6ha = 0xFEC5, Ain = 0xFEC9, Gain = 0xFECD, Fa = 0xFED1, Gaf = 0xFED5,
        Kaf = 0xFED9, Lam = 0xFEDD, Meem = 0xFEE1, Noon = 0xFEE5, Ha = 0xFEE9,
        Waw = 0xFEED, Ya = 0xFEF1, AlefMad = 0xFE81, TaMarboota = 0xFE93,
        PersianPe = 0xFB56, PersianChe = 0xFB7A, PersianZe = 0xFB8A,
        PersianGaf = 0xFB92, PersianGaf2 = 0xFB8E, PersianYeh = 0xFBFC,
    }

    internal enum GeneralArabicLetters
    {
        Hamza = 0x0621, Alef = 0x0627, AlefHamza = 0x0623, WawHamza = 0x0624,
        AlefMaksoor = 0x0625, AlefMagsora = 0x0649, HamzaNabera = 0x0626,
        Ba = 0x0628, Ta = 0x062A, Tha2 = 0x062B, Jeem = 0x062C, H7aa = 0x062D,
        Khaa2 = 0x062E, Dal = 0x062F, Thal = 0x0630, Ra2 = 0x0631, Zeen = 0x0632,
        Seen = 0x0633, Sheen = 0x0634, S9a = 0x0635, Dha = 0x0636, T6a = 0x0637,
        T6ha = 0x0638, Ain = 0x0639, Gain = 0x063A, Fa = 0x0641, Gaf = 0x0642,
        Kaf = 0x0643, Lam = 0x0644, Meem = 0x0645, Noon = 0x0646, Ha = 0x0647,
        Waw = 0x0648, Ya = 0x064A, AlefMad = 0x0622, TaMarboota = 0x0629,
        PersianPe = 0x067E, PersianChe = 0x0686, PersianZe = 0x0698,
        PersianGaf = 0x06AF, PersianGaf2 = 0x06A9, PersianYeh = 0x06CC,
    }

    internal static class ArabicTable
    {
        private static readonly Dictionary<int, int> Map = new()
        {
            [(int)GeneralArabicLetters.Hamza] = (int)IsolatedArabicLetters.Hamza,
            [(int)GeneralArabicLetters.Alef] = (int)IsolatedArabicLetters.Alef,
            [(int)GeneralArabicLetters.AlefHamza] = (int)IsolatedArabicLetters.AlefHamza,
            [(int)GeneralArabicLetters.WawHamza] = (int)IsolatedArabicLetters.WawHamza,
            [(int)GeneralArabicLetters.AlefMaksoor] = (int)IsolatedArabicLetters.AlefMaksoor,
            [(int)GeneralArabicLetters.AlefMagsora] = (int)IsolatedArabicLetters.AlefMaksora,
            [(int)GeneralArabicLetters.HamzaNabera] = (int)IsolatedArabicLetters.HamzaNabera,
            [(int)GeneralArabicLetters.Ba] = (int)IsolatedArabicLetters.Ba,
            [(int)GeneralArabicLetters.Ta] = (int)IsolatedArabicLetters.Ta,
            [(int)GeneralArabicLetters.Tha2] = (int)IsolatedArabicLetters.Tha2,
            [(int)GeneralArabicLetters.Jeem] = (int)IsolatedArabicLetters.Jeem,
            [(int)GeneralArabicLetters.H7aa] = (int)IsolatedArabicLetters.H7aa,
            [(int)GeneralArabicLetters.Khaa2] = (int)IsolatedArabicLetters.Khaa2,
            [(int)GeneralArabicLetters.Dal] = (int)IsolatedArabicLetters.Dal,
            [(int)GeneralArabicLetters.Thal] = (int)IsolatedArabicLetters.Thal,
            [(int)GeneralArabicLetters.Ra2] = (int)IsolatedArabicLetters.Ra2,
            [(int)GeneralArabicLetters.Zeen] = (int)IsolatedArabicLetters.Zeen,
            [(int)GeneralArabicLetters.Seen] = (int)IsolatedArabicLetters.Seen,
            [(int)GeneralArabicLetters.Sheen] = (int)IsolatedArabicLetters.Sheen,
            [(int)GeneralArabicLetters.S9a] = (int)IsolatedArabicLetters.S9a,
            [(int)GeneralArabicLetters.Dha] = (int)IsolatedArabicLetters.Dha,
            [(int)GeneralArabicLetters.T6a] = (int)IsolatedArabicLetters.T6a,
            [(int)GeneralArabicLetters.T6ha] = (int)IsolatedArabicLetters.T6ha,
            [(int)GeneralArabicLetters.Ain] = (int)IsolatedArabicLetters.Ain,
            [(int)GeneralArabicLetters.Gain] = (int)IsolatedArabicLetters.Gain,
            [(int)GeneralArabicLetters.Fa] = (int)IsolatedArabicLetters.Fa,
            [(int)GeneralArabicLetters.Gaf] = (int)IsolatedArabicLetters.Gaf,
            [(int)GeneralArabicLetters.Kaf] = (int)IsolatedArabicLetters.Kaf,
            [(int)GeneralArabicLetters.Lam] = (int)IsolatedArabicLetters.Lam,
            [(int)GeneralArabicLetters.Meem] = (int)IsolatedArabicLetters.Meem,
            [(int)GeneralArabicLetters.Noon] = (int)IsolatedArabicLetters.Noon,
            [(int)GeneralArabicLetters.Ha] = (int)IsolatedArabicLetters.Ha,
            [(int)GeneralArabicLetters.Waw] = (int)IsolatedArabicLetters.Waw,
            [(int)GeneralArabicLetters.Ya] = (int)IsolatedArabicLetters.Ya,
            [(int)GeneralArabicLetters.AlefMad] = (int)IsolatedArabicLetters.AlefMad,
            [(int)GeneralArabicLetters.TaMarboota] = (int)IsolatedArabicLetters.TaMarboota,
            [(int)GeneralArabicLetters.PersianPe] = (int)IsolatedArabicLetters.PersianPe,
            [(int)GeneralArabicLetters.PersianChe] = (int)IsolatedArabicLetters.PersianChe,
            [(int)GeneralArabicLetters.PersianZe] = (int)IsolatedArabicLetters.PersianZe,
            [(int)GeneralArabicLetters.PersianGaf] = (int)IsolatedArabicLetters.PersianGaf,
            [(int)GeneralArabicLetters.PersianGaf2] = (int)IsolatedArabicLetters.PersianGaf2,
            [(int)GeneralArabicLetters.PersianYeh] = (int)IsolatedArabicLetters.PersianYeh
        };

        internal static int Convert(int value) => Map.GetValueOrDefault(value, value);
    }

    internal class TashkeelLocation(char tashkeel, int position)
    {
        public char tashkeel = tashkeel;
        public readonly int position = position;
    }

    internal static class ArabicFixerTool
    {
        internal static bool showTashkeel = true;
        private const bool combineTashkeel = true;
        internal static bool useHinduNumbers;
        private static readonly StringBuilder internalStringBuilder = new();

        private static void RemoveTashkeel(ref string str, out List<TashkeelLocation> tashkeelLocation)
        {
            tashkeelLocation = [];
            var lastSplitIndex = 0;
            internalStringBuilder.Clear();
            internalStringBuilder.EnsureCapacity(str.Length);
            int index = 0;

            for (int i = 0; i < str.Length; i++)
            {
                switch (str[i])
                {
                    case (char)0x064B:
                        tashkeelLocation.Add(new TashkeelLocation((char)0x064B, i)); index++; IncrementSB(ref str, i);
                        break;
                    case (char)0x064C:
                        tashkeelLocation.Add(new TashkeelLocation((char)0x064C, i)); index++; IncrementSB(ref str, i);
                        break;
                    case (char)0x064D:
                        tashkeelLocation.Add(new TashkeelLocation((char)0x064D, i)); index++; IncrementSB(ref str, i);
                        break;
                    case (char)0x064E when index > 0 && combineTashkeel && tashkeelLocation[index - 1].tashkeel == (char)0x0651:
                        tashkeelLocation[index - 1].tashkeel = (char)0xFC60; IncrementSB(ref str, i); continue;
                    case (char)0x064E:
                        tashkeelLocation.Add(new TashkeelLocation((char)0x064E, i)); index++; IncrementSB(ref str, i);
                        break;
                    case (char)0x064F when index > 0 && combineTashkeel && tashkeelLocation[index - 1].tashkeel == (char)0x0651:
                        tashkeelLocation[index - 1].tashkeel = (char)0xFC61; IncrementSB(ref str, i); continue;
                    case (char)0x064F:
                        tashkeelLocation.Add(new TashkeelLocation((char)0x064F, i)); index++; IncrementSB(ref str, i);
                        break;
                    case (char)0x0650 when index > 0 && combineTashkeel && tashkeelLocation[index - 1].tashkeel == (char)0x0651:
                        tashkeelLocation[index - 1].tashkeel = (char)0xFC62; IncrementSB(ref str, i); continue;
                    case (char)0x0650:
                        tashkeelLocation.Add(new TashkeelLocation((char)0x0650, i)); index++; IncrementSB(ref str, i);
                        break;
                    case (char)0x0651:
                    {
                        if (index > 0 && combineTashkeel)
                        {
                            switch (tashkeelLocation[index - 1].tashkeel)
                            {
                                case (char)0x064E:
                                    tashkeelLocation[index - 1].tashkeel = (char)0xFC60; IncrementSB(ref str, i); continue;
                                case (char)0x064F:
                                    tashkeelLocation[index - 1].tashkeel = (char)0xFC61; IncrementSB(ref str, i); continue;
                                case (char)0x0650:
                                    tashkeelLocation[index - 1].tashkeel = (char)0xFC62; IncrementSB(ref str, i); continue;
                            }
                        }
                        tashkeelLocation.Add(new TashkeelLocation((char)0x0651, i)); index++; IncrementSB(ref str, i);
                        break;
                    }
                    case (char)0x0652:
                        tashkeelLocation.Add(new TashkeelLocation((char)0x0652, i)); index++; IncrementSB(ref str, i);
                        break;
                    case (char)0x0653:
                        tashkeelLocation.Add(new TashkeelLocation((char)0x0653, i)); index++; IncrementSB(ref str, i);
                        break;
                    case (char)0xFC60:
                    case (char)0xFC61:
                    case (char)0xFC62:
                        IncrementSB(ref str, i);
                        break;
                }
            }

            if (lastSplitIndex != 0)
            {
                IncrementSB(ref str, str.Length);
                str = internalStringBuilder.ToString();
            }

            return;

            void IncrementSB(ref string s, int i)
            {
                if (i - lastSplitIndex > 0)
                    internalStringBuilder.Append(s, lastSplitIndex, i - lastSplitIndex);
                lastSplitIndex = i + 1;
            }
        }

        private static void ReturnTashkeel(ref char[] letters, List<TashkeelLocation> tashkeelLocation)
        {
            Array.Resize(ref letters, letters.Length + tashkeelLocation.Count);

            foreach (TashkeelLocation tl in tashkeelLocation)
            {
                for (int j = letters.Length - 1; j > tl.position; j--)
                    letters[j] = letters[j - 1];
                letters[tl.position] = tl.tashkeel;
            }
        }

        internal static string FixLine(string str)
        {
            RemoveTashkeel(ref str, out var tashkeelLocation);

            char[] lettersOrigin = new char[str.Length];
            char[] lettersFinal = str.ToCharArray();

            for (int i = 0; i < lettersOrigin.Length; i++)
                lettersOrigin[i] = (char)ArabicTable.Convert(str[i]);

            for (int i = 0; i < lettersOrigin.Length; i++)
            {
                bool skip = false;

                if (lettersOrigin[i] == (char)IsolatedArabicLetters.Lam && i < lettersOrigin.Length - 1)
                {
                    switch (lettersOrigin[i + 1])
                    {
                        case (char)IsolatedArabicLetters.AlefMaksoor:
                            lettersOrigin[i] = (char)0xFEF7; lettersFinal[i + 1] = (char)0xFFFF; skip = true;
                            break;
                        case (char)IsolatedArabicLetters.Alef:
                            lettersOrigin[i] = (char)0xFEF9; lettersFinal[i + 1] = (char)0xFFFF; skip = true;
                            break;
                        case (char)IsolatedArabicLetters.AlefHamza:
                            lettersOrigin[i] = (char)0xFEF5; lettersFinal[i + 1] = (char)0xFFFF; skip = true;
                            break;
                        case (char)IsolatedArabicLetters.AlefMad:
                            lettersOrigin[i] = (char)0xFEF3; lettersFinal[i + 1] = (char)0xFFFF; skip = true;
                            break;
                    }
                }

                if (!IsIgnoredCharacter(lettersOrigin[i]))
                {
                    if (IsMiddleLetter(lettersOrigin, i)) lettersFinal[i] = (char)(lettersOrigin[i] + 3);
                    else if (IsFinishingLetter(lettersOrigin, i)) lettersFinal[i] = (char)(lettersOrigin[i] + 1);
                    else if (IsLeadingLetter(lettersOrigin, i)) lettersFinal[i] = (char)(lettersOrigin[i] + 2);
                    else lettersFinal[i] = lettersOrigin[i];
                }

                if (skip) i++;

                if (useHinduNumbers)
                    lettersFinal[i] = (char)HandleInduNumber(lettersOrigin[i], lettersFinal[i]);
            }

            if (showTashkeel && tashkeelLocation.Count > 0)
                ReturnTashkeel(ref lettersFinal, tashkeelLocation);

            internalStringBuilder.Clear();
            internalStringBuilder.EnsureCapacity(lettersFinal.Length);

            List<char> numberList = null;

            for (int i = lettersFinal.Length - 1; i >= 0; i--)
            {
                if (char.IsPunctuation(lettersFinal[i]) && i > 0 && i < lettersFinal.Length - 1 &&
                    (char.IsPunctuation(lettersFinal[i - 1]) || char.IsPunctuation(lettersFinal[i + 1])))
                {
                    switch (lettersFinal[i])
                    {
                        case '(':
                            internalStringBuilder.Append(')');
                            break;
                        case ')':
                            internalStringBuilder.Append('(');
                            break;
                        case '<':
                            internalStringBuilder.Append('>');
                            break;
                        case '>':
                            internalStringBuilder.Append('<');
                            break;
                        case '[':
                            internalStringBuilder.Append(']');
                            break;
                        case ']':
                            internalStringBuilder.Append('[');
                            break;
                        default:
                            if (lettersFinal[i] != 0xFFFF) internalStringBuilder.Append(lettersFinal[i]);
                            break;
                    }
                }
                else if (lettersFinal[i] == ' ' && i > 0 && i < lettersFinal.Length - 1 &&
                    (char.IsLower(lettersFinal[i - 1]) || char.IsUpper(lettersFinal[i - 1]) || char.IsNumber(lettersFinal[i - 1])) &&
                    (char.IsLower(lettersFinal[i + 1]) || char.IsUpper(lettersFinal[i + 1]) || char.IsNumber(lettersFinal[i + 1])))
                {
                    AddNumber(lettersFinal[i]);
                }
                else if (char.IsNumber(lettersFinal[i]) || char.IsLower(lettersFinal[i]) || char.IsUpper(lettersFinal[i]) ||
                    char.IsSymbol(lettersFinal[i]) || char.IsPunctuation(lettersFinal[i]))
                {
                    switch (lettersFinal[i])
                    {
                        case '(':
                            AddNumber(')');
                            break;
                        case ')':
                            AddNumber('(');
                            break;
                        case '<':
                            AddNumber('>');
                            break;
                        case '>':
                            AddNumber('<');
                            break;
                        case '[':
                            internalStringBuilder.Append(']');
                            break;
                        case ']':
                            internalStringBuilder.Append('[');
                            break;
                        default:
                            AddNumber(lettersFinal[i]);
                            break;
                    }
                }
                else if ((lettersFinal[i] >= (char)0xD800 && lettersFinal[i] <= (char)0xDBFF) || (lettersFinal[i] >= (char)0xDC00 && lettersFinal[i] <= (char)0xDFFF))
                {
                    AddNumber(lettersFinal[i]);
                }
                else
                {
                    AppendNumbers();
                    if (lettersFinal[i] != 0xFFFF) internalStringBuilder.Append(lettersFinal[i]);
                }
            }
            AppendNumbers();

            return internalStringBuilder.ToString();

            void AppendNumbers()
            {
                if (numberList is { Count: > 0 })
                {
                    for (int j = 0; j < numberList.Count; j++)
                        internalStringBuilder.Append(numberList[numberList.Count - 1 - j]);
                    numberList.Clear();
                }
            }

            void AddNumber(char value) { numberList ??= []; numberList.Add(value); }
        }

        private static ushort HandleInduNumber(ushort letterOrigin, ushort letterFinal)
        {
            return letterOrigin switch
            {
                0x0030 => 0x0660,
                0x0031 => 0x0661,
                0x0032 => 0x0662,
                0x0033 => 0x0663,
                0x0034 => 0x0664,
                0x0035 => 0x0665,
                0x0036 => 0x0666,
                0x0037 => 0x0667,
                0x0038 => 0x0668,
                0x0039 => 0x0669,
                _ => letterFinal
            };
        }

        private static bool IsIgnoredCharacter(char ch)
        {
            bool isPunctuation = char.IsPunctuation(ch);
            bool isNumber = char.IsNumber(ch);
            bool isLower = char.IsLower(ch);
            bool isUpper = char.IsUpper(ch);
            bool isSymbol = char.IsSymbol(ch);
            bool isPersianCharacter = ch is (char)0xFB56 or (char)0xFB7A or (char)0xFB8A or (char)0xFB92 or (char)0xFB8E;
            bool isPresentationFormB = ch is <= (char)0xFEFF and >= (char)0xFE70;
            bool isAcceptableCharacter = isPresentationFormB || isPersianCharacter || ch == (char)0xFBFC;
            return isPunctuation || isNumber || isLower || isUpper || isSymbol || !isAcceptableCharacter || ch == 'a' || ch == '>' || ch == '<' || ch == (char)0x061B;
        }

        private static bool IsLeadingLetter(char[] letters, int index)
        {
            bool lettersThatCannotBeBeforeALeadingLetter = index == 0
                || letters[index - 1] == ' ' || letters[index - 1] == '*' || letters[index - 1] == 'A'
                || char.IsPunctuation(letters[index - 1]) || letters[index - 1] == '>' || letters[index - 1] == '<'
                || letters[index - 1] == (int)IsolatedArabicLetters.Alef || letters[index - 1] == (int)IsolatedArabicLetters.Dal
                || letters[index - 1] == (int)IsolatedArabicLetters.Thal || letters[index - 1] == (int)IsolatedArabicLetters.Ra2
                || letters[index - 1] == (int)IsolatedArabicLetters.Zeen || letters[index - 1] == (int)IsolatedArabicLetters.PersianZe
                || letters[index - 1] == (int)IsolatedArabicLetters.Waw || letters[index - 1] == (int)IsolatedArabicLetters.AlefMad
                || letters[index - 1] == (int)IsolatedArabicLetters.AlefHamza || letters[index - 1] == (int)IsolatedArabicLetters.Hamza
                || letters[index - 1] == (int)IsolatedArabicLetters.AlefMaksoor || letters[index - 1] == (int)IsolatedArabicLetters.WawHamza;

            bool lettersThatCannotBeALeadingLetter = letters[index] != ' '
                && letters[index] != (int)IsolatedArabicLetters.Dal && letters[index] != (int)IsolatedArabicLetters.Thal
                && letters[index] != (int)IsolatedArabicLetters.Ra2 && letters[index] != (int)IsolatedArabicLetters.Zeen
                && letters[index] != (int)IsolatedArabicLetters.PersianZe && letters[index] != (int)IsolatedArabicLetters.Alef
                && letters[index] != (int)IsolatedArabicLetters.AlefHamza && letters[index] != (int)IsolatedArabicLetters.AlefMaksoor
                && letters[index] != (int)IsolatedArabicLetters.AlefMad && letters[index] != (int)IsolatedArabicLetters.WawHamza
                && letters[index] != (int)IsolatedArabicLetters.Waw && letters[index] != (int)IsolatedArabicLetters.Hamza;

            bool lettersThatCannotBeAfterLeadingLetter = index < letters.Length - 1
                && letters[index + 1] != ' ' && letters[index + 1] != '\n' && letters[index + 1] != '\r'
                && !char.IsPunctuation(letters[index + 1]) && !char.IsNumber(letters[index + 1])
                && !char.IsSymbol(letters[index + 1]) && !char.IsLower(letters[index + 1]) && !char.IsUpper(letters[index + 1])
                && letters[index + 1] != (int)IsolatedArabicLetters.Hamza;

            return lettersThatCannotBeBeforeALeadingLetter && lettersThatCannotBeALeadingLetter && lettersThatCannotBeAfterLeadingLetter;
        }

        private static bool IsFinishingLetter(char[] letters, int index)
        {
            bool lettersThatCannotBeBeforeAFinishingLetter = index != 0 &&
                letters[index - 1] != ' ' && letters[index - 1] != (int)IsolatedArabicLetters.Dal
                && letters[index - 1] != (int)IsolatedArabicLetters.Thal && letters[index - 1] != (int)IsolatedArabicLetters.Ra2
                && letters[index - 1] != (int)IsolatedArabicLetters.Zeen && letters[index - 1] != (int)IsolatedArabicLetters.PersianZe
                && letters[index - 1] != (int)IsolatedArabicLetters.Waw && letters[index - 1] != (int)IsolatedArabicLetters.Alef
                && letters[index - 1] != (int)IsolatedArabicLetters.AlefMad && letters[index - 1] != (int)IsolatedArabicLetters.AlefHamza
                && letters[index - 1] != (int)IsolatedArabicLetters.AlefMaksoor && letters[index - 1] != (int)IsolatedArabicLetters.WawHamza
                && letters[index - 1] != (int)IsolatedArabicLetters.Hamza && !char.IsPunctuation(letters[index - 1])
                && !char.IsSymbol(letters[index - 1]) && letters[index - 1] != '>' && letters[index - 1] != '<';

            bool lettersThatCannotBeFinishingLetters = letters[index] != ' ' && letters[index] != (int)IsolatedArabicLetters.Hamza;

            return lettersThatCannotBeBeforeAFinishingLetter && lettersThatCannotBeFinishingLetters;
        }

        private static bool IsMiddleLetter(char[] letters, int index)
        {
            bool lettersThatCannotBeMiddleLetters = index != 0 &&
                letters[index] != (int)IsolatedArabicLetters.Alef && letters[index] != (int)IsolatedArabicLetters.Dal
                && letters[index] != (int)IsolatedArabicLetters.Thal && letters[index] != (int)IsolatedArabicLetters.Ra2
                && letters[index] != (int)IsolatedArabicLetters.Zeen && letters[index] != (int)IsolatedArabicLetters.PersianZe
                && letters[index] != (int)IsolatedArabicLetters.Waw && letters[index] != (int)IsolatedArabicLetters.AlefMad
                && letters[index] != (int)IsolatedArabicLetters.AlefHamza && letters[index] != (int)IsolatedArabicLetters.AlefMaksoor
                && letters[index] != (int)IsolatedArabicLetters.WawHamza && letters[index] != (int)IsolatedArabicLetters.Hamza;

            bool lettersThatCannotBeBeforeMiddleCharacters = index != 0 &&
                letters[index - 1] != (int)IsolatedArabicLetters.Alef && letters[index - 1] != (int)IsolatedArabicLetters.Dal
                && letters[index - 1] != (int)IsolatedArabicLetters.Thal && letters[index - 1] != (int)IsolatedArabicLetters.Ra2
                && letters[index - 1] != (int)IsolatedArabicLetters.Zeen && letters[index - 1] != (int)IsolatedArabicLetters.PersianZe
                && letters[index - 1] != (int)IsolatedArabicLetters.Waw && letters[index - 1] != (int)IsolatedArabicLetters.AlefMad
                && letters[index - 1] != (int)IsolatedArabicLetters.AlefHamza && letters[index - 1] != (int)IsolatedArabicLetters.AlefMaksoor
                && letters[index - 1] != (int)IsolatedArabicLetters.WawHamza && letters[index - 1] != (int)IsolatedArabicLetters.Hamza
                && !char.IsPunctuation(letters[index - 1]) && letters[index - 1] != '>' && letters[index - 1] != '<'
                && letters[index - 1] != ' ' && letters[index - 1] != '*';

            bool lettersThatCannotBeAfterMiddleCharacters = index < letters.Length - 1 && letters[index + 1] != ' '
                && letters[index + 1] != '\r' && letters[index + 1] != (int)IsolatedArabicLetters.Hamza
                && !char.IsNumber(letters[index + 1]) && !char.IsSymbol(letters[index + 1]) && !char.IsPunctuation(letters[index + 1]);

            return lettersThatCannotBeAfterMiddleCharacters && lettersThatCannotBeBeforeMiddleCharacters && lettersThatCannotBeMiddleLetters;
        }
    }
}