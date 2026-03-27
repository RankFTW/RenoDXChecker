namespace RenoDXCommander.Models;

public class REFrameworkInstalledRecord
{
    public string GameName { get; set; } = "";
    public string InstallPath { get; set; } = "";
    public string InstalledVersion { get; set; } = "";
    public DateTime InstalledAt { get; set; }
}
