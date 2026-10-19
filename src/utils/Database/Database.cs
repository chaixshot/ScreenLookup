using Microsoft.Data.Sqlite;
using System.IO;

namespace ScreenLookup.src.utils.Database
{
    /// <summary>
    /// Provides central SQLite database connection management and lifecycle for all logger services.
    /// </summary>
    internal static class Database
    {
        public static readonly string ConnectionString = $"Data Source={Path.Combine(App.appDataFolder, "database.db")}";

        private static SqliteConnection? _sharedConnection;
        private static readonly Lock _connectionLock = new();
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        /// <summary>
        /// Gets an open, shared SQLite connection for database operations.
        /// Re-opens or recreates the connection if closed or disposed.
        /// </summary>
        public static SqliteConnection GetConnection()
        {
            lock (_connectionLock)
            {
                if (_sharedConnection == null)
                {
                    _sharedConnection = new SqliteConnection(ConnectionString);
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
                        _sharedConnection = new SqliteConnection(ConnectionString);
                        _sharedConnection.Open();
                    }
                }

                return _sharedConnection;
            }
        }

        public static async Task<T> ExecuteAsync<T>(Func<SqliteConnection, Task<T>> action)
        {
            await _semaphore.WaitAsync();
            try
            {
                var conn = GetConnection();
                return await action(conn);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public static async Task ExecuteAsync(Func<SqliteConnection, Task> action)
        {
            await _semaphore.WaitAsync();
            try
            {
                var conn = GetConnection();
                await action(conn);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public static T Execute<T>(Func<SqliteConnection, T> action)
        {
            _semaphore.Wait();
            try
            {
                var conn = GetConnection();
                return action(conn);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public static void Execute(Action<SqliteConnection> action)
        {
            _semaphore.Wait();
            try
            {
                var conn = GetConnection();
                action(conn);
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
