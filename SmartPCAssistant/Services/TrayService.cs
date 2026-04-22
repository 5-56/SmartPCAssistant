using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Serilog;

namespace SmartPCAssistant.Services;

public interface ITrayService
{
    void Initialize(Window mainWindow);
    void ShowNotification(string title, string message);
    void Dispose();
}

public class TrayService : ITrayService
{
    private static TrayService? _instance;
    public static TrayService Instance => _instance ??= new TrayService();

    private Window? _mainWindow;
    private bool _isInitialized;

    private TrayService() { }

    public void Initialize(Window mainWindow)
    {
        if (_isInitialized)
            return;

        _mainWindow = mainWindow;
        _isInitialized = true;

        Log.Information("Tray service initialized");
    }

    public void ShowNotification(string title, string message)
    {
        Log.Information("Notification: {Title} - {Message}", title, message);
    }

    public void Dispose()
    {
        _isInitialized = false;
        _mainWindow = null;
        Log.Information("Tray service disposed");
    }
}
