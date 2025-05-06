using ConfigCat.Client;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace ConfigCatLogging;

using MessageFormatKey = (string Format, string[] ArgNames);

public class ConfigCatToMSLoggerAdapter(ILogger<ConfigCatClient> logger) : IConfigCatLogger
{
    private readonly ILogger _logger = logger;

    private readonly ConcurrentDictionary<MessageFormatKey, string> _messageFormatCache = new(
        EqualityComparer<MessageFormatKey>.Create(
            (x, y) => ReferenceEquals(x.Format, y.Format),
            key => RuntimeHelpers.GetHashCode(key.Format)));

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

        var logValues = new LogValues(ref message, _messageFormatCache);

        _logger.Log(logLevel, eventId.Id, state: logValues, exception, static (state, _) => state.Message.ToString());

        message = logValues.Message;
    }

    private struct LogValues(ref FormattableLogMessage message, ConcurrentDictionary<MessageFormatKey, string> messageFormatCache) : IReadOnlyList<KeyValuePair<string, object?>>
    {
        public readonly int Count => Message.ArgNames.Length + 1;

        public FormattableLogMessage Message = message;

        private readonly string GetOriginalFormat() => Message.ArgNames is not { Length: > 0 }
            ? Message.Format
            : messageFormatCache.GetOrAdd((Message.Format, Message.ArgNames), key =>
            {
                var argNamePlaceholders = Array.ConvertAll(key.ArgNames, name => "{" + name + "}");
                return string.Format(CultureInfo.InvariantCulture, key.Format, argNamePlaceholders);
            });

        public readonly KeyValuePair<string, object?> this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException(nameof(index));
                }

                return index == Count - 1
                    ? new KeyValuePair<string, object?>("{OriginalFormat}", GetOriginalFormat())
                    : new KeyValuePair<string, object?>(Message.ArgNames[index], Message.ArgValues[index]);
            }
        }

        public readonly IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            for (int i = 0, n = Count; i < n; i++)
            {
                yield return this[i];
            }
        }

        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => Message.InvariantFormattedMessage;
    }
}