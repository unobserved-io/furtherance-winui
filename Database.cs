using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Furtherance.Views;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Windows.Storage;
using System.Collections;
using System.Threading.Tasks;
using System.Reflection.PortableExecutable;

namespace Furtherance;
public static class Database
{
    static string dbPath;

    public static void GetDatabasePath()
    {
        if (App.localSettings.Values["DatabaseLocation"] != null)
        {
            dbPath = (string)App.localSettings.Values["DatabaseLocation"];
        }
        else
        {
            dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "furtherance.db");
            App.localSettings.Values["DatabaseLocation"] = dbPath;
        }
        System.Diagnostics.Debug.WriteLine(dbPath);
    }

    public static async void InitializeDatabase()
    {
        GetDatabasePath();
        var dbFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(dbPath));
        await dbFolder.CreateFileAsync(Path.GetFileName(dbPath), CreationCollisionOption.OpenIfExists);
        using var db = new SqliteConnection($"Filename={dbPath}");
        db.Open();

        var tableCommand = "CREATE TABLE IF NOT " +
            "EXISTS tasks (id INTEGER PRIMARY KEY, " +
            "task_name TEXT," +
            "start_time TEXT," +
            "stop_time TEXT," +
            "tags TEXT)";

        var createTable = new SqliteCommand(tableCommand, db);

        createTable.ExecuteReader();
    }

    public static void AddData(string taskName, DateTime startTime, DateTime stopTime, string tags)
    {
        using var db = new SqliteConnection($"Filename={dbPath}");
        db.Open();

        var insertCommand = new SqliteCommand
        {
            Connection = db,
            CommandText = "INSERT INTO tasks VALUES " +
            "(NULL, @Task_Name, @Start_Time, @Stop_Time, @Tags);"
        };
        insertCommand.Parameters.AddWithValue("@Task_Name", taskName);
        insertCommand.Parameters.AddWithValue("@Start_Time", ToRfc3339String(startTime));
        insertCommand.Parameters.AddWithValue("@Stop_Time", ToRfc3339String(stopTime));
        insertCommand.Parameters.AddWithValue("@Tags", tags);

        insertCommand.ExecuteReader();

        db.Close();

    }

    public static List<FurTask> GetData()
    {
        var allTasks = new List<FurTask>();

        using (var db = new SqliteConnection($"Filename={dbPath}"))
        {
            db.Open();

            var selectCommand = new SqliteCommand
                ("SELECT * from tasks ORDER BY start_time DESC", db);

            var query = selectCommand.ExecuteReader();

            while (query.Read())
            {
                allTasks.Add(new FurTask(
                    query.GetString(0),
                    query.GetString(1),
                    query.GetString(2),
                    query.GetString(3),
                    query.GetString(4))
                );
            }

            db.Close();
        }

        return allTasks;
    }

    public static List<string> GetTaskSuggestions()
    {
        var allSuggestions = new List<string>();

        using (var db = new SqliteConnection($"Filename={dbPath}"))
        {
            db.Open();

            var selectCommand = new SqliteCommand
                ("SELECT * from tasks ORDER BY start_time DESC", db);

            var query = selectCommand.ExecuteReader();

            while (query.Read())
            {
                if (string.IsNullOrWhiteSpace(query.GetString(4)))
                {
                    allSuggestions.Add(query.GetString(1));
                }
                else
                {
                    allSuggestions.Add(query.GetString(1) + $" #{query.GetString(4)}");
                }
            }

            allSuggestions = allSuggestions.Distinct().ToList();

            db.Close();
        }

        return allSuggestions;
    }

    public static FurTask GetByID(string id)
    {
        FurTask task;

        using (var db =
           new SqliteConnection($"Filename={dbPath}"))
        {
            db.Open();

            var selectCommand = new SqliteCommand
            {
                Connection = db,
                CommandText = "SELECT * FROM tasks WHERE id = (@ID)"
            };
            selectCommand.Parameters.AddWithValue("@ID", id);

            var query = selectCommand.ExecuteReader();

            query.Read();
            task = new FurTask(
                query.GetString(0),
                query.GetString(1),
                query.GetString(2),
                query.GetString(3),
                query.GetString(4)
            );

            db.Close();
        }

        return task;
    }

    public static void UpdateByID(string id, string taskName, DateTime startTime, DateTime stopTime, string tags)
    {
        using var db = new SqliteConnection($"Filename={dbPath}");
        db.Open();

        var updateCommand = new SqliteCommand
        {
            Connection = db,
            CommandText = "UPDATE tasks SET task_name = (@Task_Name)," +
            "start_time = (@Start_Time), stop_time = (@Stop_Time), tags = (@Tags) " +
            "WHERE id = (@ID)"
        };
        updateCommand.Parameters.AddWithValue("@Task_Name", taskName);
        updateCommand.Parameters.AddWithValue("@Start_Time", ToRfc3339String(startTime));
        updateCommand.Parameters.AddWithValue("@Stop_Time", ToRfc3339String(stopTime));
        updateCommand.Parameters.AddWithValue("@Tags", tags);
        updateCommand.Parameters.AddWithValue("@ID", id);

        updateCommand.ExecuteReader();

        db.Close();

    }

    public static void UpdateTaskName(string id, string newTaskName)
    {
        using var db = new SqliteConnection($"Filename={dbPath}");
        db.Open();

        var updateCommand = new SqliteCommand
        {
            Connection = db,
            CommandText = "UPDATE tasks SET task_name = (@New_Task_Name)" +
            "WHERE id = (@ID)"
        };
        updateCommand.Parameters.AddWithValue("@New_Task_Name", newTaskName);
        updateCommand.Parameters.AddWithValue("@ID", id);

        updateCommand.ExecuteReader();

        db.Close();

    }

    public static void UpdateTaskTags(string id, string newTags)
    {
        using var db = new SqliteConnection($"Filename={dbPath}");
        db.Open();

        var updateCommand = new SqliteCommand
        {
            Connection = db,
            CommandText = "UPDATE tasks SET tags = (@New_Tags)" +
            "WHERE id = (@ID)"
        };
        updateCommand.Parameters.AddWithValue("@New_Tags", newTags);
        updateCommand.Parameters.AddWithValue("@ID", id);

        updateCommand.ExecuteReader();

        db.Close();

    }

    public static void DeleteByGroupedTask(List<GroupedTask> taskList)
    {
        using var db = new SqliteConnection($"Filename={dbPath}");
        db.Open();

        foreach (var task in taskList)
        {
            var deleteCommand = new SqliteCommand
            {
                Connection = db,
                CommandText = "DELETE FROM tasks WHERE id = (@ID)"
            };
            deleteCommand.Parameters.AddWithValue("@ID", task.ID);
            deleteCommand.ExecuteReader();
        }

        db.Close();
    }

    public static void DeleteByIDs(List<string> idList)
    {
        using var db = new SqliteConnection($"Filename={dbPath}");
        db.Open();

        foreach (var id in idList)
        {
            var deleteCommand = new SqliteCommand
            {
                Connection = db,
                CommandText = "DELETE FROM tasks WHERE id = (@ID)"
            };
            deleteCommand.Parameters.AddWithValue("@ID", id);
            deleteCommand.ExecuteReader();
        }

        db.Close();
    }

    public static void DeleteByID(string id)
    {
        using var db = new SqliteConnection($"Filename={dbPath}");
        db.Open();

        var deleteCommand = new SqliteCommand
        {
            Connection = db,
            CommandText = "DELETE FROM tasks WHERE id = (@ID)"
        };
        deleteCommand.Parameters.AddWithValue("@ID", id);
        deleteCommand.ExecuteReader();

        db.Close();
    }

    public static bool CheckDBValidity(SqliteConnection db)
    {
        db.Open();

        var selectCommand = new SqliteCommand
        {
            Connection = db,
            CommandText = "SELECT task_name, start_time, stop_time, tags FROM tasks ORDER BY ROWID ASC LIMIT 1"
        };

        try
        {
            var query = selectCommand.ExecuteReader();
            if (query.HasRows)
            {
                return true;
            }
            return false;
        }
        catch (Exception)
        {
            return false;
        }

    }

    public static void BackupDB(string backupPath)
    {
        try
        {
            using var db = new SqliteConnection($"Filename={dbPath}");
            db.Open();
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
            using var backupDb = new SqliteConnection($"Filename={backupPath}");
            backupDb.Open();
            db.BackupDatabase(backupDb);
        }
        catch (Exception)
        {
            ShowErrorDialog("Unable to back up the database.");
        }
       
    }

    public static void ImportDB(string importFile)
    {
        try
        {
            using var newDb = new SqliteConnection($"Filename={importFile}");

            if (CheckDBValidity(newDb))
            {
                newDb.Open();
                using var oldDb = new SqliteConnection($"Filename={dbPath}");
                oldDb.Open();
                newDb.BackupDatabase(oldDb);
            }
            else
            {
                ShowErrorDialog("The selected database is not a valid Furtherance database.");
            }

        }
        catch (Exception)
        {
            ShowErrorDialog("The selected database could not be imported.");
        }
    }

    public static async void ShowErrorDialog(string message)
    {
        ContentDialog dialog = new ContentDialog();

        dialog.XamlRoot = MainPage.mainPage.XamlRoot;
        dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
        dialog.Title = "Failed";
        dialog.Content = message;
        dialog.PrimaryButtonText = "OK";
        dialog.DefaultButton = ContentDialogButton.Primary;

        var result = await dialog.ShowAsync();
    }

    public static string ToRfc3339String(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", DateTimeFormatInfo.InvariantInfo);
    }
}
