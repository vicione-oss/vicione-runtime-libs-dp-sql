using System.Collections.Generic;
using System.Threading;
using Microsoft.Data.SqlClient;

namespace ViciOne.Suite.DataPort;

/// <summary>
/// Tracks how many active <see cref="SqlConnection"/> instances share the same connection string.
/// When the last user of a given connection string is removed, the corresponding connection pool
/// is cleared via <see cref="SqlConnection.ClearPool"/>. This ensures all pooled connections are
/// physically released before the AssemblyLoadContext is unloaded, preventing the native SNI
/// teardown crash (0xC0000005 in SNIPacketRelease).
/// </summary>
internal static class SqlConnectionPoolTracker // This class is needed because Unloading in Engine Host is too soon and a lib is unloaded before the SqlConnection is properly cleaned up.
{
    private static readonly Lock s_lock = new();
    private static readonly Dictionary<string, int> s_entries = [];

    internal static void Register(string connectionString)
    {
        lock (s_lock)
        {
            s_entries[connectionString] = s_entries.TryGetValue(connectionString, out var count) ? count + 1 : 1;
        }
    }

    /// <summary>
    /// Decrements the usage count for <paramref name="connectionString"/>. When the count reaches
    /// zero, calls <see cref="SqlConnection.ClearPool"/> on <paramref name="connection"/> so every
    /// pooled connection sharing that string is destroyed immediately rather than being left for
    /// the AssemblyLoadContext unload hook to clean up.
    /// </summary>
    internal static void Unregister(string connectionString, SqlConnection connection)
    {
        lock (s_lock)
        {
            if (!s_entries.TryGetValue(connectionString, out var count))
                return;

            if (count <= 1)
            {
                s_entries.Remove(connectionString);
                SqlConnection.ClearPool(connection);
            }
            else
            {
                s_entries[connectionString] = count - 1;
            }
        }
    }
}
