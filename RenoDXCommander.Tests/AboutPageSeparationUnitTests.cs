// Unit tests for About page separation feature
using Microsoft.UI.Xaml;
using Xunit;

namespace RenoDXCommander.Tests;

/// <summary>
/// Unit tests for About page enum and navigation visibility.
/// Requirements: 1.1, 5.1, 5.2, 5.3
/// </summary>
public class AboutPageSeparationUnitTests
{
    [Fact]
    public void AppPage_Enum_ContainsExactlyThreeValues()
    {
        var values = Enum.GetValues<AppPage>();
        Assert.Equal(3, values.Length);
        Assert.Contains(AppPage.GameView, values);
        Assert.Contains(AppPage.Settings, values);
        Assert.Contains(AppPage.About, values);
    }

    [Fact]
    public void ComputeVisibilityState_AboutPage_ReturnsAboutPanelVisible()
    {
        var state = MainWindow.ComputeVisibilityState(false, true, AppPage.About);

        Assert.Equal(Visibility.Visible, state.AboutPanel);
        Assert.Equal(Visibility.Collapsed, state.GameViewPanel);
        Assert.Equal(Visibility.Collapsed, state.SkeletonRowPanel);
        Assert.Equal(Visibility.Collapsed, state.SkeletonDetailPanel);
    }

    [Fact]
    public void ComputeVisibilityState_SettingsPage_ReturnsAboutPanelCollapsed()
    {
        var state = MainWindow.ComputeVisibilityState(false, true, AppPage.Settings);

        Assert.Equal(Visibility.Collapsed, state.AboutPanel);
    }

    [Fact]
    public void ComputeVisibilityState_GameViewPage_ReturnsAboutPanelCollapsed()
    {
        var state = MainWindow.ComputeVisibilityState(false, true, AppPage.GameView);

        Assert.Equal(Visibility.Collapsed, state.AboutPanel);
        Assert.Equal(Visibility.Visible, state.GameViewPanel);
    }
}
