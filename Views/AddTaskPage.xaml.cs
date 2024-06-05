using System;
using System.Collections.Generic;
using System.Linq;
using Furtherance.Contracts.Services;
using Furtherance.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Furtherance.Views;

public sealed partial class AddTaskPage : Page
{
    public AddTaskViewModel ViewModel
    {
        get;
    }

    private DateTime startDateTime = DateTime.Now;
    private DateTime stopDateTime = DateTime.Now;
    private readonly INavigationService _navigationService;

    public AddTaskPage()
    {
        ViewModel = App.GetService<AddTaskViewModel>();
        InitializeComponent();
        _navigationService = App.GetService<INavigationService>();

        var now = DateTime.Now;
        startDate.SelectedDate = now;
        startTime.SelectedTime = now.AddMinutes(-1).TimeOfDay;
        endDate.SelectedDate = now;
        endTime.SelectedTime = now.TimeOfDay;
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

    private void SaveButton_Clicked(object sender, RoutedEventArgs e)
    {
        var tagsValid = true;
        string tags;
        if (!string.IsNullOrWhiteSpace(taskTags.Text))
        {
            // Split and remove whitespace
            var words = taskTags.Text.Trim().Split('#').Select(word => word.Trim()).ToArray();
            // Throw out first element
            if (!string.IsNullOrWhiteSpace(words[0]))
            {
                tagsValid = false;
            }
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

        errorMessage.Visibility = Visibility.Collapsed;
        if (string.IsNullOrWhiteSpace(taskName.Text))
        {
            errorMessage.Text = "Task name cannot be blank.";
            errorMessage.Visibility = Visibility.Visible;
        }
        else if (startDateTime > stopDateTime)
        {
            errorMessage.Text = "Start time cannot be later than end time.";
            errorMessage.Visibility = Visibility.Visible;
        }
        else if (!tagsValid)
        {
            errorMessage.Text = "Tags should start with #.";
            errorMessage.Visibility = Visibility.Visible;
        }
        else
        {
            Database.AddData(taskName.Text, startDateTime, stopDateTime, tags);
            _navigationService.NavigateTo(typeof(MainViewModel).FullName);
        }
    }

    private void CancelButton_Clicked(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo(typeof(MainViewModel).FullName);
    }
}
