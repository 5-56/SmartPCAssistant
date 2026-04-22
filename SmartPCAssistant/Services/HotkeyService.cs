using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Serilog;

namespace SmartPCAssistant.Services;

public class HotkeyEventArgs : EventArgs
{
    public string Hotkey { get; set; } = string.Empty;
}

public interface IHotkeyService
{
    event EventHandler<HotkeyEventArgs>? HotkeyPressed;
    void Register(string hotkey);
    void Unregister(string hotkey);
    void UnregisterAll();
    bool IsRegistered(string hotkey);
}

public class HotkeyService : IHotkeyService
{
    private static HotkeyService? _instance;
    public static HotkeyService Instance => _instance ??= new HotkeyService();

    private readonly Dictionary<string, bool> _registeredHotkeys = new();
    private CancellationTokenSource? _monitorCts;
    private bool _isMonitoring;

    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    private HotkeyService() { }

    public void Register(string hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
            return;

        hotkey = hotkey.ToUpper();
        _registeredHotkeys[hotkey] = true;
        Log.Information("Hotkey registered: {Hotkey}", hotkey);

        if (!_isMonitoring)
        {
            StartMonitoring();
        }
    }

    public void Unregister(string hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
            return;

        hotkey = hotkey.ToUpper();
        _registeredHotkeys.Remove(hotkey);
        Log.Information("Hotkey unregistered: {Hotkey}", hotkey);

        if (_registeredHotkeys.Count == 0)
        {
            StopMonitoring();
        }
    }

    public void UnregisterAll()
    {
        _registeredHotkeys.Clear();
        StopMonitoring();
        Log.Information("All hotkeys unregistered");
    }

    public bool IsRegistered(string hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
            return false;
        return _registeredHotkeys.ContainsKey(hotkey.ToUpper());
    }

    private void StartMonitoring()
    {
        if (_isMonitoring)
            return;

        _isMonitoring = true;
        _monitorCts = new CancellationTokenSource();

        Task.Run(() => MonitorKeyboard(_monitorCts.Token));
        Log.Information("Hotkey monitoring started");
    }

    private void StopMonitoring()
    {
        if (!_isMonitoring)
            return;

        _isMonitoring = false;
        _monitorCts?.Cancel();
        _monitorCts?.Dispose();
        _monitorCts = null;
        Log.Information("Hotkey monitoring stopped");
    }

    private async Task MonitorKeyboard(CancellationToken cancellationToken)
    {
        Log.Information("Keyboard monitoring task started");

        while (!cancellationToken.IsCancellationRequested && _isMonitoring)
        {
            try
            {
                await Task.Delay(100, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        Log.Information("Keyboard monitoring task stopped");
    }

    public void SimulateHotkeyPress(string hotkey)
    {
        if (IsRegistered(hotkey))
        {
            Log.Information("Simulated hotkey press: {Hotkey}", hotkey);
            HotkeyPressed?.Invoke(this, new HotkeyEventArgs { Hotkey = hotkey });
        }
    }
}
