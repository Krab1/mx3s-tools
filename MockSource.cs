namespace MxBattery;

/// <summary>Preview-only source (MxBattery.exe --mock): a battery state you pick from the tray menu.</summary>
public sealed class MockSource : IBatterySource
{
    public static readonly (string Label, BatteryReading? Reading)[] States =
    [
        ("95% on battery (Bolt)", new(95, false, Via.Bolt)),
        ("72% charging (Bolt)", new(72, true, Via.Bolt)),
        ("100% charged (Bolt)", new(100, true, Via.Bolt)),
        ("75% (Bluetooth)", new(75, null, Via.Bluetooth)),
        ("55% yellow", new(55, false, Via.Bolt)),
        ("28% low", new(28, false, Via.Bolt)),
        ("8% critical", new(8, false, Via.Bolt)),
        ("not connected", null),
    ];

    public BatteryReading? Current = States[0].Reading;
    public Task<BatteryReading?> ReadAsync(CancellationToken ct) => Task.FromResult(Current);
}
