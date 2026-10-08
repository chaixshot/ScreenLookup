using CsvHelper;
using Microsoft.Data.Sqlite;
using ScreenLookup.src.models;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace ScreenLookup.src.utils.Database
{
    internal static class HistoryLogger
    {
        static HistoryLogger()
        {
            InitializeDatabase();
        }

        private static void InitializeDatabase()
        {
            using var command = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS history (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Original TEXT,
                    OriginalWords TEXT,
                    Translated TEXT,
                    SourceLanguage INTEGER,
                    TargetLanguage INTEGER
                );", Database.GetConnection());
            command.ExecuteNonQuery();
        }

        public static async Task<int> Add(string original, List<CaptureWordsSimplifiedEntry> originalWords, string translated, int sourceLanguage, int targetLanguage)
        {
            string originalWordsJson = JsonSerializer.Serialize(originalWords, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

            string insertQuery = @"
                INSERT INTO history (Original, OriginalWords, Translated, SourceLanguage, TargetLanguage)
                VALUES (@Original, @OriginalWords, @Translated, @SourceLanguage, @TargetLanguage);

                DELETE FROM history WHERE rowid NOT IN (
                    SELECT rowid FROM history ORDER BY rowid DESC LIMIT 200
                );

                SELECT last_insert_rowid();";

            using var command = new SqliteCommand(insertQuery, Database.GetConnection());

            command.Parameters.AddWithValue("@Original", original);
            command.Parameters.AddWithValue("@OriginalWords", originalWordsJson);
            command.Parameters.AddWithValue("@Translated", translated);
            command.Parameters.AddWithValue("@SourceLanguage", sourceLanguage);
            command.Parameters.AddWithValue("@TargetLanguage", targetLanguage);

            var id = await command.ExecuteScalarAsync();
            return int.Parse(id?.ToString() ?? "0");
        }

        public static void Remove(string id)
        {
            string query = "DELETE FROM history WHERE Id = @Id";

            using var command = new SqliteCommand(query, Database.GetConnection());
            command.Parameters.AddWithValue("@Id", id);
            command.ExecuteNonQuery();
        }

        public static void Update(int id, string translated)
        {
            string query = "UPDATE history SET Translated = @Translated WHERE Id = @Id";

            using var command = new SqliteCommand(query, Database.GetConnection());
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Translated", translated);
            command.ExecuteNonQuery();
        }

        public static void Clear()
        {
            string query = "DELETE FROM history; DELETE FROM sqlite_sequence WHERE NAME='history'";
            using var command = new SqliteCommand(query, Database.GetConnection());
            command.ExecuteNonQuery();
        }

        public static async Task<bool> IsExist(string originalWord)
        {
            string query = "SELECT 1 FROM history WHERE Original = @Original LIMIT 1";

            using var command = new SqliteCommand(query, Database.GetConnection());
            command.Parameters.AddWithValue("@Original", originalWord);
            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync();
        }

        public static async Task<(List<HistoryLoggerPageEntry> Entries, int MaxPage)> LoadAsync(
            int page, int maxRow, string searchText, int searchSourceLanguage, double windowWidth = 800)
        {
            var history = new List<HistoryLoggerPageEntry>();
            int totalCount = 0;

            using (var command = new SqliteCommand(@"
                SELECT COUNT(*) 
                FROM history
                WHERE (Original LIKE @searchText OR Translated LIKE @searchText) AND (SourceLanguage = @searchSourceLanguage or @searchSourceLanguage='-1')", Database.GetConnection()))
            {
                command.Parameters.AddWithValue("@searchText", $"%{searchText}%");
                command.Parameters.AddWithValue("@searchSourceLanguage", $"{searchSourceLanguage}");
                totalCount = Convert.ToInt32(await command.ExecuteScalarAsync());
            }

            int maxPage = Math.Max(1, (int)Math.Ceiling(totalCount / (double)maxRow));
            int offset = Math.Max(0, (page - 1) * maxRow);

            using (var command = new SqliteCommand(@"
                SELECT Id, Original, OriginalWords, Translated, SourceLanguage, TargetLanguage
                FROM history
                WHERE (Original LIKE @searchText OR Translated LIKE @searchText) AND (SourceLanguage = @searchSourceLanguage or @searchSourceLanguage='-1')
                ORDER BY Id DESC
                LIMIT @maxRow OFFSET @offset", Database.GetConnection()))
            {
                command.Parameters.AddWithValue("@searchText", $"%{searchText}%");
                command.Parameters.AddWithValue("@searchSourceLanguage", $"{searchSourceLanguage}");
                command.Parameters.AddWithValue("@maxRow", maxRow);
                command.Parameters.AddWithValue("@offset", offset);

                FontFamily fontFace = new(App.setting.FontFace);
                int fontSizeS = App.setting.FontSizeS;
                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string originalWordsJson = reader.GetString(reader.GetOrdinal("OriginalWords"));
                    string sourceLanguage = reader.GetString(reader.GetOrdinal("SourceLanguage"));
                    string targetLanguage = reader.GetString(reader.GetOrdinal("TargetLanguage"));
                    string translated = reader.GetString(reader.GetOrdinal("Translated"));

                    List<CaptureWordsSimplifiedEntry> captureWordsSmall = JsonSerializer.Deserialize<List<CaptureWordsSimplifiedEntry>>(originalWordsJson) ?? [];
                    List<CaptureWordsEntry> captureWords = Convertor.ConvertCaptureWordsEntry(captureWordsSmall, int.Parse(sourceLanguage), int.Parse(targetLanguage), windowWidth);

                    history.Add(new HistoryLoggerPageEntry
                    {
                        Id = reader.GetString(reader.GetOrdinal("Id")),
                        Original = reader.GetString(reader.GetOrdinal("Original")),
                        OriginalWords = captureWords,
                        ReTranslate = string.IsNullOrEmpty(translated) ? Visibility.Visible : Visibility.Collapsed,
                        Translated = translated,
                        SourceLanguage = sourceLanguage,
                        TargetLanguage = targetLanguage,
                        FontSizeS = fontSizeS,
                        FontFace = fontFace,
                    });
                }
            }
            return (history, maxPage);
        }

        public static async Task ExportToCSV(string filePath)
        {
            var history = new List<HistoryLoggerExportEntry>();

            string query = @"
                SELECT Id, Original, Translated, SourceLanguage, TargetLanguage
                FROM history
                ORDER BY Id DESC";

            using (var command = new SqliteCommand(query, Database.GetConnection()))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    int sourceLanguage = int.Parse(reader.GetString(reader.GetOrdinal("SourceLanguage")));
                    int targetLanguage = int.Parse(reader.GetString(reader.GetOrdinal("TargetLanguage")));

                    history.Add(new HistoryLoggerExportEntry
                    {
                        Original = reader.GetString(reader.GetOrdinal("Original")),
                        Translated = reader.GetString(reader.GetOrdinal("Translated")),
                        SourceLanguage = LanguageList.GetDisplayNameFromID(sourceLanguage, false),
                        TargetLanguage = LanguageList.GetDisplayNameFromID(targetLanguage, false),
                    });
                }
            }

            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
            using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csvWriter.WriteRecordsAsync(history);
        }
    }
}
