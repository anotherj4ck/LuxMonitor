using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using LuxMonitor.Sensors;
using LuxMonitor.Services;
using LuxMonitor.ViewModels;

namespace LuxMonitor;

public partial class MainWindow : Window
{
    private readonly ISensorProvider _provider;
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _timer;

    public MainWindow()
    {
        InitializeComponent();

        // "--demo" sur la ligne de commande = fausses mesures (utile dans une VM).
        bool isDemo = Environment.GetCommandLineArgs()
            .Contains("--demo", StringComparer.OrdinalIgnoreCase);

        _provider = isDemo ? new DemoSensorProvider() : new WindowsSensorProvider();
        _viewModel = new MainViewModel(_provider, isDemo);
        DataContext = _viewModel;

        _viewModel.Refresh();

        // Rafraîchissement toutes les secondes, sur le thread de l'interface.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => _viewModel.Refresh();
        _timer.Start();

        // À la fermeture : arrêter les mesures et libérer l'accès au matériel.
        Closed += (_, _) =>
        {
            _timer.Stop();
            _provider.Dispose();
        };
    }

    // La fenêtre n'a pas de barre de titre : on la déplace en cliquant n'importe où.
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Cpu_Click(object sender, RoutedEventArgs e) => Launcher.OpenTaskManager();

    private void Gpu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GpuViewModel gpu })
            Launcher.OpenGpuTool(gpu.Vendor);
    }

    private void Ram_Click(object sender, RoutedEventArgs e) => Launcher.OpenTaskManager();

    private void Disk_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DiskViewModel disk })
            Launcher.OpenFolder(disk.RootPath);
    }
}
