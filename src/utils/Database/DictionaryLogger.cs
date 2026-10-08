using Microsoft.Data.Sqlite;
using ScreenLookup.src.models;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ScreenLookup.src.utils.Database
{
    internal static class DictionaryLogger
    {
        static DictionaryLogger()
        {
            InitializeDatabase();
        }

        private static void InitializeDatabase()
        {
            using var command = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS dictionary (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Original TEXT,
                    Translated TEXT,
                    SourceLanguage INTEGER,
                    TargetLanguage INTEGER,
                    ProviderServices INTEGER,
                    ExtraMeanings TEXT,
                    Phonetic TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_dict_lookup
                ON dictionary (Original, SourceLanguage, TargetLanguage, ProviderServices);
            ", Database.GetConnection());
            command.ExecuteNonQuery();
        }

        public static async Task<DictionaryEntry?> GetAsync(string original, int sourceLang, int targetLang, int providerID)
        {
            string query = @"
                SELECT Original, Translated, SourceLanguage, TargetLanguage, ProviderServices, ExtraMeanings, Phonetic
                FROM dictionary
                WHERE Original = @Original AND SourceLanguage = @SourceLanguage AND TargetLanguage = @TargetLanguage AND ProviderServices = @ProviderServices
                LIMIT 1";

            using var command = new SqliteCommand(query, Database.GetConnection());
            command.Parameters.AddWithValue("@Original", original.Trim());
            command.Parameters.AddWithValue("@SourceLanguage", sourceLang);
            command.Parameters.AddWithValue("@TargetLanguage", targetLang);
            command.Parameters.AddWithValue("@ProviderServices", providerID);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                string translated = reader.IsDBNull(reader.GetOrdinal("Translated")) ? string.Empty : reader.GetString(reader.GetOrdinal("Translated"));
                string extraMeaningsJson = reader.IsDBNull(reader.GetOrdinal("ExtraMeanings")) ? string.Empty : reader.GetString(reader.GetOrdinal("ExtraMeanings"));
                string phonetic = reader.IsDBNull(reader.GetOrdinal("Phonetic")) ? string.Empty : reader.GetString(reader.GetOrdinal("Phonetic"));

                List<ExtraMeaningEntity>? extraMeanings = null;
                if (!string.IsNullOrWhiteSpace(extraMeaningsJson))
                {
                    try
                    {
                        extraMeanings = JsonSerializer.Deserialize<List<ExtraMeaningEntity>>(extraMeaningsJson);
                    }
                    catch { }
                }

                return new DictionaryEntry
                {
                    Original = reader.GetString(reader.GetOrdinal("Original")),
                    Translated = translated,
                    SourceLanguage = reader.GetInt32(reader.GetOrdinal("SourceLanguage")),
                    TargetLanguage = reader.GetInt32(reader.GetOrdinal("TargetLanguage")),
                    ProviderServices = reader.GetInt32(reader.GetOrdinal("ProviderServices")),
                    ExtraMeanings = extraMeanings,
                    Phonetic = phonetic
                };
            }

            return null;
        }

        public static async Task SaveTranslatedAsync(string original, string translated, int sourceLang, int targetLang, int providerID)
        {
            if (string.IsNullOrWhiteSpace(translated)) return;

            var existing = await GetAsync(original, sourceLang, targetLang, providerID);
            if (existing != null)
            {
                string updateQuery = @"
                    UPDATE dictionary
                    SET Translated = @Translated
                    WHERE Original = @Original AND SourceLanguage = @SourceLanguage AND TargetLanguage = @TargetLanguage AND ProviderServices = @ProviderServices";

                using var cmd = new SqliteCommand(updateQuery, Database.GetConnection());
                cmd.Parameters.AddWithValue("@Original", original.Trim());
                cmd.Parameters.AddWithValue("@Translated", translated.Trim());
                cmd.Parameters.AddWithValue("@SourceLanguage", sourceLang);
                cmd.Parameters.AddWithValue("@TargetLanguage", targetLang);
                cmd.Parameters.AddWithValue("@ProviderServices", providerID);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                string insertQuery = @"
                    INSERT INTO dictionary (Original, Translated, SourceLanguage, TargetLanguage, ProviderServices, ExtraMeanings, Phonetic)
                    VALUES (@Original, @Translated, @SourceLanguage, @TargetLanguage, @ProviderServices, '', '')";

                using var cmd = new SqliteCommand(insertQuery, Database.GetConnection());
                cmd.Parameters.AddWithValue("@Original", original.Trim());
                cmd.Parameters.AddWithValue("@Translated", translated.Trim());
                cmd.Parameters.AddWithValue("@SourceLanguage", sourceLang);
                cmd.Parameters.AddWithValue("@TargetLanguage", targetLang);
                cmd.Parameters.AddWithValue("@ProviderServices", providerID);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public static async Task SaveExtraDetailsAsync(string original, int sourceLang, int targetLang, int providerID, List<ExtraMeaningEntity> extraMeanings, string phonetic)
        {
            if (extraMeanings == null || phonetic == null) return;

            // Serialize using relaxed JSON escaping to preserve raw UTF-8 text
            string json = JsonSerializer.Serialize(extraMeanings, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            var existing = await GetAsync(original, sourceLang, targetLang, providerID);

            if (existing != null)
            {
                string updateQuery = @"
                    UPDATE dictionary 
                    SET ExtraMeanings = @ExtraMeanings, Phonetic = @Phonetic
                    WHERE Original = @Original AND SourceLanguage = @SourceLanguage AND TargetLanguage = @TargetLanguage AND ProviderServices = @ProviderServices";

                using var cmd = new SqliteCommand(updateQuery, Database.GetConnection());
                cmd.Parameters.AddWithValue("@Original", original);
                cmd.Parameters.AddWithValue("@ExtraMeanings", json);
                cmd.Parameters.AddWithValue("@Phonetic", phonetic);
                cmd.Parameters.AddWithValue("@SourceLanguage", sourceLang);
                cmd.Parameters.AddWithValue("@TargetLanguage", targetLang);
                cmd.Parameters.AddWithValue("@ProviderServices", providerID);

                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                string insertQuery = @"
                    INSERT INTO dictionary (Original, Translated, SourceLanguage, TargetLanguage, ProviderServices, ExtraMeanings, Phonetic)
                    VALUES (@Original, '', @SourceLanguage, @TargetLanguage, @ProviderServices, @ExtraMeanings, @Phonetic)";

                using var cmd = new SqliteCommand(insertQuery, Database.GetConnection());
                cmd.Parameters.AddWithValue("@Original", original);
                cmd.Parameters.AddWithValue("@ExtraMeanings", json);
                cmd.Parameters.AddWithValue("@Phonetic", phonetic);
                cmd.Parameters.AddWithValue("@SourceLanguage", sourceLang);
                cmd.Parameters.AddWithValue("@TargetLanguage", targetLang);
                cmd.Parameters.AddWithValue("@ProviderServices", providerID);

                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
