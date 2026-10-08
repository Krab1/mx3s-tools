using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Enumeration.Pnp;

namespace MxBattery;

/// <summary>
/// Reads the battery Windows already publishes on the paired BLE device node
/// (DEVPKEY_Bluetooth_Battery), gated on the AEP's live connection state because the node keeps a stale value.
/// ponytail: no direct GATT 0x180F fallback and no charging state; add if the property turns out to be missing.
/// </summary>
public sealed class BluetoothSource(Func<string> nameFilter) : IBatterySource
{
    const string BatteryKey = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    const string NameKey = "System.ItemNameDisplay";
    const string ConnectedKey = "System.Devices.Aep.IsConnected";

    string? _issue;
    public string? Issue => _issue;

    public async Task<BatteryReading?> ReadAsync(CancellationToken ct)
    {
        var filter = nameFilter();
        var paired = await DeviceInformation.FindAllAsync(
            BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), [ConnectedKey], DeviceInformationKind.AssociationEndpoint);
        var match = paired.Where(d => d.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        if (match.Count == 0) { _issue = $"Bluetooth: no paired device named \"{filter}\"."; return null; }
        if (!match.Any(d => d.Properties.TryGetValue(ConnectedKey, out var c) && c is true))
        { _issue = $"Bluetooth: \"{match[0].Name}\" is paired but not connected."; return null; }

        var all = await PnpObject.FindAllAsync(PnpObjectType.Device, [BatteryKey, NameKey]);
        foreach (var o in all)
        {
            if (!o.Id.StartsWith("BTH", StringComparison.OrdinalIgnoreCase)) continue;
            if (!o.Properties.TryGetValue(NameKey, out var n) || n?.ToString()?.Contains(filter, StringComparison.OrdinalIgnoreCase) != true) continue;
            if (o.Properties.TryGetValue(BatteryKey, out var b) && b is not null)
            {
                _issue = null;
                return new(Convert.ToInt32(b), null, Via.Bluetooth);
            }
        }
        _issue = "Bluetooth: connected, but Windows reports no battery level for it.";
        return null;
    }
}
