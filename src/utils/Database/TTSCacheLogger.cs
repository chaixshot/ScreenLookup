using Microsoft.Data.Sqlite;

namespace ScreenLookup.src.utils.Database
{
    internal static class TTSCacheLogger
    {
        static TTSCacheLogger()
        {
            InitializeDatabase();
        }

        private static void InitializeDatabase()
        {
            Database.Execute(conn =>
            {
                using SqliteCommand command = new(@"
                    CREATE TABLE IF NOT EXISTS tts_cache (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Text TEXT,
                        Language INTEGER,
                        ProviderServices INTEGER,
                        AudioStream BLOB
                    );
                    CREATE INDEX IF NOT EXISTS idx_tts_lookup
                    ON tts_cache (Text, Language, ProviderServices);
                ", conn);
                command.ExecuteNonQuery();
            });
        }

        public static async Task<byte[]?> GetTtsAudioAsync(string text, int langID, ProviderService providerID)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            return await Database.ExecuteAsync(async conn =>
            {
                string query = @"
                    SELECT AudioStream
                    FROM tts_cache
                    WHERE Text = @Text AND Language = @Language AND ProviderServices = @ProviderServices
                    LIMIT 1";

                using SqliteCommand command = new(query, conn);
                command.Parameters.AddWithValue("@Text", text.Trim());
                command.Parameters.AddWithValue("@Language", langID);
                command.Parameters.AddWithValue("@ProviderServices", (int)providerID);

                using SqliteDataReader reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    if (!reader.IsDBNull(0))
                    {
                        return (byte[])reader["AudioStream"];
                    }
                }

                return null;
            });
        }

        public static async Task SaveTtsAudioAsync(string text, int langID, ProviderService providerID, byte[] audioData)
        {
            if (string.IsNullOrWhiteSpace(text) || audioData == null || audioData.Length == 0) return;

            await Database.ExecuteAsync(async conn =>
            {
                string checkQuery = "SELECT Id FROM tts_cache WHERE Text = @Text AND Language = @Language AND ProviderServices = @ProviderServices LIMIT 1";
                object? id;
                using (SqliteCommand checkCmd = new(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@Text", text.Trim());
                    checkCmd.Parameters.AddWithValue("@Language", langID);
                    checkCmd.Parameters.AddWithValue("@ProviderServices", (int)providerID);
                    id = await checkCmd.ExecuteScalarAsync();
                }

                if (id != null)
                {
                    string updateQuery = "UPDATE tts_cache SET AudioStream = @AudioStream WHERE Id = @Id";
                    using SqliteCommand updateCmd = new(updateQuery, conn);
                    updateCmd.Parameters.AddWithValue("@AudioStream", audioData);
                    updateCmd.Parameters.AddWithValue("@Id", id);
                    await updateCmd.ExecuteNonQueryAsync();
                }
                else
                {
                    string insertQuery = "INSERT INTO tts_cache (Text, Language, ProviderServices, AudioStream) VALUES (@Text, @Language, @ProviderServices, @AudioStream)";
                    using SqliteCommand insertCmd = new(insertQuery, conn);
                    insertCmd.Parameters.AddWithValue("@Text", text.Trim());
                    insertCmd.Parameters.AddWithValue("@Language", langID);
                    insertCmd.Parameters.AddWithValue("@ProviderServices", (int)providerID);
                    insertCmd.Parameters.AddWithValue("@AudioStream", audioData);
                    await insertCmd.ExecuteNonQueryAsync();
                }
            });
        }

        public static void Clear()
        {
            Database.Execute(conn =>
            {
                string query = "DELETE FROM tts_cache; DELETE FROM sqlite_sequence WHERE NAME='tts_cache'";
                using SqliteCommand command = new(query, conn);
                command.ExecuteNonQuery();
            });
        }
    }
}
