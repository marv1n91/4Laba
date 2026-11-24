using System.Windows;
using SortingLab4.App.Helpers;

namespace SortingLab4.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void NavTab_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        ViewInternal.Visibility = NavInternal.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewExternal.Visibility = NavExternal.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ViewLexicographic.Visibility = NavLex.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

        UIElement active = NavInternal.IsChecked == true
            ? ViewInternal
            : NavExternal.IsChecked == true ? ViewExternal : ViewLexicographic;

        UiAnimations.FadeIn(active, durationMs: 140);
    }
}
