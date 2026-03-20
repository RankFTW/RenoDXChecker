namespace RenoDXCommander.Services;

public class CrashReporterService : ICrashReporter
{
    public void Log(string message) => CrashReporter.Log(message);

    public void WriteCrashReport(string source, Exception? ex, bool isTerminating = false, string? note = null)
        => CrashReporter.WriteCrashReport(source, ex, isTerminating, note);

    public bool VerboseLogging
    {
        get => CrashReporter.VerboseLogging;
        set => CrashReporter.VerboseLogging = value;
    }
}
