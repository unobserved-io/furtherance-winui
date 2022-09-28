using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Furtherance.Contracts.Services;
using Furtherance.ViewModels;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.UI.Popups;
using WinRT.Interop;

namespace Furtherance.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel
    {
        get;
    }

    private DateTime startTime;
    private readonly System.Timers.Timer timer = new(100);
    private readonly INavigationService _navigationService;
    private DispatcherTimer dispatcherTimer;
    private bool idleNotified = false;
    private bool idleTimeReached = false;
    private DateTime idleStartTime;
    public static FurTask clickedTask;
    private FurTask rightClickedTask;
    private double lastSavedSeconds;
    public static bool pomodoroEnabled = (bool)App.localSettings.Values["Pomodoro"];
    public static double pomodoroTime = (double)App.localSettings.Values["PomodoroTime"];
    private bool pomodoroContinue = false;
    public static MainPage mainPage = null;
    private List<string> suggestions;
    public Window settingsWindow;
    public Window aboutWindow;
    public Window reportWindow;

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();
        _navigationService = App.GetService<INavigationService>();

        mainPage = this;

        RefreshTaskList();
        ResetTimer();
        timer.AutoReset = true;
        timer.Elapsed += Timer_Tick;

        var autosaveTask = CheckForAutosaveAsync();
    }

    private void TaskInput_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(taskInput.Text))
        {
            startButton.IsEnabled = false;
        }
        else if (taskInput.Text.TrimStart().First() == '#')
        {
            startButton.IsEnabled = false;
        }
        else
        {
            startButton.IsEnabled = true;
        }

        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            var suitableItems = new List<string>();
            var splitText = sender.Text.ToLower().Split(" ");
            foreach (var suggestion in suggestions)
            {
                var found = splitText.All((key) =>
                {
                    return suggestion.ToLower().Contains(key);
                });
                if (found)
                {
                    suitableItems.Add(suggestion);
                }
            }
            sender.ItemsSource = suitableItems;
        }
    }

    private void TaskInput_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        taskInput.Text = args.SelectedItem.ToString();
    }

    private void TaskInput_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion == null)
        {
            StartButton_Click(this, new RoutedEventArgs());
        }
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (!timer.Enabled)
        {
            startTime = DateTime.Now;
            timer.Start();
            startButton.Content = "\xE71A";

            BeforeAfterRunning();
        } 
        else
        {
            var stopTime = DateTime.Now;
            timer.Stop();
            SaveTask(stopTime);
        }
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => 
        {
            var timeSinceStart = TimeSpan.Zero;
            if (!pomodoroEnabled || pomodoroContinue)
            {
                timeSinceStart = DateTime.Now.Subtract(startTime);
            }
            else
            {
                timeSinceStart = DateTime.Now.Subtract(startTime.AddMinutes(pomodoroTime));
                if (Math.Floor(timeSinceStart.TotalSeconds) == 0)
                {
                    timer.Stop();
                    PomodorOver(DateTime.Now);
                }
            }
            timerLabel.Text = timeSinceStart.ToString(@"hh\:mm\:ss");

            if ((bool)App.localSettings.Values["Idle"])
            {
                CheckUserIdle();
            }
            var totalSeconds = Math.Floor(timeSinceStart.TotalSeconds);
            if (totalSeconds % 60 == 0 && totalSeconds != lastSavedSeconds)
            {
                lastSavedSeconds = totalSeconds;
                var (task, tags) = SplitTags(taskInput.Text);
                Autosave.WriteAutosave(task, startTime, DateTime.Now, tags);
            }
        });
    }

    private void BeforeAfterRunning()
    {
        taskInput.IsEnabled = !taskInput.IsEnabled;
        newTask.IsEnabled = !newTask.IsEnabled;
        repeatButton.IsEnabled = !repeatButton.IsEnabled;
        settings.IsEnabled = !settings.IsEnabled;
        pomodoroContinue = false;
    }

    private void CheckUserIdle()
    {
        var selectedIdle = Convert.ToInt32((double)App.localSettings.Values["IdleTime"]) * 60;
        var idleTime = IdleTimeFinder.GetIdleTime() / 1000;
        if (idleTime < selectedIdle && idleTimeReached && !idleNotified)
        {
            // User is back, show idle message
            idleNotified = true;
            ResumeFromIdleAsync();
        }
        else if (idleTime >= selectedIdle && !idleTimeReached)
        {
            idleTimeReached = true;
            idleStartTime = DateTime.Now - TimeSpan.FromSeconds(selectedIdle);
        }
    }

    private async void ResumeFromIdleAsync()
    {
        var resumeTime = DateTime.Now;
        var idleTime = resumeTime.Subtract(idleStartTime).ToString(@"h\:mm\:ss");

        var dialog = new ContentDialog()
        {
            XamlRoot = XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Title = $"You have been idle for {idleTime}",
            PrimaryButtonText = "Discard",
            SecondaryButtonText = "Continue",
            DefaultButton = ContentDialogButton.Primary,
            Content = "Would you like to discard that time, or continue the clock?"
        };

        SystemNotification($"You have been idle for {idleTime}", "Open Furtherance to discard or continue.");

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            timer.Stop();
            SaveTask(idleStartTime);
            // Need to reset the timerLabel again due to this being an async function
            ResetTimer();
        }
        else
        {
            ResetIdle();
        }
    }

    private async void PomodorOver(DateTime stopTime)
    {
        var dialog = new ContentDialog()
        {
            XamlRoot = XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Title = "Time's up!",
            PrimaryButtonText = "Stop",
            SecondaryButtonText = "Continue",
            DefaultButton = ContentDialogButton.Primary,
            Content = "Would you like to stop or keep the timer going?"
        };

        SystemNotification("Time's up!", "Your Furtherance timer has finished.");

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            SaveTask(stopTime);
            // Need to reset the timerLabel again due to this being an async function
            ResetTimer();
        }
        else
        {
            pomodoroContinue = true;
            timer.Start();
        }
    }

    private void ResetIdle()
    {
        idleNotified = false;
        idleTimeReached = false;
    }

    private void TaskClicked(object sender, ItemClickEventArgs e)
    {
        if (!timer.Enabled)
        {
            clickedTask = e.ClickedItem as FurTask;
            // Go straight to TaskEdit if there aren't any others with the same name
            var counter = 0;
            foreach (var task in Database.GetData())
            {
                if (clickedTask.Name == task.Name
                    && clickedTask.Tags == task.Tags
                    && clickedTask.StartTime.ToString("d", DateTimeFormatInfo.InvariantInfo)
                        == task.StartTime.ToString("d", DateTimeFormatInfo.InvariantInfo))
                {
                    counter++;
                    if (counter > 1)
                    {
                        break;
                    }
                }
            }
            if (counter == 1)
            {
                TaskDetailsPage.clickedToEdit = clickedTask.ID;
                _navigationService.NavigateTo(typeof(TaskEditViewModel).FullName);
            }
            else
            {
                _navigationService.NavigateTo(typeof(TaskDetailsViewModel).FullName);
            }
        }
    }

    private void TaskList_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var rightClickedElement = ((FrameworkElement)e.OriginalSource).DataContext;
        if (rightClickedElement != null)
        {
            var isTask = rightClickedElement.GetType() == typeof(Furtherance.Views.FurTask);
            if (isTask)
            {
                rightClickedTask = (FurTask)rightClickedElement;
                var flyoutOption = new FlyoutShowOptions
                {
                    ShowMode = FlyoutShowMode.TransientWithDismissOnPointerMoveAway
                };
                taskListFlyout.ShowAt((FrameworkElement)e.OriginalSource, flyoutOption);
            }
        }
    }

    private void RepeatTask_Clicked(object sender, RoutedEventArgs e)
    {
        if (!timer.Enabled)
        {
            taskInput.Text = rightClickedTask.Name + " " + rightClickedTask.Tags;
            StartButton_Click(startButton, new RoutedEventArgs());
            taskListFlyout.Hide();
        }    
    }

    private async void DeleteTask_Clicked(object sender, RoutedEventArgs e)
    {
        taskListFlyout.Hide();
        // Check if there are more than one tasks in the group
        // If so set dialog accordingly
        var titleMsg = "Delete this task?";
        var contentMsg = "";

        var idList = new List<string>();
        foreach (var task in Database.GetData())
        {
            if (rightClickedTask.Name == task.Name
                && rightClickedTask.Tags == task.Tags
                && rightClickedTask.StartTime.ToString("d", DateTimeFormatInfo.InvariantInfo)
                    == task.StartTime.ToString("d", DateTimeFormatInfo.InvariantInfo))
            {
                idList.Add(task.ID);
            }
        }
        if (idList.Count > 1)
        {
            titleMsg = "There are multiple tasks in this group.";
            contentMsg = "Delete them all?";
        }

        var dialog = new ContentDialog()
        {
            XamlRoot = XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
            Title = titleMsg,
            PrimaryButtonText = "Cancel",
            SecondaryButtonText = "Delete",
            DefaultButton = ContentDialogButton.Primary,
            Content = contentMsg
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Secondary)
        {
            if (idList.Count > 1)
            {
                Database.DeleteByIDs(idList);
            }
            else
            {
                Database.DeleteByID(rightClickedTask.ID);
            }
            RefreshTaskList();
        }
    }

    private void NewTask_Clicked(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo(typeof(AddTaskViewModel).FullName);
    }

    // TODO Delete if not needed
    public void CreateDispatcherTimer()
    {
        // Create a timer to close Toast messages after 5 seconds
        dispatcherTimer = new DispatcherTimer();
        dispatcherTimer.Tick += DispatcherTimer_Tick; ;
        dispatcherTimer.Interval = new TimeSpan(0, 0, 5);
        dispatcherTimer.Start();

        /*if (dispatcherTimer == null)
        {
            toast.IsOpen = true;
            CreateDispatcherTimer();
        }*/
    }

    private void DispatcherTimer_Tick(object sender, object e)
    {
        toast.IsOpen = false;
        dispatcherTimer.Stop();
        dispatcherTimer = null;
    }

    private void Exit_Clicked(object sender, RoutedEventArgs e)
    {
        App.MainWindow.Close();
    }

    private void About_Clicked(object sender, RoutedEventArgs e)
    {
        if (aboutWindow == null)
        {
            aboutWindow = new AboutWindow();
        }
        try
        {
            aboutWindow.Activate();
        }
        catch (Exception)
        {
            aboutWindow = new AboutWindow();
            aboutWindow.Activate();
        }
    }

    private void Settings_Clicked(object sender, RoutedEventArgs e)
    {
        if (settingsWindow == null)
        {
            settingsWindow = new SettingsWindow();
        }
        try
        {
            settingsWindow.Activate();
        }
        catch (Exception)
        {
            settingsWindow = new SettingsWindow();
            settingsWindow.Activate();
        }
    }

    private void GenerateReport_Clicked(object sender, RoutedEventArgs e)
    {
        if (reportWindow == null)
        {
            reportWindow = new ReportWindow();
        }
        try
        {
            reportWindow.Activate();
        }
        catch (Exception)
        {
            reportWindow = new ReportWindow();
            reportWindow.Activate();
        }
    }

    private void SaveTask(DateTime stopTime)
    {
        var (task, tags) = SplitTags(taskInput.Text);
        Database.AddData(task, startTime, stopTime, tags);
        Autosave.DeleteAutosave();
        RefreshTaskList();
        taskInput.Text = "";
        startButton.Content = "\xE768";
        ResetTimer();
        BeforeAfterRunning();
        ResetIdle();
    }

    private static (string, string) SplitTags(string taskAndTags)
    {
        // Split and remove whitespace
        var words = taskAndTags.Trim().Split('#').Select(word => word.Trim()).ToArray();
        // Retrieve task name
        var task = words.First();
        
        var tagList = new List<string>(words[1..]);
        // Remove empty tags
        tagList.RemoveAll(tag => string.IsNullOrWhiteSpace(tag));
        // Remove duplicate tags
        tagList = tagList.Distinct().ToList();
        // Lowercase tags
        tagList = tagList.ConvertAll(tag => tag.ToLower());
        var tags = string.Join(" #", tagList);
        return (task, tags);
    }

    private async Task CheckForAutosaveAsync()
    {
        if (Autosave.AutosaveExists())
        {
            var autosaveTask = Autosave.ReadAutosave();
            Database.AddData(autosaveTask[0], FromRfc3339String(autosaveTask[1]), FromRfc3339String(autosaveTask[2]), autosaveTask[3]);

            Autosave.DeleteAutosave();
            RefreshTaskList();

            var showDialog = new MessageDialog("Furtherance shut down improperly. An autosave was restored.");
            InitializeWithWindow.Initialize(showDialog, WindowNative.GetWindowHandle(App.MainWindow));
            await showDialog.ShowAsync();
        }
    }

    public void RefreshTaskList()
    {
        TaskListData.Source = GetTasksGrouped();
        suggestions = Database.GetTaskSuggestions();
    }

    public static ObservableCollection<FurTaskGroupList> GetTasksGrouped()
    {
        IEnumerable<FurTaskGroupList> query;
        query = from item in Database.GetData()
        group item by item.StartTime.ToString("m", DateTimeFormatInfo.InvariantInfo) into g
        orderby g.Key
        // Filter tasks that are unique by both name and tags
        select new FurTaskGroupList(g
            .GroupBy(p => new { p.Name, p.Tags })
            .Select(g => g.First())
            .ToList()) { Key = g.Key }; 
        
        return new ObservableCollection<FurTaskGroupList>(query.Reverse());
    }

    public static DateTime FromRfc3339String(string rfc3339String)
    {
        return System.Xml.XmlConvert.ToDateTime(rfc3339String, System.Xml.XmlDateTimeSerializationMode.Local);
    }

    public static TimeSpan RoundSeconds(TimeSpan span)
    {
        return TimeSpan.FromSeconds(Math.Floor(span.TotalSeconds));
    }

    public static void SystemNotification(string title, string body)
    {
        new ToastContentBuilder()
            .SetBackgroundActivation()
            .AddText(title)
            .AddText(body)
            .Show();
    }

    public void ResetTimer()
    {
        if (pomodoroEnabled)
        {
            var timeInMinutes = TimeSpan.FromMinutes(pomodoroTime);
            timerLabel.Text = timeInMinutes.ToString(@"hh\:mm\:ss");
        }
        else
        {
            timerLabel.Text = "00:00:00";
        }
    }
}


