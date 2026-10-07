namespace MxBattery;

public enum Via { Bolt, Bluetooth }
public enum Preferred { Auto, Bolt, Bluetooth }

/// <summary>Charging is null when the link can't report it (Bluetooth).</summary>
public record BatteryReading(int Percent, bool? Charging, Via Via);

public interface IBatterySource
{
    /// <summary>null = device not reachable (asleep, out of range, switched channel).</summary>
    Task<BatteryReading?> ReadAsync(CancellationToken ct);
}

/// <summary>Auto = Bolt first (it also reports charging); a forced preference asks only that source.</summary>
public sealed class CompositeSource(IBatterySource bolt, IBatterySource bluetooth, Func<Preferred> preferred) : IBatterySource
{
    public async Task<BatteryReading?> ReadAsync(CancellationToken ct)
    {
        IBatterySource[] order = preferred() switch
        {
            Preferred.Bolt => [bolt],
            Preferred.Bluetooth => [bluetooth],
            _ => [bolt, bluetooth],
        };
        foreach (var s in order)
            if (await s.ReadAsync(ct) is { } r) return r;
        return null;
    }
}
