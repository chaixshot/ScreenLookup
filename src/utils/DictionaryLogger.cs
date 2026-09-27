using Microsoft.Data.Sqlite;
using ScreenLookup.src.models;
using System.IO;
using System.Text.Json;

namespace ScreenLookup.src.utils
{
    internal class DictionaryLogger
    {
        public static readonly string CONNECTION_STRING = $"Data Source={Path.Combine(App.appDataFolder, "database.db")}";

        private static SqliteConnection _sharedConnection;
        private static readonly object _connectionLock = new();

        static DictionaryLogger()
        {
            InitializeDatabase();
        }

        private static void InitializeDatabase()
        {
            GetConnection();

            using var command = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS dictionary (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Original TEXT,
                    Translated TEXT,
                    SourceLanguage INTEGER,
                    TargetLanguage INTEGER,
                    ProviderServices INTEGER,
                    ExtraMeanings TEXT
                );
                CREATE INDEX IF NOT EXISTS idx_dict_lookup
                ON dictionary (Original, SourceLanguage, TargetLanguage, ProviderServices);
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
                        _sharedConnection.Dispose();
                        _sharedConnection = new SqliteConnection(CONNECTION_STRING);
                        _sharedConnection.Open();
                    }
                }

                return _sharedConnection;
            }
        }

        public static async Task<DictionaryEntry?> GetAsync(string original, int sourceLang, int targetLang, int providerID)
        {
            if (!IsSingleWordOrShortPhrase(original)) return null;

            string selectQuery = @"
                SELECT Original, Translated, SourceLanguage, TargetLanguage, ProviderServices, ExtraMeanings
                FROM dictionary
                WHERE Original = @Original AND SourceLanguage = @SourceLanguage AND TargetLanguage = @TargetLanguage AND ProviderServices = @ProviderServices
                LIMIT 1";

            using var command = new SqliteCommand(selectQuery, GetConnection());
            command.Parameters.AddWithValue("@Original", original.Trim());
            command.Parameters.AddWithValue("@SourceLanguage", sourceLang);
            command.Parameters.AddWithValue("@TargetLanguage", targetLang);
            command.Parameters.AddWithValue("@ProviderServices", providerID);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                string translated = reader.IsDBNull(reader.GetOrdinal("Translated")) ? string.Empty : reader.GetString(reader.GetOrdinal("Translated"));
                string extraMeaningsJson = reader.IsDBNull(reader.GetOrdinal("ExtraMeanings")) ? string.Empty : reader.GetString(reader.GetOrdinal("ExtraMeanings"));

                List<ExtraMeaning>? extraMeanings = null;
                if (!string.IsNullOrWhiteSpace(extraMeaningsJson))
                {
                    try
                    {
                        extraMeanings = JsonSerializer.Deserialize<List<ExtraMeaning>>(extraMeaningsJson);
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
                    ExtraMeanings = extraMeanings
                };
            }

            return null;
        }

        public static async Task SaveTranslatedAsync(string original, string translated, int sourceLang, int targetLang, int providerID)
        {
            if (!IsSingleWordOrShortPhrase(original) || string.IsNullOrWhiteSpace(translated)) return;

            var existing = await GetAsync(original, sourceLang, targetLang, providerID);
            if (existing != null)
            {
                string updateQuery = @"
                    UPDATE dictionary
                    SET Translated = @Translated
                    WHERE Original = @Original AND SourceLanguage = @SourceLanguage AND TargetLanguage = @TargetLanguage AND ProviderServices = @ProviderServices";

                using var cmd = new SqliteCommand(updateQuery, GetConnection());
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
                    INSERT INTO dictionary (Original, Translated, SourceLanguage, TargetLanguage, ProviderServices, ExtraMeanings)
                    VALUES (@Original, @Translated, @SourceLanguage, @TargetLanguage, @ProviderServices, '')";

                using var cmd = new SqliteCommand(insertQuery, GetConnection());
                cmd.Parameters.AddWithValue("@Original", original.Trim());
                cmd.Parameters.AddWithValue("@Translated", translated.Trim());
                cmd.Parameters.AddWithValue("@SourceLanguage", sourceLang);
                cmd.Parameters.AddWithValue("@TargetLanguage", targetLang);
                cmd.Parameters.AddWithValue("@ProviderServices", providerID);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        public static async Task SaveExtraMeaningsAsync(string original, int sourceLang, int targetLang, int providerID, List<ExtraMeaning> extraMeanings)
        {
            if (!IsSingleWordOrShortPhrase(original) || extraMeanings == null || extraMeanings.Count == 0) return;

            string json = JsonSerializer.Serialize(extraMeanings);

            var existing = await GetAsync(original, sourceLang, targetLang, providerID);
            if (existing != null)
            {
                string updateQuery = @"
                    UPDATE dictionary
                    SET ExtraMeanings = @ExtraMeanings
                    WHERE Original = @Original AND SourceLanguage = @SourceLanguage AND TargetLanguage = @TargetLanguage AND ProviderServices = @ProviderServices";

                using var cmd = new SqliteCommand(updateQuery, GetConnection());
                cmd.Parameters.AddWithValue("@Original", original.Trim());
                cmd.Parameters.AddWithValue("@ExtraMeanings", json);
                cmd.Parameters.AddWithValue("@SourceLanguage", sourceLang);
                cmd.Parameters.AddWithValue("@TargetLanguage", targetLang);
                cmd.Parameters.AddWithValue("@ProviderServices", providerID);
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                string insertQuery = @"
                    INSERT INTO dictionary (Original, Translated, SourceLanguage, TargetLanguage, ProviderServices, ExtraMeanings)
                    VALUES (@Original, '', @SourceLanguage, @TargetLanguage, @ProviderServices, @ExtraMeanings)";

                using var cmd = new SqliteCommand(insertQuery, GetConnection());
                cmd.Parameters.AddWithValue("@Original", original.Trim());
                cmd.Parameters.AddWithValue("@ExtraMeanings", json);
                cmd.Parameters.AddWithValue("@SourceLanguage", sourceLang);
                cmd.Parameters.AddWithValue("@TargetLanguage", targetLang);
                cmd.Parameters.AddWithValue("@ProviderServices", providerID);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
