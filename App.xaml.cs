using System;
using System.Runtime.InteropServices;
using Furtherance.Activation;
using Furtherance.Contracts.Services;
using Furtherance.Core.Contracts.Services;
using Furtherance.Core.Services;
using Furtherance.Models;
using Furtherance.Services;
using Furtherance.ViewModels;
using Furtherance.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Storage;
using static PInvoke.User32;

namespace Furtherance;

public partial class App : Application
{

    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            // Services
            services.AddSingleton<ILocalSettingsService, LocalSettingsServicePackaged>();
            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();

            // Views and ViewModels
            services.AddTransient<AddTaskViewModel>();
            services.AddTransient<AddTaskPage>();
            services.AddTransient<TaskEditViewModel>();
            services.AddTransient<TaskEditPage>();
            services.AddTransient<TaskDetailsViewModel>();
            services.AddTransient<TaskDetailsPage>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<MainPage>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        })
        .Build();

    public static T GetService<T>()
        where T : class
    {
        return _host.Services.GetService(typeof(T)) as T;
    }

    public static Window MainWindow { get; set; } = new Window() { Title = "Furtherance" };
    public static ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

    public App()
    {
        InitializeComponent();
        UnhandledException += App_UnhandledException;
        // Database.InitializeDatabase();
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Log and handle exceptions as appropriate.
        // For more details, see https://docs.microsoft.com/windows/winui/api/microsoft.ui.xaml.unhandledexceptioneventargs.
    }

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Get the activation args
        var appArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();

        // Get or register the main instance
        var mainInstance = Microsoft.Windows.AppLifecycle.AppInstance.FindOrRegisterForKey("main");
        if (!mainInstance.IsCurrent)
        {
            // Redirect activation to that instance
            await mainInstance.RedirectActivationToAsync(appArgs);

            // And exit our instance and stop
            System.Diagnostics.Process.GetCurrentProcess().Kill();
            return;
        }

        InitializeSettings();
        Database.InitializeDatabase();
        base.OnLaunched(args);
        var activationService = App.GetService<IActivationService>();
        await activationService.ActivateAsync(args);

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);
        SetWindowDetails(hwnd, 350, 600);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon("Assets/furtherance.ico");

        ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
    }

    private void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        WindowHelper.ShowWindow(MainWindow);
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

    private static class WindowHelper
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public static void ShowWindow(Window window)
        {
            // Bring the window to the foreground... first get the window handle...
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

            // Restore window if minimized... requires DLL import above
            ShowWindow(hwnd, 0x00000009);

            // And call SetForegroundWindow... requires DLL import above
            SetForegroundWindow(hwnd);
        }
    }

    private static void InitializeSettings()
    {
        if (localSettings.Values["Idle"] == null)
        {
            localSettings.Values["Idle"] = true;
        }
        if (localSettings.Values["IdleTime"] == null)
        {
            localSettings.Values["IdleTime"] = Convert.ToDouble(6);
        }
        if (localSettings.Values["Pomodoro"] == null)
        {
            localSettings.Values["Pomodoro"] = false;
        }
        if (localSettings.Values["PomodoroTime"] == null)
        {
            localSettings.Values["PomodoroTime"] = Convert.ToDouble(25);
        }
    }
}
