using IndustrialSentinel.Core.Alerts;
using IndustrialSentinel.Core.Pipeline;
using IndustrialSentinel.Core.Telemetry;
using IndustrialSentinel.Infrastructure.Security;
using Microsoft.Data.Sqlite;

namespace IndustrialSentinel.Infrastructure.Persistence;

public sealed class SqliteTelemetryPersistence : ITelemetryPersistence
{
    private readonly SqliteConnection _connection;
    private readonly SqliteCommand _telemetryCommand;
    private readonly SqliteCommand _alertCommand;

    public SqliteTelemetryPersistence(SqliteConnectionFactory factory)
    {
        _connection = factory.OpenConnection();
        SqliteSchema.EnsureCreated(_connection);

        _telemetryCommand = _connection.CreateCommand();
        _telemetryCommand.CommandText = @"
INSERT INTO telemetry_samples
(timestamp_utc, rpm, temperature_c, vibration_mms, rpm_smooth, temperature_smooth, vibration_smooth)
VALUES ($ts, $rpm, $temp, $vib, $rpm_s, $temp_s, $vib_s);
";
        _telemetryCommand.Parameters.Add("$ts", SqliteType.Text);
        _telemetryCommand.Parameters.Add("$rpm", SqliteType.Real);
        _telemetryCommand.Parameters.Add("$temp", SqliteType.Real);
        _telemetryCommand.Parameters.Add("$vib", SqliteType.Real);
        _telemetryCommand.Parameters.Add("$rpm_s", SqliteType.Real);
        _telemetryCommand.Parameters.Add("$temp_s", SqliteType.Real);
        _telemetryCommand.Parameters.Add("$vib_s", SqliteType.Real);

        _alertCommand = _connection.CreateCommand();
        _alertCommand.CommandText = @"
INSERT INTO alerts
(timestamp_utc, level, metric, value, threshold, message)
VALUES ($ts, $level, $metric, $value, $threshold, $message);
";
        _alertCommand.Parameters.Add("$ts", SqliteType.Text);
        _alertCommand.Parameters.Add("$level", SqliteType.Text);
        _alertCommand.Parameters.Add("$metric", SqliteType.Text);
        _alertCommand.Parameters.Add("$value", SqliteType.Real);
        _alertCommand.Parameters.Add("$threshold", SqliteType.Real);
        _alertCommand.Parameters.Add("$message", SqliteType.Text);
    }

    public void SaveTelemetry(TelemetryFrame frame)
    {
        _telemetryCommand.Parameters["$ts"].Value = frame.TimestampUtc.ToString("O");
        _telemetryCommand.Parameters["$rpm"].Value = frame.Rpm;
        _telemetryCommand.Parameters["$temp"].Value = frame.TemperatureC;
        _telemetryCommand.Parameters["$vib"].Value = frame.VibrationMmS;
        _telemetryCommand.Parameters["$rpm_s"].Value = frame.RpmSmoothed;
        _telemetryCommand.Parameters["$temp_s"].Value = frame.TemperatureSmoothed;
        _telemetryCommand.Parameters["$vib_s"].Value = frame.VibrationSmoothed;
        _telemetryCommand.ExecuteNonQuery();
    }

    public void SaveAlert(AlertEvent alert)
    {
        _alertCommand.Parameters["$ts"].Value = alert.TimestampUtc.ToString("O");
        _alertCommand.Parameters["$level"].Value = alert.Level.ToString();
        _alertCommand.Parameters["$metric"].Value = alert.Metric;
        _alertCommand.Parameters["$value"].Value = alert.Value;
        _alertCommand.Parameters["$threshold"].Value = alert.Threshold;
        _alertCommand.Parameters["$message"].Value = alert.Message;
        _alertCommand.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _telemetryCommand.Dispose();
        _alertCommand.Dispose();
        _connection.Dispose();
    }
}
