using System;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace StarterApp.API.Utilities;

public static class UtilityFunctions
{
    public static string GetControllerName<T>()
    {
        var typeName = typeof(T).Name;
        var splitOn = "Controller";

        if (typeName == null || !typeName.EndsWith(splitOn, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Controllers must end with 'Controller'. {typeName} does not.", nameof(T));
        }

        return typeName.Split(splitOn).First();
    }

    public static LogLevel LogLevelFromString(string logLevel)
    {
        ArgumentNullException.ThrowIfNull(logLevel);

        return logLevel.ToUpperInvariant() switch
        {
            "TRACE" => LogLevel.Trace,
            "DEBUG" => LogLevel.Debug,
            "INFORMATION" => LogLevel.Information,
            "WARNING" => LogLevel.Warning,
            "ERROR" => LogLevel.Error,
            "CRITICAL" => LogLevel.Critical,
            _ => throw new ArgumentException($"{nameof(logLevel)} must be Trace, Debug, Information, Warning, Error, or Critical.", nameof(logLevel)),
        };
    }
}