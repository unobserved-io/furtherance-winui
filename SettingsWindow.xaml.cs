using System;
using Furtherance.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static PInvoke.User32;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Furtherance;
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        Title = "Settings";
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        SetWindowDetails(hwnd, 500, 600);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon("Assets/furtherance.ico");

        // Idle settings
        if (App.localSettings.Values["Idle"] != null)
        {
            idleToggle.IsOn = (bool)App.localSettings.Values["Idle"];
        } 

        if (App.localSettings.Values["IdleTime"] != null)
        {
            idleMinutesSpin.Value = (double)App.localSettings.Values["IdleTime"];
        }

        if (App.localSettings.Values["Pomodoro"] != null)
        {
            pomodoroToggle.IsOn = (bool)App.localSettings.Values["Pomodoro"];
        }

        if (App.localSettings.Values["PomodoroTime"] != null)
        {
            pomodoroMinutesSpin.Value = (double)App.localSettings.Values["PomodoroTime"];
        }
    }

    private void Idle_Toggled(object sender, RoutedEventArgs e)
    {
        var toggleSwitch = sender as ToggleSwitch;
        if (toggleSwitch != null && idleMinutesStack != null)
        {
            if (toggleSwitch.IsOn == true)
            {
                App.localSettings.Values["Idle"] = true;
                idleMinutesStack.Visibility = Visibility.Visible;
            }
            else
            {
                App.localSettings.Values["Idle"] = false;
                idleMinutesStack.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void IdleSpin_Changed(object sender, NumberBoxValueChangedEventArgs e)
    {
        var numberBox = sender as NumberBox;
        App.localSettings.Values["IdleTime"] = numberBox.Value;
    }

    private void Pomodoro_Toggled(object sender, RoutedEventArgs e)
    {
        var toggleSwitch = sender as ToggleSwitch;
        if (toggleSwitch != null && pomodoroMinutesStack != null)
        {
            if (toggleSwitch.IsOn == true)
            {
                App.localSettings.Values["Pomodoro"] = true;
                pomodoroMinutesStack.Visibility = Visibility.Visible;
            }
            else
            {
                App.localSettings.Values["Pomodoro"] = false;
                pomodoroMinutesStack.Visibility = Visibility.Collapsed;
            }
            MainPage.pomodoroEnabled = toggleSwitch.IsOn;
            MainPage.mainPage.ResetTimer();
        }
    }

    private void PomodoroSpin_Changed(object sender, NumberBoxValueChangedEventArgs e)
    {
        var numberBox = sender as NumberBox;
        App.localSettings.Values["PomodoroTime"] = numberBox.Value;
        MainPage.pomodoroTime = numberBox.Value;
        MainPage.mainPage.ResetTimer();
    }

    private static void SetWindowDetails(IntPtr hwnd, int width, int height)
    {
        var dpi = GetDpiForWindow(hwnd);
        var scalingFactor = (float)dpi / 96;
        width = (int)(width * scalingFactor);
        height = (int)(height * scalingFactor);

        _ = SetWindowPos(hwnd, SpecialWindowHandles.HWND_TOP,
                                    0, 0, width, height,
                                    SetWindowPosFlags.SWP_NOMOVE);
        _ = SetWindowLong(hwnd,
               WindowLongIndexFlags.GWL_STYLE,
               (SetWindowLongFlags)(GetWindowLong(hwnd,
                  WindowLongIndexFlags.GWL_STYLE) &
                  ~(int)SetWindowLongFlags.WS_MAXIMIZEBOX));
    }

}
