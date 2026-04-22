using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SmartPCAssistant.ViewModels;
using SmartPCAssistant.Views;
using Serilog;

namespace SmartPCAssistant;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private FloatBallWindow? _floatBallWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartPCAssistant",
            "logs",
            "app-.log");

        var logDir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("SmartPCAssistant starting...");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel();

            _mainWindow = new MainWindow
            {
                DataContext = viewModel
            };

            viewModel.SetMainWindow(_mainWindow);

            _floatBallWindow = new FloatBallWindow();
            _floatBallWindow.Opened += (s, e) =>
            {
                viewModel.SetFloatBallWindow(_floatBallWindow);
            };

            _floatBallWindow.Clicked += (s, e) =>
            {
                if (_mainWindow.WindowState == WindowState.Minimized || !_mainWindow.IsVisible)
                {
                    _mainWindow.WindowState = WindowState.Normal;
                    _mainWindow.Show();
                }
                _mainWindow.Activate();
                _mainWindow.Focus();
            };

            desktop.MainWindow = _mainWindow;
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;

            desktop.ShutdownRequested += (s, e) =>
            {
                _floatBallWindow?.Close();
                Log.Information("Application shutting down");
            };

            _floatBallWindow.Show();

            Log.Information("Main window and float ball initialized");
        }

        base.OnFrameworkInitializationCompleted();
    }
}
