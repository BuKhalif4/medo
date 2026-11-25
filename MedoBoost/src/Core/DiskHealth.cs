using System.Text.Json;

namespace MedoBoost.Core;

public class DiskInfo
{
    public string Name = "", Media = "", Health = "", Status = "";
    public long Size;
    public double? Temp, Wear, Hours, Errors;
    public bool Bad => (!Health.Equals("Healthy", StringComparison.OrdinalIgnoreCase) && Health.Length > 0) || (Wear ?? 0) >= 90;
    public bool Warn => !Bad && ((Wear ?? 0) >= 70 || (Temp ?? 0) >= 65 || (Errors ?? 0) > 0);
}

/// <summary>Reads drive health from Windows' storage reliability counters (SMART-like: wear, temperature, power-on hours, errors).</summary>
public static class DiskHealth
{
    public static List<DiskInfo> Read()
    {
        var list = new List<DiskInfo>();
        var (_, o) = Util.PowerShell(@"
$ProgressPreference='SilentlyContinue'; $ErrorActionPreference='SilentlyContinue'
$r = Get-PhysicalDisk | ForEach-Object {
  $c = $_ | Get-StorageReliabilityCounter
  [pscustomobject]@{ Name=$_.FriendlyName; Media=[string]$_.MediaType; Bus=[string]$_.BusType; Size=[int64]$_.Size; Health=[string]$_.HealthStatus; Status=[string]$_.OperationalStatus
    Temp=$c.Temperature; Wear=$c.Wear; Hours=$c.PowerOnHours; Errors=($c.ReadErrorsTotal + $c.WriteErrorsTotal) } }
ConvertTo-Json -InputObject @($r) -Compress", 90000);
        int i = o.IndexOf('[');
        if (i < 0) return list;
        try
        {
            using var doc = JsonDocument.Parse(o[i..]);
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                string Str(string n) => e.TryGetProperty(n, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : "";
                double? Num(string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
                var media = Str("Media"); var bus = Str("Bus");
                if (bus.Equals("NVMe", StringComparison.OrdinalIgnoreCase)) media = "NVMe SSD";
                list.Add(new DiskInfo
                {
                    Name = Str("Name"), Media = media, Health = Str("Health"), Status = Str("Status"),
                    Size = (long)(Num("Size") ?? 0), Temp = Num("Temp"), Wear = Num("Wear"), Hours = Num("Hours"), Errors = Num("Errors"),
                });
            }
        }
        catch (Exception ex) { Log.Warn("DiskHealth: " + ex.Message); }
        return list;
    }
}
