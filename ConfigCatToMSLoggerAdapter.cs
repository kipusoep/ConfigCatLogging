using System.Collections;
using ConfigCat.Client;

namespace ConfigCatLogging;

public class ConfigCatToMSLoggerAdapter(ILogger<ConfigCatClient> logger) : IConfigCatLogger
{
    private readonly ILogger _logger = logger;

    public ConfigCat.Client.LogLevel LogLevel
    {
        get => ConfigCat.Client.LogLevel.Debug;
        set { }
    }

    public void Log(ConfigCat.Client.LogLevel level, LogEventId eventId, ref FormattableLogMessage message, Exception? exception = null)
    {
        var logLevel = level switch
        {
            ConfigCat.Client.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            ConfigCat.Client.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            ConfigCat.Client.LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
            ConfigCat.Client.LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            _ => Microsoft.Extensions.Logging.LogLevel.None,
        };

        var logValues = new LogValues(ref message);

        _logger.Log(logLevel, eventId.Id, state: logValues, exception, static (state, _) => state.Message.ToString()); // Note: the formatter lambda is not being called when Serilog is the log provider.

        message = logValues.Message;
    }

    private sealed class LogValues(ref FormattableLogMessage message) : IReadOnlyList<KeyValuePair<string, object?>>
    {
        public int Count => Message.ArgNames.Length + 1;

        public FormattableLogMessage Message { get; } = message;

        public KeyValuePair<string, object?> this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException(nameof(index));
                }

                return index == Count - 1
                    ? new KeyValuePair<string, object?>("{OriginalFormat}", Message.Format)
                    : new KeyValuePair<string, object?>(Message.ArgNames[index], Message.ArgValues[index]);
            }
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            for (int i = 0, n = Count; i < n; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => Message.InvariantFormattedMessage;
    }
}