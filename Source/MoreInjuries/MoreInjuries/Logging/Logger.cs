using MoreInjuries.Roslyn.CompilerServices;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MoreInjuries.Logging;

internal static partial class Logger
{
    [CompileTimePlatformInfo]
    private static partial CompileTimePlatformInfo CompileTimePlatformInfo { get; }

    internal static bool IsEnabled => MoreInjuriesMod.Settings is { EnableLogging: true };

    public static void Log(string message)
    {
        if (IsEnabled)
        {
            Verse.Log.Message($"[{nameof(MoreInjuries)}] {message}");
        }
    }

    public static void LogVerbose(string message)
    {
        if (IsEnabled && MoreInjuriesMod.Settings.EnableVerboseLogging)
        {
            Log($"VERBOSE: {message}");
        }
    }

    [Conditional("DEBUG")]
    public static void LogDebug(string message, [CallerFilePath] string callsite = "", [CallerLineNumber] int lineNo = -1) =>
        Log($"DEBUG: {FormatCallSite(callsite, lineNo)}{message}");

    public static void Error(string message, [CallerFilePath] string callsite = "", [CallerLineNumber] int lineNo = -1)
    {
        if (IsEnabled)
        {
            Verse.Log.Error($"[{nameof(MoreInjuries)}] ERROR: {FormatCallSite(callsite, lineNo)}{message}");
        }
    }

    public static void Warning(string message, [CallerFilePath] string callsite = "", [CallerLineNumber] int lineNo = -1)
    {
        if (IsEnabled)
        {
            Verse.Log.Warning($"[{nameof(MoreInjuries)}] WARNING: {FormatCallSite(callsite, lineNo)}{message}");
        }
    }

    public static void LogAlways(string message)
    {
        if (IsEnabled)
        {
            Verse.Log.Message($"[{nameof(MoreInjuries)}] CRITICAL: {message}");
        }
    }

#if DEBUG
    [DoesNotReturn]
#endif
    public static void ConfigError(string message, [CallerFilePath] string callsite = "", [CallerLineNumber] int lineNo = -1)
    {
        string formattedMessage = $"[{nameof(MoreInjuries)}] {FormatCallSite(callsite, lineNo)}Config Error: {message}";
        if (IsEnabled)
        {
            Verse.Log.Error(formattedMessage);
        }
#if DEBUG
        throw new InvalidOperationException(formattedMessage);
#endif
    }

    private static string FormatCallSite(string callsite, int lineNo)
    {
        if (string.IsNullOrEmpty(callsite))
        {
            return string.Empty;
        }
        int fileNameIndex = callsite.LastIndexOf(CompileTimePlatformInfo.DirectorySeparatorChar);
        if (fileNameIndex == -1 || ++fileNameIndex >= callsite.Length)
        {
            return $"{callsite}::L{lineNo}: ";
        }
        return $"{callsite.Substring(fileNameIndex)}::L{lineNo}: ";
    }
}
