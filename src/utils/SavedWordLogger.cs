﻿using CsvHelper;
using Microsoft.Data.Sqlite;
using ScreenLookup.src.models;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace ScreenLookup.src.utils
{
    public static class SavedWordLogger
    {
        public static readonly string CONNECTION_STRING = $"Data Source={Path.Combine(App.appDataFolder, "database.db")}";

        private static SqliteConnection _sharedConnection;
        private static readonly object _connectionLock = new();

        static SavedWordLogger()
        {
            InitializeDatabase();
        }

        private static void InitializeDatabase()
        {
            GetConnection();

            using SqliteCommand command = new(@"
                CREATE TABLE IF NOT EXISTS savedword (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Original TEXT,
                    Translated TEXT,
                    SourceLanguage INTEGER,
                    TargetLanguage INTEGER,
                    Score INTEGER
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

        public static void Add(string originalWord, string translatedWord, int sourceLanguage, int sargetLanguage)
        {
            string insertQuery = @"
                INSERT INTO savedword (Original, Translated, SourceLanguage, TargetLanguage)
                VALUES (@Original, @Translated, @SourceLanguage, @TargetLanguage)";

            using SqliteCommand command = new(insertQuery, GetConnection());

            command.Parameters.AddWithValue("@Original", originalWord);
            command.Parameters.AddWithValue("@Translated", translatedWord);
            command.Parameters.AddWithValue("@SourceLanguage", sourceLanguage);
            command.Parameters.AddWithValue("@TargetLanguage", sargetLanguage);

            command.ExecuteNonQuery();
        }

        public static void AddScore(string originalWord)
        {
            string insertQuery = @"
                UPDATE savedword
                SET Score = Score+1
                WHERE Original = @Original; ";

            using SqliteCommand command = new(insertQuery, GetConnection());

            command.Parameters.AddWithValue("@Original", originalWord);

            command.ExecuteNonQuery();
        }

        public static void SubtractScore(string originalWord)
        {
            string insertQuery = @"
                UPDATE savedword
                SET Score = Score-1
                WHERE Original = @Original AND Score > 0; ";

            using SqliteCommand command = new(insertQuery, GetConnection());

            command.Parameters.AddWithValue("@Original", originalWord);

            command.ExecuteNonQuery();
        }

        public static void Remove(string Id)
        {
            string insertQuery = @"
                 DELETE FROM savedword WHERE Id = @Id or Original = @Id";

            using SqliteCommand command = new(insertQuery, GetConnection());

            command.Parameters.AddWithValue("@Id", Id);

            command.ExecuteNonQuery();
        }

        public static async void ToggleSaved(string original, string translated, int sourceLanguage, int targetLanguage)
        {
            bool isExist = IsExist(original).Result;

            if (isExist)
                Remove(original);
            else
            {
                Add(original, translated, sourceLanguage, targetLanguage);
            }
        }

        public static void Clear()
        {
            string selectQuery = "DELETE FROM savedword; DELETE FROM sqlite_sequence WHERE NAME='savedword'";
            using SqliteCommand command = new(selectQuery, GetConnection());
            command.ExecuteNonQuery();
        }

        public static async Task<bool> IsExist(string originalWord)
        {
            string selectQuery = @"
                 SELECT 1 FROM savedword WHERE Original = @Original LIMIT 1";

            using SqliteCommand command = new(selectQuery, GetConnection());
            command.Parameters.AddWithValue("@Original", originalWord);
            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync();
        }

        public static async Task<(List<SavedWordEntry>, int)> LoadAsync(
            int page, int maxRow, string searchText, int searchSourceLanguage, string orderBy)
        {
            List<SavedWordEntry> history = new();
            int totalCount = 0;
            using (SqliteCommand command = new(@"
                SELECT COUNT(*) 
                FROM savedword
                WHERE (Original LIKE @searchText OR Translated LIKE @searchText) AND (SourceLanguage = @searchSourceLanguage or @searchSourceLanguage='-1')", GetConnection()))
            {
                command.Parameters.AddWithValue("@searchText", $"%{searchText}%");
                command.Parameters.AddWithValue("@searchSourceLanguage", $"{searchSourceLanguage}");
                totalCount = Convert.ToInt32(await command.ExecuteScalarAsync());
            }

            int maxPage = Math.Max(1, (int)Math.Ceiling(totalCount / (double)maxRow));
            int offset = Math.Max(0, (page - 1) * maxRow);

            using (SqliteCommand command = new(@"
                SELECT Id, Original, Translated, SourceLanguage, TargetLanguage, Score
                FROM savedword
                WHERE (Original LIKE @searchText OR Translated LIKE @searchText) AND (SourceLanguage = @searchSourceLanguage or @searchSourceLanguage='-1')
                ORDER BY 
                    (CASE
                        WHEN @orderBy == 'Id' THEN Id
                        WHEN @orderBy == 'Score' THEN Score
                    END)
                DESC, Id DESC
                LIMIT @maxRow OFFSET @offset", GetConnection()))
            {
                command.Parameters.AddWithValue("@searchText", $"%{searchText}%");
                command.Parameters.AddWithValue("@searchSourceLanguage", $"{searchSourceLanguage}");
                command.Parameters.AddWithValue("@maxRow", maxRow);
                command.Parameters.AddWithValue("@offset", offset);
                command.Parameters.AddWithValue("@orderBy", orderBy);

                using SqliteDataReader reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    history.Add(new SavedWordEntry
                    {
                        Id = reader.GetString(reader.GetOrdinal("Id")),
                        Original = reader.GetString(reader.GetOrdinal("Original")),
                        Translated = reader.GetString(reader.GetOrdinal("Translated")),
                        SourceLanguage = reader.GetString(reader.GetOrdinal("SourceLanguage")),
                        TargetLanguage = reader.GetString(reader.GetOrdinal("TargetLanguage")),
                        ScoreVisibility = Int32.Parse(reader.GetString(reader.GetOrdinal("Score"))) > 0 ? Visibility.Visible : Visibility.Collapsed,
                    });
                }
            }
            return (history, maxPage);
        }

        public static async Task ExportToCSV(string filePath)
        {
            List<SavedWordEntry> history = new();

            string selectQuery = @"
                SELECT Id, Original, Translated, SourceLanguage, TargetLanguage
                FROM savedword";

            using (SqliteCommand command = new(selectQuery, GetConnection()))
            using (SqliteDataReader reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    int sourceLanguage = Int32.Parse(reader.GetString(reader.GetOrdinal("SourceLanguage")));
                    int targetLanguage = Int32.Parse(reader.GetString(reader.GetOrdinal("TargetLanguage")));

                    history.Add(new SavedWordEntry
                    {
                        Id = reader.GetString(reader.GetOrdinal("Id")),
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
