using ScreenLookup.src.utils;
using System.Windows;
using System.Windows.Media;

namespace ScreenLookup.src.models
{
    public class SavedWordEntry
    {
        public required string Id { get; set; }
        public required string Original { get; set; }
        public required string Translated { get; set; }
        public required string SourceLanguage { get; set; }
        public required string TargetLanguage { get; set; }
        public Visibility ScoreVisibility { get; set; }
        public required FontFamily FontFace { get; set; }

        private Task<(List<ExtraMeaningEntity> ExtraMeanings, string Phonetic)>? _extraDetailsTask;
        private readonly Lock _lock = new();

        public Task<(List<ExtraMeaningEntity> ExtraMeanings, string Phonetic)> GetExtraDetailsAsync()
        {
            lock (_lock)
            {
                _extraDetailsTask ??= Task.Run(async () =>
                {
                    int srcLang = int.TryParse(SourceLanguage, out int s) ? s : 0;
                    int tgtLang = int.TryParse(TargetLanguage, out int t) ? t : 0;
                    using var cts = new CancellationTokenSource();
                    return await Translation.GetExtraDetailsAsync(Original, srcLang, tgtLang, cts);
                });
                return _extraDetailsTask;
            }
        }
    }
}
