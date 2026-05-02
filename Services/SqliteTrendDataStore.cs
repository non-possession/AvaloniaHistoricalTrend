using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AvaloniaApplication2.Models;
using Microsoft.Data.Sqlite;

namespace AvaloniaApplication2.Services;

// SQLite 历史数据存储实现。
// 本类只负责数据库文件、表结构、写入和查询，不处理趋势业务规则。
// 程序通过 Microsoft.Data.Sqlite 和 NuGet 自带的 e_sqlite3 native runtime 工作，
// 因此不依赖系统环境变量中的 sqlite3 命令行工具。
public sealed class SqliteTrendDataStore : ITrendDataStore
{
    private readonly string databasePath;
    private readonly string connectionString;

    public SqliteTrendDataStore(string databasePath)
    {
        this.databasePath = Path.GetFullPath(databasePath);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = this.databasePath,
        }.ToString();
    }

    public string DatabasePath => databasePath;

    public void Initialize(IReadOnlyList<TrendSeriesState> series)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath) ?? Directory.GetCurrentDirectory());

        using SqliteConnection connection = new SqliteConnection(connectionString);
        connection.Open();

        using SqliteCommand createCommand = connection.CreateCommand();
        createCommand.CommandText = """
CREATE TABLE IF NOT EXISTS trend_samples (
    timestamp_utc TEXT NOT NULL,
    series_index INTEGER NOT NULL,
    series_name TEXT NOT NULL,
    value REAL NOT NULL,
    PRIMARY KEY (timestamp_utc, series_index)
);
CREATE INDEX IF NOT EXISTS idx_trend_samples_time ON trend_samples(timestamp_utc);
""";
        // 表按“时间戳 + 变量索引”作为主键，保证同一时刻同一变量只有一个值。
        createCommand.ExecuteNonQuery();
    }

    public void AppendSample(TrendSample sample, IReadOnlyList<TrendSeriesState> series)
    {
        using SqliteConnection connection = new SqliteConnection(connectionString);
        connection.Open();

        using SqliteTransaction transaction = connection.BeginTransaction();
        for (int i = 0; i < sample.Values.Length && i < series.Count; i++)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
INSERT OR REPLACE INTO trend_samples(timestamp_utc, series_index, series_name, value)
VALUES ($timestamp, $seriesIndex, $seriesName, $value);
""";
            command.Parameters.AddWithValue("$timestamp", ToStorageTimestamp(sample.Timestamp));
            command.Parameters.AddWithValue("$seriesIndex", i);
            command.Parameters.AddWithValue("$seriesName", series[i].Name);
            command.Parameters.AddWithValue("$value", sample.Values[i]);
            command.ExecuteNonQuery();
        }

        // 一次采样包含多条变量值，使用事务避免只写入一部分变量。
        transaction.Commit();
    }

    public List<TrendSample> QuerySamples(DateTime start, DateTime end, int seriesCount)
    {
        Dictionary<string, TrendSample> samplesByTimestamp = new Dictionary<string, TrendSample>();

        using SqliteConnection connection = new SqliteConnection(connectionString);
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT timestamp_utc, series_index, value
FROM trend_samples
WHERE timestamp_utc >= $start AND timestamp_utc <= $end
ORDER BY timestamp_utc, series_index;
""";
        command.Parameters.AddWithValue("$start", ToStorageTimestamp(start));
        command.Parameters.AddWithValue("$end", ToStorageTimestamp(end));

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string timestampText = reader.GetString(0);
            int seriesIndex = reader.GetInt32(1);
            double value = reader.GetDouble(2);

            if (seriesIndex < 0 || seriesIndex >= seriesCount)
                continue;

            if (!samplesByTimestamp.TryGetValue(timestampText, out TrendSample? sample))
            {
                // 某些变量在指定时刻可能没有数据，用 NaN 保持列位置不变。
                sample = new TrendSample
                {
                    Timestamp = FromStorageTimestamp(timestampText),
                    Values = CreateEmptyValues(seriesCount),
                };
                samplesByTimestamp.Add(timestampText, sample);
            }

            sample.Values[seriesIndex] = value;
        }

        return new List<TrendSample>(samplesByTimestamp.Values);
    }

    private static double[] CreateEmptyValues(int seriesCount)
    {
        double[] values = new double[seriesCount];
        for (int i = 0; i < values.Length; i++)
            values[i] = double.NaN;

        return values;
    }

    private static string ToStorageTimestamp(DateTime value)
    {
        return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }

    private static DateTime FromStorageTimestamp(string value)
    {
        return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime();
    }
}
