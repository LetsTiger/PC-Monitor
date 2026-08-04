using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PcMonitor.App.ViewModels;

namespace PcMonitor.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isClosing;
    private bool _isDarkTheme = true;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel
                     ?? throw new ArgumentNullException(
                         nameof(viewModel));

        DataContext = _viewModel;
        SetAdvancedMode(false);
        SetThemeSelection(true);

        Closing += OnWindowClosing;
    }

    private void OnEasyModeClick(object sender, RoutedEventArgs eventArgs) =>
        SetAdvancedMode(false);

    private void OnAdvancedModeClick(object sender, RoutedEventArgs eventArgs) =>
        SetAdvancedMode(true);

    private void SetAdvancedMode(bool enabled)
    {
        AdvancedAnalysisTab.Visibility = enabled
            ? Visibility.Visible
            : Visibility.Collapsed;
        PerformanceTab.Visibility = enabled
            ? Visibility.Visible
            : Visibility.Collapsed;
        SimpleResultTab.Visibility = enabled
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (enabled && SimpleResultTab.IsSelected)
        {
            MainNavigation.SelectedIndex = 0;
        }

        SystemCpuPanel.Visibility = enabled
            ? Visibility.Visible
            : Visibility.Collapsed;
        GpuPanel.Visibility = enabled
            ? Visibility.Visible
            : Visibility.Collapsed;
        StoragePanel.Visibility = enabled
            ? Visibility.Visible
            : Visibility.Collapsed;
        NetworkPanel.Visibility = enabled
            ? Visibility.Visible
            : Visibility.Collapsed;

        GridLength advancedSpacing = enabled
            ? new GridLength(20)
            : new GridLength(0);
        AdvancedSpacerOne.Height = advancedSpacing;
        AdvancedSpacerTwo.Height = advancedSpacing;
        AdvancedSpacerThree.Height = advancedSpacing;
        AdvancedSpacerFour.Height = advancedSpacing;

        if (!enabled &&
            (AdvancedAnalysisTab.IsSelected || PerformanceTab.IsSelected))
        {
            MainNavigation.SelectedIndex = 0;
        }

        Style primaryStyle = (Style)FindResource("PrimaryButtonStyle");
        Style defaultStyle = (Style)FindResource(typeof(Button));
        EasyModeButton.Style = enabled ? defaultStyle : primaryStyle;
        AdvancedModeButton.Style = enabled ? primaryStyle : defaultStyle;
    }

    private void OnDarkThemeClick(object sender, RoutedEventArgs eventArgs) =>
        SetThemeSelection(true);

    private void OnLightThemeClick(object sender, RoutedEventArgs eventArgs) =>
        SetThemeSelection(false);

    private void SetThemeSelection(bool useDarkTheme)
    {
        _isDarkTheme = useDarkTheme;
        string themeFile = _isDarkTheme
            ? "Themes/DarkTheme.xaml"
            : "Themes/LightTheme.xaml";

        ResourceDictionary resources = System.Windows.Application.Current.Resources;
        ResourceDictionary? currentTheme = resources.MergedDictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.Contains("Theme.xaml", StringComparison.OrdinalIgnoreCase) == true);
        int themeIndex = currentTheme is null
            ? 0
            : resources.MergedDictionaries.IndexOf(currentTheme);

        var newTheme = new ResourceDictionary
        {
            Source = new Uri(themeFile, UriKind.Relative)
        };

        if (currentTheme is null)
        {
            resources.MergedDictionaries.Insert(0, newTheme);
        }
        else
        {
            resources.MergedDictionaries[themeIndex] = newTheme;
        }

        Style primaryStyle = (Style)FindResource("PrimaryButtonStyle");
        Style defaultStyle = (Style)FindResource(typeof(Button));
        DarkThemeButton.Style = _isDarkTheme ? primaryStyle : defaultStyle;
        LightThemeButton.Style = _isDarkTheme ? defaultStyle : primaryStyle;
    }

    private async void OnWindowClosing(
        object? sender,
        CancelEventArgs eventArgs)
    {
        if (_isClosing)
        {
            return;
        }

        eventArgs.Cancel = true;
        _isClosing = true;

        Closing -= OnWindowClosing;

        try
        {
            await _viewModel.DisposeAsync();
        }
        catch (Exception exception)
        {
            Services.AppLog.Error("Fehler beim Beenden der Anwendung.", exception);
        }

        _ = Dispatcher.BeginInvoke(
            new Action(Close),
            DispatcherPriority.Background);
    }
}
