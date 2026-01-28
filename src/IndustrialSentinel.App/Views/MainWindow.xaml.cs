using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using IndustrialSentinel.App.ViewModels;

namespace IndustrialSentinel.App.Views;

public partial class MainWindow : Window
{
    private readonly Stopwatch _fpsTimer = Stopwatch.StartNew();
    private int _frameCount;

    public MainWindow()
    {
        InitializeComponent();
        PreviewMouseDown += OnUserActivity;
        PreviewKeyDown += OnUserActivity;
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        _frameCount++;
        if (_fpsTimer.ElapsedMilliseconds >= 1000)
        {
            var fps = _frameCount / (_fpsTimer.ElapsedMilliseconds / 1000.0);
            _frameCount = 0;
            _fpsTimer.Restart();

            if (DataContext is MainViewModel vm)
            {
                vm.UpdateFps(fps);
            }
        }
    }

    private void OnUserActivity(object sender, InputEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.RegisterActivity();
        }
    }

    public void RequestClose()
    {
        Close();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WndProc);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_NCLBUTTONDBLCLK = 0x00A3;
        const int HTSYSMENU = 0x0003;

        if (msg == WM_NCLBUTTONDBLCLK && wParam == (IntPtr)HTSYSMENU)
        {
            handled = true;
        }

        return IntPtr.Zero;
    }
}
