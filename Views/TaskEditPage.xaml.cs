using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Furtherance.Contracts.Services;
using Furtherance.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Furtherance.Views;

public sealed partial class TaskEditPage : Page
{
    public TaskEditViewModel ViewModel
    {
        get;
    }

    private readonly FurTask thisTask = Database.GetByID(TaskDetailsPage.clickedToEdit);
    private DateTime startDateTime;
    private DateTime stopDateTime;
    private readonly INavigationService _navigationService;

    public TaskEditPage()
    {
        ViewModel = App.GetService<TaskEditViewModel>();
        InitializeComponent();
        _navigationService = App.GetService<INavigationService>();

        taskName.Text = thisTask.Name;
        taskTags.Text = thisTask.Tags;
        startDate.SelectedDate = thisTask.StartTime;
        startTime.SelectedTime = thisTask.StartTime.TimeOfDay;
        endDate.SelectedDate = thisTask.StopTime;
        endTime.SelectedTime = thisTask.StopTime.TimeOfDay;
        endDate.MinYear = startDate.Date;
        ChangeSavedDates();
    }

    private void SelectedDateChanged(DatePicker sender, DatePickerSelectedValueChangedEventArgs args)
    {
        errorMessage.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;

        if (startDate.SelectedDate == null || endDate.SelectedDate == null)
        {
            errorMessage.Text = "Please pick a date.";
            errorMessage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
        else
        {
            ChangeSavedDates();
            if (startDate.SelectedDate > endDate.SelectedDate)
            {
                errorMessage.Text = "Start date cannot be later than end date.";
                errorMessage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
            else
            {
                endDate.MinYear = startDate.Date;
            }
        }
    }

    private void SelectedTimeChanged(TimePicker sender, TimePickerSelectedValueChangedEventArgs args)
    {
        errorMessage.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;

        if (startDate.SelectedDate == null || endDate.SelectedDate == null)
        {
            errorMessage.Text = "Please pick a date.";
            errorMessage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
        else
        {
            ChangeSavedDates();
            if (startDateTime > stopDateTime)
            {
                errorMessage.Text = "Start time cannot be later than end time.";
                errorMessage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            }
        }
    }

    private void SaveButton_Clicked(object sender, RoutedEventArgs e)
    {
        var tagsValid = true;
        string tags;
        if (taskTags.Text != thisTask.Tags)
        {
            if (!string.IsNullOrWhiteSpace(taskTags.Text)) {
                // Split and remove whitespace
                var words = taskTags.Text.Trim().Split('#').Select(word => word.Trim()).ToArray();
                // Throw out first element
                var tagList = new List<string>(words[1..]);
                // Remove empty tags
                tagList.RemoveAll(tag => string.IsNullOrWhiteSpace(tag));
                // Remove duplicate tags
                tagList = tagList.Distinct().ToList();
                // Lowercase tags
                tagList = tagList.ConvertAll(tag => tag.ToLower());
                tags = string.Join(" #", tagList);
                if (string.IsNullOrEmpty(tags))
                {
                    tagsValid = false;
                }
            }
            else
            {
                tags = "";
            }
            
        }
        else
        {
            tags = thisTask.Tags.TrimStart('#');
        }

        errorMessage.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        if (string.IsNullOrWhiteSpace(taskName.Text))
        {
            errorMessage.Text = "Task name cannot be blank.";
            errorMessage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
        else if (startDateTime > stopDateTime)
        {
            errorMessage.Text = "Start time cannot be later than end time.";
            errorMessage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
        else if (!tagsValid)
        {
            errorMessage.Text = "Tags should start with #.";
            errorMessage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
        else
        {
            Database.UpdateByID(thisTask.ID, taskName.Text, startDateTime, stopDateTime, tags);
            GoBack();
        }
    }

    private void CancelButton_Clicked(object sender, RoutedEventArgs e)
    {
        GoBack();
    }

    private void DeleteButton_Clicked(object sender, RoutedEventArgs e)
    {
        Database.DeleteByID(thisTask.ID);
        GoBack();
    }

    private void GoBack()
    {
        if (IsSingular())
        {
            _navigationService.NavigateTo(typeof(MainViewModel).FullName);
        }
        else
        {
            TaskDetailsPage.clickedToEdit = thisTask.ID;
            _navigationService.NavigateTo(typeof(TaskDetailsViewModel).FullName);
        }
    }

    private bool IsSingular()
    {
        var counter = 0;
        foreach (var task in Database.GetData())
        {
            if (thisTask.Name == task.Name
                && thisTask.Tags == task.Tags
                && thisTask.StartTime.ToString("d", DateTimeFormatInfo.InvariantInfo)
                    == task.StartTime.ToString("d", DateTimeFormatInfo.InvariantInfo))
            {
                counter++;
                if (counter > 1)
                {
                    break;
                }
            }
        }
        // Account for both 0 and 1 depending on if deleted or canceled, respectively.
        if (counter < 2)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private void ChangeSavedDates()
    {
        startDateTime = new DateTime(
            startDate.SelectedDate.Value.Year,
            startDate.SelectedDate.Value.Month,
            startDate.SelectedDate.Value.Day,
            startTime.SelectedTime.Value.Hours,
            startTime.SelectedTime.Value.Minutes,
            startTime.SelectedTime.Value.Seconds);
        stopDateTime = new DateTime(
            endDate.SelectedDate.Value.Year,
            endDate.SelectedDate.Value.Month,
            endDate.SelectedDate.Value.Day,
            endTime.SelectedTime.Value.Hours,
            endTime.SelectedTime.Value.Minutes,
            endTime.SelectedTime.Value.Seconds);
    }
}
