using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using WorkingHours.Core.Models;
using WorkingHours.Services;

namespace WorkingHours.Views;

public partial class SettingsWindow : Window
{
    private readonly AppServices _services;
    private readonly AppSettings _draft;

    public SettingsWindow(AppServices services)
    {
        InitializeComponent();
        _services = services;
        _draft = services.Settings.Current.Clone();

        var loc = services.Loc;
        ModeCombo.ItemsSource = new[]
        {
            new OptionItem("auto", loc["Auto"]),
            new OptionItem("light", loc["Light"]),
            new OptionItem("dark", loc["Dark"])
        };

        LanguageCombo.ItemsSource = new[]
        {
            new OptionItem("auto", loc["SystemLanguage"]),
            new OptionItem("en", loc["English"]),
            new OptionItem("pt-BR", loc["Portuguese"])
        };

        var themes = services.Themes.AllThemes.Select(t => t.Name).ToList();
        LightThemeCombo.ItemsSource = themes;
        DarkThemeCombo.ItemsSource = themes;

        DataContext = _draft;

        // Re-assert selections after the items and DataContext are in place.
        ModeCombo.SelectedValue = _draft.ThemeMode;
        LanguageCombo.SelectedValue = _draft.Language;
        LightThemeCombo.SelectedValue = _draft.ThemeLight;
        DarkThemeCombo.SelectedValue = _draft.ThemeDark;

        VersionText.Text = loc.Format("Version", typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3) ?? "1.0.0");
        DataFolderText.Text = services.Paths.Root;

        Loaded += (_, _) =>
        {
            ContentScroll.ScrollToTop();
            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                textBox.PreviewTextInput += OnNumericPreview;
                DataObject.AddPastingHandler(textBox, OnNumericPaste);
            }
        };
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            WindowEffects.ApplyRoundedCorners(handle);
            WindowEffects.SetDarkTitleBar(handle, services.Themes.CurrentTheme?.IsLight != true);
        };
    }

    public void ScrollToEndForPreview() => ContentScroll.ScrollToEnd();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        CommitTextBindings(this);

        if (string.IsNullOrWhiteSpace(_draft.ThemeLight))
            _draft.ThemeLight = "Pomotroid Light";
        if (string.IsNullOrWhiteSpace(_draft.ThemeDark))
            _draft.ThemeDark = "Pomotroid";

        _draft.Sanitize();
        _services.Settings.Replace(_draft);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        _services.Paths.EnsureCreated();
        Process.Start(new ProcessStartInfo
        {
            FileName = _services.Paths.Root,
            UseShellExecute = true
        });
    }

    private void ResetStatistics_Click(object sender, RoutedEventArgs e)
    {
        var loc = _services.Loc;
        var result = MessageBox.Show(this, loc["ResetStatisticsConfirm"], loc["ResetStatistics"],
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result == MessageBoxResult.Yes)
            _services.Stats.Clear();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private static void OnNumericPreview(object sender, TextCompositionEventArgs e) =>
        e.Handled = e.Text.Any(c => !char.IsDigit(c));

    private static void OnNumericPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        var text = (string)e.DataObject.GetData(typeof(string));
        if (text.Any(c => !char.IsDigit(c)))
            e.CancelCommand();
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;

            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
    }

    private static void CommitTextBindings(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBox textBox)
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            CommitTextBindings(child);
        }
    }

    private sealed record OptionItem(string Value, string Label)
    {
        public override string ToString() => Label;
    }
}
