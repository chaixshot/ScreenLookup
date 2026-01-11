using CsvHelper;
using Microsoft.Data.Sqlite;
using ScreenLookup.src.models;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace ScreenLookup.src.utils
{
    internal class HistoryLogger
    {
        public static readonly string CONNECTION_STRING = $"Data Source={Path.Combine(App.appDataFolder, "database.db")}";

        private static SqliteConnection _sharedConnection;
        private static readonly object _connectionLock = new object();

        static HistoryLogger()
        {
            InitializeDatabase();
        }

        private static void InitializeDatabase()
        {
            GetConnection();

            using var command = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS history (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Original TEXT,
                    OriginalWords TEXT,
                    Translated TEXT,
                    SourceLanguage INTEGER,
                    TargetLanguage INTEGER
                );", GetConnection());
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

        public static async Task<int> Add(string Original, List<CaptureWordsSimplifiedEntry> OriginalWords, string Translated, int SourceLanguage, int TargetLanguage)
        {
            var originalWordsJson = JsonSerializer.Serialize(OriginalWords);

            string insertQuery = @"
                INSERT INTO history (Original, OriginalWords, Translated, SourceLanguage, TargetLanguage)
                VALUES (@Original, @OriginalWords, @Translated, @SourceLanguage, @TargetLanguage);

                SELECT last_insert_rowid();";

            using var command = new SqliteCommand(insertQuery, GetConnection());

            command.Parameters.AddWithValue("@Original", Original);
            command.Parameters.AddWithValue("@OriginalWords", originalWordsJson);
            command.Parameters.AddWithValue("@Translated", Translated);
            command.Parameters.AddWithValue("@SourceLanguage", SourceLanguage);
            command.Parameters.AddWithValue("@TargetLanguage", TargetLanguage);

            var ID = await command.ExecuteScalarAsync();
            return Int32.Parse(ID.ToString());
        }

        public static void Remove(string Id)
        {
            string insertQuery = @"
                 DELETE FROM history WHERE Id = @Id";

            using var command = new SqliteCommand(insertQuery, GetConnection());

            command.Parameters.AddWithValue("@Id", Id);

            command.ExecuteNonQuery();
        }

        public static void Update(int Id, string Translated)
        {
            string insertQuery = @"
                  UPDATE history SET Translated = @Translated WHERE Id = @Id";

            using var command = new SqliteCommand(insertQuery, GetConnection());

            command.Parameters.AddWithValue("@Id", Id);
            command.Parameters.AddWithValue("@Translated", Translated);

            command.ExecuteNonQuery();
        }

        public static void Clear()
        {
            string selectQuery = "DELETE FROM history; DELETE FROM sqlite_sequence WHERE NAME='history'";
            using var command = new SqliteCommand(selectQuery, GetConnection());
            command.ExecuteNonQuery();
        }

        public static async Task<bool> IsExist(string originalWord)
        {
            string selectQuery = @"
                 SELECT 1 FROM history WHERE Original = @Original LIMIT 1";

            using var command = new SqliteCommand(selectQuery, GetConnection());
            command.Parameters.AddWithValue("@Original", originalWord);
            using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync();
        }

        public static async Task<(List<HistoryLoggerPageEntry>, int)> LoadAsync(
            int page, int maxRow, string searchText, int searchSourceLanguage)
        {
            var history = new List<HistoryLoggerPageEntry>();
            int totalCount = 0;
            using (var command = new SqliteCommand(@"
                SELECT COUNT(*) 
                FROM history
                WHERE (Original LIKE @searchText OR Translated LIKE @searchText) AND (SourceLanguage = @searchSourceLanguage or @searchSourceLanguage='-1')", GetConnection()))
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
                LIMIT @maxRow OFFSET @offset", GetConnection()))
            {
                command.Parameters.AddWithValue("@searchText", $"%{searchText}%");
                command.Parameters.AddWithValue("@searchSourceLanguage", $"{searchSourceLanguage}");
                command.Parameters.AddWithValue("@maxRow", maxRow);
                command.Parameters.AddWithValue("@offset", offset);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string OriginalWords = reader.GetString(reader.GetOrdinal("OriginalWords"));
                    string SourceLanguage = reader.GetString(reader.GetOrdinal("SourceLanguage"));
                    string TargetLanguage = reader.GetString(reader.GetOrdinal("TargetLanguage"));
                    string Translated = reader.GetString(reader.GetOrdinal("Translated"));

                    List<CaptureWordsSimplifiedEntry> captureWordsSmall = JsonSerializer.Deserialize<List<CaptureWordsSimplifiedEntry>>(OriginalWords);
                    List<CaptureWordsEntry> captureWords = Convertor.ConvertCaptureWordsEntry(captureWordsSmall, Int32.Parse(SourceLanguage), Int32.Parse(TargetLanguage), App.mainWindow.Width);

                    history.Add(new HistoryLoggerPageEntry
                    {
                        Id = reader.GetString(reader.GetOrdinal("Id")),
                        Original = reader.GetString(reader.GetOrdinal("Original")),
                        OriginalWords = captureWords,
                        ReTranslate = string.IsNullOrEmpty(Translated) ? Visibility.Visible : Visibility.Collapsed,
                        Translated = Translated,
                        SourceLanguage = SourceLanguage,
                        TargetLanguage = TargetLanguage,
                        FontSizeS = App.setting.FontSizeS,
                        FontFace = new FontFamily(App.setting.FontFace),
                    });
                }
            }
            return (history, maxPage);
        }

        public static async Task ExportToCSV(string filePath)
        {
            var history = new List<HistoryLoggerExportEntry>();

            string selectQuery = @"
                SELECT Id, Original, Translated, SourceLanguage, TargetLanguage
                FROM history
                ORDER BY Id DESC";

            using (var command = new SqliteCommand(selectQuery, GetConnection()))
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    int sourceLanguage = Int32.Parse(reader.GetString(reader.GetOrdinal("SourceLanguage")));
                    int targetLanguage = Int32.Parse(reader.GetString(reader.GetOrdinal("TargetLanguage")));

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
