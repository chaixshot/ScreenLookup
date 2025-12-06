using System;
using System.Collections.Generic;
using System.Text;

namespace ScreenLookup.src.models
{
    public class SaveWordEntry
    {
        public required string Original { get; set; }
        public required string Translated { get; set; }
        public required string SourceLanguage { get; set; }
        public required string TargetLanguage { get; set; }
    }
}
