using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Furtherance.Views;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static PInvoke.User32;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Furtherance;

public class TaskReportGroup : List<FurTask>
{
    public TaskReportGroup(IEnumerable<FurTask> items, string key, string organizer = "none") : base(items)
    {
        Key = key;

        var totalTime = TimeSpan.Zero;
        foreach (var item in items)
        {
            totalTime = totalTime.Add(MainPage.RoundSeconds(item.StopTime.Subtract(item.StartTime)));
        }
        TotalTime = totalTime.ToString(@"h\:mm\:ss");

        var subGroup = new List<TaskReportGroup>();
        if (organizer == "name")
        {
            TaskReportSubGroup = items
                .GroupBy(g => g.Tags)
                .Select(g => new TaskReportGroup(g, string.IsNullOrWhiteSpace(g.Key) ? "no tags" : g.Key))
                .ToList();
            // Don't show any tags if the task doesn't have any tags to show (including "no tags")
            if (TaskReportSubGroup.Count == 1 && TaskReportSubGroup[0].Key == "no tags")
            {
                TaskReportSubGroup.Clear();
            }
        }
        else if (organizer == "tag")
        {
            if (string.IsNullOrEmpty(items.First().Tags))
            {
                Key = "no tags";
            }
            TaskReportSubGroup = items
                .GroupBy(g => g.Name)
                .Select(g => new TaskReportGroup(g, g.Key))
                .ToList();
        }

        if (key == "No Results")
        {
            TotalTime = "";
        }
    }
    public string Key { get; set; }
    public string TotalTime { get; set; }
    public List<TaskReportGroup> TaskReportSubGroup { get; set; }
}


public sealed partial class ReportWindow : Window
{
    public ReportWindow()
    {
        InitializeComponent();

        Title = "Generate Report";
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        SetWindowDetails(hwnd, 420, 700);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.SetIcon("Assets/furtherance.ico");

        endCalendar.MaxDate = DateTime.Today;
        startCalendar.MaxDate = DateTime.Today;
    }

    private void Timeframe_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (pickADatePanel != null)
        {
            var comboBox = sender as ComboBox;
            if (comboBox.SelectedIndex != 5)
            {
                pickADatePanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                pickADatePanel.Visibility = Visibility.Visible;
            }
        }
    }

    private void Filter_Checked(object sender, RoutedEventArgs e)
    {
        var checkBox = sender as CheckBox;
        if (checkBox.IsChecked == true)
        {
            filterPanel.Visibility = Visibility.Visible;
        }
        else
        {
            filterPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void Calendar_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (endCalendar.Date != null)
        {
            startCalendar.MaxDate = (DateTimeOffset)endCalendar.Date;
            if (startCalendar.Date > endCalendar.Date)
            {
                startCalendar.Date = endCalendar.Date;
            }
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshReport();
    }

    private void RefreshReport()
    {
        totalTimeText.Visibility = Visibility.Collapsed;

        var activeRange = timeframeCombo.SelectedIndex;
        var allTasks = Database.GetData();
        // Get date range
        var today = DateTime.Today;
        DateTime rangeStartDate;
        var rangeEndDate = today;


        switch (activeRange) {
            case 0:
                rangeStartDate = today.AddDays(-6);
                break;
            case 1:
                var dayOfMonth = today.Day - 1;
                rangeStartDate = today.AddDays(-dayOfMonth);
                break;
            case 2:
                rangeStartDate = today.AddDays(-30);
                break;
            case 3:
                rangeStartDate = today.AddDays(-180);
                break;
            case 4:
                rangeStartDate = today.AddDays(-365);
                break;
            default:
                // Parse user inputted start and stop dates
                rangeStartDate = startCalendar.Date.Value.DateTime;
                rangeEndDate = endCalendar.Date.Value.DateTime;
                break;
        }

        // Get tasks in date range
        var tasksInRange = new List<FurTask>();
        var tasksInRangeTotalTime = TimeSpan.Zero;
        foreach (var task in allTasks)
        {
            var startDate = task.StartTime.Date;
            if (startDate >= rangeStartDate && startDate <= rangeEndDate)
            {
                if (filterCheck.IsChecked == true && !string.IsNullOrWhiteSpace(filterTextBox.Text))
                {
                    var chosenFilter = filterTextBox.Text.Trim().Split(',').Select(g => g.Trim()).ToList();
                    chosenFilter.RemoveAll(g => string.IsNullOrWhiteSpace(g));
                    chosenFilter = chosenFilter.Distinct().ToList();
                    chosenFilter = chosenFilter.ConvertAll(g => g.ToLower());

                    if (filterCombo.SelectedIndex == 0)
                    {
                        // Filter by tasks
                        if (chosenFilter.Contains(task.Name.ToLower()))
                        {
                            tasksInRange.Add(task);
                            tasksInRangeTotalTime = tasksInRangeTotalTime
                                .Add(MainPage.RoundSeconds(task.StopTime.Subtract(task.StartTime)));
                        }
                    }
                    else
                    {
                        // Filter by tags
                        if (!string.IsNullOrWhiteSpace(task.Tags))
                        {
                            // Get task's tags as a List
                            var tagList = task.Tags.Split('#').ToList();
                            tagList.RemoveAll(g => string.IsNullOrWhiteSpace(g));
                            if (chosenFilter.Intersect(tagList).Any())
                            {
                                tasksInRange.Add(task);
                                tasksInRangeTotalTime = tasksInRangeTotalTime
                                    .Add(MainPage.RoundSeconds(task.StopTime.Subtract(task.StartTime)));
                            }
                        }
                    }
                }
                else
                {
                    tasksInRange.Add(task);
                    tasksInRangeTotalTime = tasksInRangeTotalTime
                        .Add(MainPage.RoundSeconds(task.StopTime.Subtract(task.StartTime)));
                }
            }
        }

        if (tasksInRange.Any())
        {
            if (sortRadio.SelectedIndex == 0)
            {
                // Sort by task selected
                TreeViewData.ItemsSource = GetTasksGrouped(tasksInRange, "name");
            }
            else if (sortRadio.SelectedIndex == 1)
            {
                // Sort by tag selected
                TreeViewData.ItemsSource = GetTasksGrouped(tasksInRange, "tag");
            }
            // Set total time label
            var totalTimeString = tasksInRangeTotalTime.ToString(@"h\:mm\:ss");
            totalTimeText.Text = $"Total Time: {totalTimeString}";
            totalTimeText.Visibility = Visibility.Visible;
        }
        else
        {
            // No tasks in range. Add a task report group with the name No Results.
            var dateTime = Database.ToRfc3339String(DateTime.Now);
            var noTasksTask = new FurTask("0", "No Results", dateTime, dateTime, "");
            var noTasksTaskList = new List<FurTask>
            {
                noTasksTask
            };
            TreeViewData.ItemsSource = GetTasksGrouped(noTasksTaskList, "name");
        }
    }

    private static ObservableCollection<TaskReportGroup> GetTasksGrouped(List<FurTask> tasksInRange, string organizer)
    {
        if (organizer == "name")
        {
            IEnumerable<TaskReportGroup> query;
            query = from item in tasksInRange
                    group item by item.Name into g
                    orderby g.Key
                    select new TaskReportGroup(g, g.Key, organizer);

            return new ObservableCollection<TaskReportGroup>(query.Reverse());
        }
        else
        {
            IEnumerable<TaskReportGroup> query;
            query = from item in tasksInRange
                    group item by item.Tags into g
                    orderby g.Key
                    select new TaskReportGroup(g, g.Key, organizer);

            return new ObservableCollection<TaskReportGroup>(query.Reverse());
        }
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
