using Microsoft.Data.Sqlite;
using System.IO;

namespace ScreenLookup.src.utils
{
    internal class WordClassLogger
    {
        public static readonly string CONNECTION_STRING = $"Data Source={Path.Combine(App.appDataFolder, "database.db")}";

        private static SqliteConnection? _sharedConnection;
        private static readonly Lock _connectionLock = new();

        static WordClassLogger()
        {
            InitializeDatabase();
        }

        private static void InitializeDatabase()
        {
            GetConnection();

            using var command = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS word_class (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Original TEXT,
                    Translated TEXT,
                    TargetLanguage INTEGER
                );
                CREATE INDEX IF NOT EXISTS idx_word_class_lookup
                ON word_class (Original, TargetLanguage);
            ", GetConnection());
            command.ExecuteNonQuery();
        }

        private static SqliteConnection GetConnection()
        {
            lock (_connectionLock)
            {
                if (_sharedConnection == null)
                {
                    _sharedConnection = new SqliteConnection(CONNECTION_STRING);
                    _sharedConnection.Open();
                }
                else if (_sharedConnection.State != System.Data.ConnectionState.Open)
                {
                    try
                    {
                        _sharedConnection.Open();
                    }
                    catch
                    {
                        _sharedConnection?.Dispose();
                        _sharedConnection = new SqliteConnection(CONNECTION_STRING);
                        _sharedConnection.Open();
                    }
                }

                return _sharedConnection;
            }
        }

        public static async Task<string?> GetAsync(string original, int targetLang)
        {
            string selectQuery = @"
                SELECT Translated
                FROM word_class
                WHERE Original = @Original AND TargetLanguage = @TargetLanguage
                LIMIT 1";

            using var command = new SqliteCommand(selectQuery, GetConnection());
            command.Parameters.AddWithValue("@Original", original.Trim());
            command.Parameters.AddWithValue("@TargetLanguage", targetLang);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return reader.IsDBNull(0) ? null : reader.GetString(0);
            }

            return null;
        }

        public static async Task SaveAsync(string original, string translated, int targetLang)
        {
            if (string.IsNullOrWhiteSpace(original) || string.IsNullOrWhiteSpace(translated)) return;

            string? existing = await GetAsync(original, targetLang);
            if (existing != null)
            {
                string updateQuery = @"
                    UPDATE word_class
                    SET Translated = @Translated
                    WHERE Original = @Original AND TargetLanguage = @TargetLanguage";

                using var cmd = new SqliteCommand(updateQuery, GetConnection());
                cmd.Parameters.AddWithValue("@Original", original.Trim());
                cmd.Parameters.AddWithValue("@Translated", translated.Trim());
                cmd.Parameters.AddWithValue("@TargetLanguage", targetLang);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                string insertQuery = @"
                    INSERT INTO word_class (Original, Translated, TargetLanguage)
                    VALUES (@Original, @Translated, @TargetLanguage)";

                using var cmd = new SqliteCommand(insertQuery, GetConnection());
                cmd.Parameters.AddWithValue("@Original", original.Trim());
                cmd.Parameters.AddWithValue("@Translated", translated.Trim());
                cmd.Parameters.AddWithValue("@TargetLanguage", targetLang);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public static async Task<string> GetOrTranslateAsync(string pos, int targetLang)
        {
            if (string.IsNullOrWhiteSpace(pos))
                return string.Empty;

            string cleanPos = pos.Trim().Normalize(System.Text.NormalizationForm.FormC);

            // If target language is English, word classes are already in English
            string targetIso = LanguageList.GetLanguageISO6391FromID(targetLang);
            if (targetIso == "en")
                return cleanPos;

            // Check local SQLite cache first
            try
            {
                string? cached = await GetAsync(cleanPos, targetLang);
                if (!string.IsNullOrWhiteSpace(cached))
                    return cached;
            }
            catch { }

            try
            {
                // Find English language ID for translation source
                int engLangID = Array.IndexOf(TesseractHelper.LangList, "eng");
                string translatedPos = await Translation.GetTranslated(isWord: true, cleanPos, engLangID, targetLang);
                if (!string.IsNullOrWhiteSpace(translatedPos))
                {
                    translatedPos = translatedPos.Trim().Normalize(System.Text.NormalizationForm.FormC);
                    await SaveAsync(cleanPos, translatedPos, targetLang);
                    return translatedPos;
                }
            }
            catch { }

            return cleanPos;
        }
    }
}
