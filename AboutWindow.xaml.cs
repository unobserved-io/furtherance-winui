using System;
using Furtherance.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using static PInvoke.User32;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Furtherance;
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        Title = "About";
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        SetWindowDetails(hwnd, 300, 330);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon("Assets/furtherance.ico");
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

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        MainPage.mainPage.aboutWindow = null;
    }
}