public class FurTaskGroupList : List<FurTask>
{
    public FurTaskGroupList(IEnumerable<FurTask> items) : base(items)
    {
        var allTasks = Database.GetData();

        var totalTime = TimeSpan.Zero;
        foreach (var task in allTasks)
        {
            if (task.StartTime.ToString("d", DateTimeFormatInfo.InvariantInfo) == items.First().StartTime.ToString("d", DateTimeFormatInfo.InvariantInfo))
            {
                totalTime = totalTime.Add(MainPage.RoundSeconds(task.StopTime.Subtract(task.StartTime)));
            }
        }
        TotalTime = totalTime.ToString(@"h\:mm\:ss");

        foreach (var item in items)
        {
            var itemsTotalTime = TimeSpan.Zero;
            foreach (var task in allTasks)
            {
                if (item.Name == task.Name 
                    && item.Tags == task.Tags 
                    && item.StartTime.ToString("d", DateTimeFormatInfo.InvariantInfo) 
                        == task.StartTime.ToString("d", DateTimeFormatInfo.InvariantInfo))
                {
                    itemsTotalTime = itemsTotalTime.Add(MainPage.RoundSeconds(task.StopTime.Subtract(task.StartTime)));
                }
            }
            item.SetTimeOfGroup(itemsTotalTime.ToString(@"h\:mm\:ss"));
        }

        var itemDate = items.First().StartTime.ToString("d", DateTimeFormatInfo.InvariantInfo);
        var yesterday = DateTime.Today.AddDays(-1);
        if (itemDate == DateTime.Today.ToString("d", DateTimeFormatInfo.InvariantInfo))
        {
            Day = "Today";
        }
        else if (itemDate == yesterday.ToString("d", DateTimeFormatInfo.InvariantInfo))
        {
            Day = "Yesterday";
        }
        else
        {
            // Different if year is different from current
            Day = items.First().StartTime.ToString("MMM d", DateTimeFormatInfo.InvariantInfo);
        }

    }
    public object Key { get; set; }
    public string TotalTime { get; set; }
    public string Day { get; set; }
    public string KeyAndString => Day + " - " + TotalTime;
}


public class FurTask
{
    public string ID { get; private set; }
    public string Name { get; private set; }
    public DateTime StartTime { get; private set; }
    public DateTime StopTime { get; private set; }
    public string Tags { get; private set; }
    public string TotalTime { get; }
    public string TimeOfGroup { get; set; }

    public FurTask(string id, string name, string startTime, string stopTime, string tags)
    {
        ID = id;
        Name = name;

        var startTimeDT = MainPage.FromRfc3339String(startTime);
        var stopTimeDT = MainPage.FromRfc3339String(stopTime);
        StartTime = startTimeDT;
        StopTime = stopTimeDT;
        TotalTime = MainPage.RoundSeconds(stopTimeDT.Subtract(startTimeDT)).ToString(@"h\:mm\:ss");

        if (!string.IsNullOrWhiteSpace(tags))
        {
            Tags = $"#{tags}";
        }
    }

    public void SetTimeOfGroup(string timeOfGroup)
    {
        TimeOfGroup = timeOfGroup;
    }

}
