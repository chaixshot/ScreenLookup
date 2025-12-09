using CsvHelper;
using Microsoft.Data.Sqlite;
using ScreenLookup.src.models;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Media;

namespace ScreenLookup.src.utils
{
    internal class HistoryLogger
    {
        private static readonly string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
        public static readonly string CONNECTION_STRING = $"Data Source={appData}\\ScreenLookup\\database.db;";

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
                    SourceLanguage TEXT,
                    TargetLanguage TEXT
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

        public static async Task Add(string originalWord, List<CaptureWordsEntrySimplify> originalWords, string translatedWord, int sourceLanguage, int sargetLanguage, CancellationToken token = default)
        {
            var originalWordsJson = JsonSerializer.Serialize(originalWords);

            string insertQuery = @"
                INSERT INTO history (Original, OriginalWords, Translated, SourceLanguage, TargetLanguage)
                VALUES (@Original, @OriginalWords, @Translated, @SourceLanguage, @TargetLanguage)";

            using var command = new SqliteCommand(insertQuery, GetConnection());
            command.Parameters.AddWithValue("@Original", originalWord);
            command.Parameters.AddWithValue("@OriginalWords", originalWordsJson);
            command.Parameters.AddWithValue("@Translated", translatedWord);
            command.Parameters.AddWithValue("@SourceLanguage", sourceLanguage);
            command.Parameters.AddWithValue("@TargetLanguage", sargetLanguage);
            await command.ExecuteNonQueryAsync(token);
        }

        public static async Task Remove(string Id, CancellationToken token = default)
        {
            string insertQuery = @"
                 DELETE FROM history WHERE Id = @Id";

            using var command = new SqliteCommand(insertQuery, GetConnection());
            command.Parameters.AddWithValue("@Id", Id);
            await command.ExecuteNonQueryAsync(token);
        }

        public static async Task Clear(CancellationToken token = default)
        {
            string selectQuery = "DELETE FROM history; DELETE FROM sqlite_sequence WHERE NAME='history'";
            using var command = new SqliteCommand(selectQuery, GetConnection());
            await command.ExecuteNonQueryAsync(token);
        }

        public static async Task<bool> IsExist(string originalWord, CancellationToken token = default)
        {
            string selectQuery = @"
                 SELECT 1 FROM history WHERE Original = @Original LIMIT 1";

            using var command = new SqliteCommand(selectQuery, GetConnection());
            command.Parameters.AddWithValue("@Original", originalWord);
            using var reader = await command.ExecuteReaderAsync(token);
            return await reader.ReadAsync(token);
        }

        public static async Task<(List<HistoryLoggerPageEntry>, int)> LoadAsync(
            int page, int maxRow, string searchText, CancellationToken token = default)
        {
            var history = new List<HistoryLoggerPageEntry>();
            int totalCount = 0;
            using (var command = new SqliteCommand(@"
                SELECT COUNT(*) 
                FROM history
                WHERE Original LIKE @search OR Translated LIKE @search", GetConnection()))
            {
                command.Parameters.AddWithValue("@search", $"%{searchText}%");
                totalCount = Convert.ToInt32(await command.ExecuteScalarAsync(token));
            }

            int maxPage = Math.Max(1, (int)Math.Ceiling(totalCount / (double)maxRow));
            int offset = Math.Max(0, (page - 1) * maxRow);

            using (var command = new SqliteCommand(@"
                SELECT Id, Original, OriginalWords, Translated, SourceLanguage, TargetLanguage
                FROM history
                WHERE Original LIKE @search OR Translated LIKE @search
                ORDER BY Id DESC
                LIMIT @maxRow OFFSET @offset", GetConnection()))

            {
                command.Parameters.AddWithValue("@search", $"%{searchText}%");
                command.Parameters.AddWithValue("@maxRow", maxRow);
                command.Parameters.AddWithValue("@offset", offset);

                using var reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    string originalWords = reader.GetString(reader.GetOrdinal("OriginalWords"));
                    string sourceLanguage = reader.GetString(reader.GetOrdinal("SourceLanguage"));
                    string targetLanguage = reader.GetString(reader.GetOrdinal("TargetLanguage"));
                    List<CaptureWordsEntrySimplify> captureWordsSmall = JsonSerializer.Deserialize<List<CaptureWordsEntrySimplify>>(originalWords);
                    List<CaptureWordsEntry> captureWords = Convertor.ConvertCaptureWordsEntry(captureWordsSmall, Int32.Parse(sourceLanguage), Int32.Parse(targetLanguage));

                    history.Add(new HistoryLoggerPageEntry
                    {
                        Id = reader.GetString(reader.GetOrdinal("Id")),
                        Original = reader.GetString(reader.GetOrdinal("Original")),
                        OriginalWords = captureWords,
                        Translated = reader.GetString(reader.GetOrdinal("Translated")),
                        SourceLanguage = sourceLanguage,
                        TargetLanguage = targetLanguage,
                        FontSizeS = Setting.FontSizeS,
                        FontFace = new FontFamily(Setting.FontFace),
                    });
                }
            }
            return (history, maxPage);
        }

        public static async Task ExportToCSV(string filePath, CancellationToken token = default)
        {
            var history = new List<HistoryLoggerExportEntry>();

            string selectQuery = @"
                SELECT Id, Original, Translated, SourceLanguage, TargetLanguage
                FROM history
                ORDER BY Id DESC";

            using (var command = new SqliteCommand(selectQuery, GetConnection()))
            using (var reader = await command.ExecuteReaderAsync(token))
            {
                while (await reader.ReadAsync(token))
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
            await csvWriter.WriteRecordsAsync(history, token);
        }
    }
}
