using CsvHelper;
using Microsoft.Data.Sqlite;
using ScreenLookup.src.models;
using System.Globalization;
using System.IO;
using System.Text;

namespace ScreenLookup.src.utils
{
    public static class SavedWord
    {
        private static readonly string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
        public static readonly string CONNECTION_STRING = $"Data Source={appData}\\ScreenLookup\\database.db;";

        private static SqliteConnection _sharedConnection;
        private static readonly object _connectionLock = new object();

        static SavedWord()
        {
            InitializeDatabase();
        }

        private static void InitializeDatabase()
        {
            GetConnection();

            using (var command = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS savedword (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Original TEXT,
                    Translated TEXT,
                    SourceLanguage TEXT,
                    TargetLanguage TEXT
                );", GetConnection()))
            {
                command.ExecuteNonQuery();
            }
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

        public static async Task Add(string originalWord, string translatedWord, int sourceLanguage, int sargetLanguage, CancellationToken token = default)
        {
            string insertQuery = @"
                INSERT INTO savedword (Original, Translated, SourceLanguage, TargetLanguage)
                VALUES (@Original, @Translated, @SourceLanguage, @TargetLanguage)";

            using (var command = new SqliteCommand(insertQuery, GetConnection()))
            {
                command.Parameters.AddWithValue("@Original", originalWord);
                command.Parameters.AddWithValue("@Translated", translatedWord);
                command.Parameters.AddWithValue("@SourceLanguage", sourceLanguage);
                command.Parameters.AddWithValue("@TargetLanguage", sargetLanguage);
                await command.ExecuteNonQueryAsync(token);
            }
        }

        public static async Task Remove(string Id, CancellationToken token = default)
        {
            string insertQuery = @"
                 DELETE FROM savedword WHERE Id = @Id";

            using (var command = new SqliteCommand(insertQuery, GetConnection()))
            {
                command.Parameters.AddWithValue("@Id", Id);
                await command.ExecuteNonQueryAsync(token);
            }
        }

        public static async Task Clear(CancellationToken token = default)
        {
            string selectQuery = "DELETE FROM savedword; DELETE FROM sqlite_sequence WHERE NAME='savedword'";
            using (var command = new SqliteCommand(selectQuery, GetConnection()))
            {
                await command.ExecuteNonQueryAsync(token);
            }
        }

        public static async Task<bool> IsExist(string originalWord, CancellationToken token = default)
        {
            string selectQuery = @"
                 SELECT 1 FROM savedword WHERE Original = @Original LIMIT 1";

            using (var command = new SqliteCommand(selectQuery, GetConnection()))
            {
                command.Parameters.AddWithValue("@Original", originalWord);
                using (var reader = await command.ExecuteReaderAsync(token))
                {
                    return await reader.ReadAsync(token);
                }
            }
        }

        public static async Task<(List<SavedWordEntry>, int)> LoadAsync(
            int page, int maxRow, string searchText, CancellationToken token = default)
        {
            var history = new List<SavedWordEntry>();
            int maxPage = 1;
            using (var command = new SqliteCommand(@$"SELECT COUNT() AS maxPage
                FROM savedword
                WHERE Original LIKE '%{searchText}%' OR Translated LIKE '%{searchText}%'",
                GetConnection()))
            {
                maxPage = Convert.ToInt32(await command.ExecuteScalarAsync(token)) / maxRow;
            }

            using (var command = new SqliteCommand(@$"
                SELECT Id, Original, Translated, SourceLanguage, TargetLanguage
                FROM savedword
                WHERE Original LIKE '%{searchText}%' OR Translated LIKE '%{searchText}%'
                LIMIT " + maxRow + " OFFSET " + (page * maxRow - maxRow),
                GetConnection()))
            using (var reader = await command.ExecuteReaderAsync(token))
            {
                while (await reader.ReadAsync(token))
                {
                    history.Add(new SavedWordEntry
                    {
                        Id = reader.GetString(reader.GetOrdinal("Id")),
                        Original = reader.GetString(reader.GetOrdinal("Original")),
                        Translated = reader.GetString(reader.GetOrdinal("Translated")),
                        SourceLanguage = reader.GetString(reader.GetOrdinal("SourceLanguage")),
                        TargetLanguage = reader.GetString(reader.GetOrdinal("TargetLanguage")),
                    });
                }
            }
            return (history, maxPage);
        }

        public static async Task ExportToCSV(string filePath, CancellationToken token = default)
        {
            var history = new List<SavedWordEntry>();

            string selectQuery = @"
                SELECT Id, Original, Translated, SourceLanguage, TargetLanguage
                FROM savedword";

            using (var command = new SqliteCommand(selectQuery, GetConnection()))
            using (var reader = await command.ExecuteReaderAsync(token))
            {
                while (await reader.ReadAsync(token))
                {
                    history.Add(new SavedWordEntry
                    {
                        Id = reader.GetString(reader.GetOrdinal("Id")),
                        Original = reader.GetString(reader.GetOrdinal("Original")),
                        Translated = reader.GetString(reader.GetOrdinal("Translated")),
                        SourceLanguage = reader.GetString(reader.GetOrdinal("SourceLanguage")),
                        TargetLanguage = reader.GetString(reader.GetOrdinal("TargetLanguage")),
                    });
                }
            }

            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
            using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csvWriter.WriteRecordsAsync(history, token);
        }
    }
}
