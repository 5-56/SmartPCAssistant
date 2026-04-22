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

    public FloatBallWindow()
    {
        InitializeComponent();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
        _startPosition = e.GetPosition(this);
        _windowStartPosition = new Point(Position.X, Position.Y);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
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

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
    }
}
