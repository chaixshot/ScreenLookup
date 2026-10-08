using CsvHelper;
using Microsoft.Data.Sqlite;
using ScreenLookup.src.models;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace ScreenLookup.src.utils.Database
{
    public static class SavedWordLogger
    {
        static SavedWordLogger()
        {
            InitializeDatabase();
        }

        private static void InitializeDatabase()
        {
            using SqliteCommand command = new(@"
                CREATE TABLE IF NOT EXISTS savedword (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Original TEXT,
                    Translated TEXT,
                    SourceLanguage INTEGER,
                    TargetLanguage INTEGER,
                    Score INTEGER
                );", Database.GetConnection());
            command.ExecuteNonQuery();
        }

        public static void Add(string originalWord, string translatedWord, int sourceLanguage, int targetLanguage)
        {
            string query = @"
                INSERT INTO savedword (Original, Translated, SourceLanguage, TargetLanguage)
                VALUES (@Original, @Translated, @SourceLanguage, @TargetLanguage)";

            using SqliteCommand command = new(query, Database.GetConnection());
            command.Parameters.AddWithValue("@Original", originalWord);
            command.Parameters.AddWithValue("@Translated", translatedWord);
            command.Parameters.AddWithValue("@SourceLanguage", sourceLanguage);
            command.Parameters.AddWithValue("@TargetLanguage", targetLanguage);

            command.ExecuteNonQuery();
        }

        public static void AddScore(string originalWord)
        {
            string query = @"
                UPDATE savedword
                SET Score = Score+1
                WHERE Original = @Original;";

            using SqliteCommand command = new(query, Database.GetConnection());
            command.Parameters.AddWithValue("@Original", originalWord);
            command.ExecuteNonQuery();
        }

        public static void SubtractScore(string originalWord)
        {
            string query = @"
                UPDATE savedword
                SET Score = Score-1
                WHERE Original = @Original AND Score > 0;";

            using SqliteCommand command = new(query, Database.GetConnection());
            command.Parameters.AddWithValue("@Original", originalWord);
            command.ExecuteNonQuery();
        }

        public static void Remove(string id)
        {
            string query = "DELETE FROM savedword WHERE Id = @Id or Original = @Id";

            using SqliteCommand command = new(query, Database.GetConnection());
            command.Parameters.AddWithValue("@Id", id);
            command.ExecuteNonQuery();
        }

        public static async void ToggleSaved(string original, string translated, int sourceLanguage, int targetLanguage)
        {
            bool isExist = await IsExist(original);

            if (isExist)
                Remove(original);
            else
                Add(original, translated, sourceLanguage, targetLanguage);
        }

        public static void Clear()
        {
            string query = "DELETE FROM savedword; DELETE FROM sqlite_sequence WHERE NAME='savedword'";
            using SqliteCommand command = new(query, Database.GetConnection());
            command.ExecuteNonQuery();
        }

        public static async Task<bool> IsExist(string originalWord)
        {
            string query = "SELECT 1 FROM savedword WHERE Original = @Original LIMIT 1";

            using SqliteCommand command = new(query, Database.GetConnection());
            command.Parameters.AddWithValue("@Original", originalWord);
            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync();
        }

        public static async Task<(List<SavedWordEntry> Entries, int MaxPage)> LoadAsync(
            int page, int maxRow, string searchText, int searchSourceLanguage, string orderBy)
        {
            List<SavedWordEntry> history = [];
            int totalCount = 0;

            using (SqliteCommand command = new(@"
                SELECT COUNT(*) 
                FROM savedword
                WHERE (Original LIKE @searchText OR Translated LIKE @searchText) AND (SourceLanguage = @searchSourceLanguage or @searchSourceLanguage='-1')", Database.GetConnection()))
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
                LIMIT @maxRow OFFSET @offset", Database.GetConnection()))
            {
                command.Parameters.AddWithValue("@searchText", $"%{searchText}%");
                command.Parameters.AddWithValue("@searchSourceLanguage", $"{searchSourceLanguage}");
                command.Parameters.AddWithValue("@maxRow", maxRow);
                command.Parameters.AddWithValue("@offset", offset);
                command.Parameters.AddWithValue("@orderBy", orderBy);

                FontFamily fontFace = new(App.setting.FontFace);
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
                        ScoreVisibility = int.Parse(reader.GetString(reader.GetOrdinal("Score"))) > 0 ? Visibility.Visible : Visibility.Collapsed,
                        FontFace = fontFace
                    });
                }
            }
            return (history, maxPage);
        }

        public static async Task ExportToCSV(string filePath)
        {
            List<SavedWordEntry> history = [];

            string query = @"
                SELECT Id, Original, Translated, SourceLanguage, TargetLanguage
                FROM savedword";

            using (SqliteCommand command = new(query, Database.GetConnection()))
            using (SqliteDataReader reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    int sourceLanguage = int.Parse(reader.GetString(reader.GetOrdinal("SourceLanguage")));
                    int targetLanguage = int.Parse(reader.GetString(reader.GetOrdinal("TargetLanguage")));

                    history.Add(new SavedWordEntry
                    {
                        Id = reader.GetString(reader.GetOrdinal("Id")),
                        Original = reader.GetString(reader.GetOrdinal("Original")),
                        Translated = reader.GetString(reader.GetOrdinal("Translated")),
                        SourceLanguage = LanguageList.GetDisplayNameFromID(sourceLanguage, false),
                        TargetLanguage = LanguageList.GetDisplayNameFromID(targetLanguage, false),
                        FontFace = new FontFamily(App.setting.FontFace),
                    });
                }
            }

            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(true));
            using var csvWriter = new CsvWriter(writer, CultureInfo.InvariantCulture);
            await csvWriter.WriteRecordsAsync(history);
        }
    }
}
