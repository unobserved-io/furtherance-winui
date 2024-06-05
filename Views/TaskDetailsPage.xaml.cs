using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Furtherance.Contracts.Services;
using Furtherance.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Furtherance.Views;

public class GroupedTask
{
    public string ID { get; set; }
    public string Name { get; set; }
    public string StartTime { get; set; }
    public string StopTime { get; set; }
    public string TotalTime { get; set; }
    public string Tags { get; set; }

    public GroupedTask(FurTask task)
    {
        ID = task.ID;
        Name = task.Name;
        // TODO Use a setting in the settings to change between 12 and 24 hour formats
        StartTime = task.StartTime.ToString("h:mm:ss tt", DateTimeFormatInfo.InvariantInfo);
        StopTime = task.StopTime.ToString("h:mm:ss tt", DateTimeFormatInfo.InvariantInfo);
        TotalTime = task.TotalTime;
        Tags = task.Tags;
    }
}

public sealed partial class TaskDetailsPage : Page
{
    public TaskDetailsViewModel ViewModel
    {
        get;
    }

    public static List<GroupedTask> GroupedTasks { get; set; }
    public static string clickedToEdit;
    private readonly INavigationService _navigationService;

    public TaskDetailsPage()
    {
        ViewModel = App.GetService<TaskDetailsViewModel>();
        InitializeComponent();
        _navigationService = App.GetService<INavigationService>();

        RefreshPage();
    }

    private void BackButton_Clicked(object sender, RoutedEventArgs e)
    {
        _navigationService.NavigateTo(typeof(MainViewModel).FullName);
    }

    private void DeleteButton_Clicked(object sender, RoutedEventArgs e)
    {
        // Delete all tasks in this group
        Database.DeleteByGroupedTask(GroupedTasks);
        _navigationService.NavigateTo(typeof(MainViewModel).FullName);
    }

    private void TaskClicked(object sender, ItemClickEventArgs e)
    {
        var task = e.ClickedItem as GroupedTask;
        clickedToEdit = task.ID;
        _navigationService.NavigateTo(typeof(TaskEditViewModel).FullName);
    }

    private void NewNameButton_Clicked(object sender, RoutedEventArgs e)
    {
        errorMessage.Visibility = Visibility.Collapsed;
        var nameChanged = !(newTaskNameInput.Text == GroupedTasks[0].Name);
        var tagsChanged = !(newTagsInput.Text == GroupedTasks[0].Tags);
        var newTags = "";

        if (nameChanged)
        {
            if (string.IsNullOrWhiteSpace(newTaskNameInput.Text))
            {
                errorMessage.Text = "Task name cannot be empty.";
                errorMessage.Visibility = Visibility.Visible;
            }
        }

        if (tagsChanged)
        {
            if (!string.IsNullOrWhiteSpace(newTagsInput.Text))
            {
                // Split and remove whitespace
                var words = newTagsInput.Text.Trim().Split('#').Select(word => word.Trim()).ToArray();
                // Throw out first element
                var tagList = new List<string>(words[1..]);
                // Remove empty tags
                tagList.RemoveAll(tag => string.IsNullOrWhiteSpace(tag));
                // Remove duplicate tags
                tagList = tagList.Distinct().ToList();
                // Lowercase tags
                tagList = tagList.ConvertAll(tag => tag.ToLower());
                newTags = string.Join(" #", tagList);
                if (string.IsNullOrEmpty(newTags))
                {
                    errorMessage.Text = "Tags should start with #.";
                    errorMessage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                }
            }
        }

        if (errorMessage.Visibility == Visibility.Collapsed) 
        {
            foreach (var task in GroupedTasks)
            {
                if (nameChanged)
                {
                    Database.UpdateTaskName(task.ID, newTaskNameInput.Text.Trim());
                }
                
                if (tagsChanged)
                {
                    Database.UpdateTaskTags(task.ID, newTags);
                }
            }
            MainPage.clickedTask = Database.GetByID(GroupedTasks[0].ID);
            RefreshPage();
            groupNameEdit.Flyout.Hide();
        }
    }

    private void RefreshPage()
    {
        var clickedTask = MainPage.clickedTask;
        var totalTime = TimeSpan.Zero;

        GroupedTasks = new List<GroupedTask>();

        var allTasks = Database.GetData();
        allTasks.Reverse();

        foreach (var task in allTasks)
        {
            if (clickedTask.Name == task.Name
                    && clickedTask.Tags == task.Tags
                    && clickedTask.StartTime.ToString("d", DateTimeFormatInfo.InvariantInfo)
                        == task.StartTime.ToString("d", DateTimeFormatInfo.InvariantInfo))
            {
                GroupedTasks.Add(new GroupedTask(task));
                totalTime = totalTime.Add(MainPage.RoundSeconds(task.StopTime.Subtract(task.StartTime)));

            }
        }

        groupDate.Text = clickedTask.StartTime.ToString("D", DateTimeFormatInfo.InvariantInfo);
        groupName.Text = GroupedTasks[0].Name;
        newTaskNameInput.Text = groupName.Text;
        groupTags.Text = GroupedTasks[0].Tags;
        newTagsInput.Text = groupTags.Text;
        // If no tags, groupName expands two rows and is vertically centered
        if (string.IsNullOrWhiteSpace(groupTags.Text))
        {
            Grid.SetRowSpan(groupName, 2);
        }
        else
        {
            Grid.SetRowSpan(groupName, 1);
        }
        groupTime.Text = totalTime.ToString(@"h\:mm\:ss");
        TaskList.ItemsSource = GroupedTasks;
    }
}