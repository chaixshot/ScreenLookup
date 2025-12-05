using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ScreenLookup.src.models
{
    internal class LangugeList
    {
        public static Dictionary<int, string> LanguageShort => new()
        {
            { 0, "en" },
            { 1, "th" },
        };

        public static Dictionary<int, string> LanguageTesseract => new()
        {
            { 0, "eng" },
            { 1, "tha" },
        };

        public static Dictionary<string, int> LanguageIndex => new()
        {
           { "English", 0 },
           { "Thai", 1 },
        };
    }
}
