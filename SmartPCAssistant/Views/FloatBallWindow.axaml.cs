using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using System;

namespace SmartPCAssistant.Views;

public partial class FloatBallWindow : Window
{
    private bool _isDragging;
    private Point _startPosition;
    private Point _windowStartPosition;

    public event EventHandler? Clicked;

    public FloatBallWindow()
    {
        InitializeComponent();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _startPosition = e.GetPosition(this);
            _windowStartPosition = new Point(Position.X, Position.Y);
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            var delta = e.GetPosition(this) - _startPosition;

            if (Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5)
            {
                Clicked?.Invoke(this, EventArgs.Empty);
            }
        }
        _isDragging = false;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
        {
            var currentPosition = e.GetPosition(this);
            var delta = currentPosition - _startPosition;

            var newX = _windowStartPosition.X + delta.X;
            var newY = _windowStartPosition.Y + delta.Y;

            Position = new PixelPoint((int)newX, (int)newY);
        }
    }
}
