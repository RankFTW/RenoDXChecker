namespace RenoDXCommander.Services;

public interface ICrashReporter
{
    void Log(string message);
    void WriteCrashReport(string source, Exception? ex, bool isTerminating = false, string? note = null);
    bool VerboseLogging { get; set; }
}
