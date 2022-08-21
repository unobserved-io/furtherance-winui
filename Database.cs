using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Furtherance.Views;
using Microsoft.Data.Sqlite;
using Windows.Storage;

namespace Furtherance;
public static class Database
{
    public const string databaseName = "furtherance.db";

    public static async void InitializeDatabase()
    {
        await ApplicationData.Current.LocalFolder.CreateFileAsync(databaseName, CreationCollisionOption.OpenIfExists);
        var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, databaseName);
        System.Diagnostics.Debug.WriteLine(dbpath);
        using var db = new SqliteConnection($"Filename={dbpath}");
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
        var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, databaseName);
        using var db = new SqliteConnection($"Filename={dbpath}");
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

        var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, databaseName);
        using (var db = new SqliteConnection($"Filename={dbpath}"))
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

        var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, databaseName);
        using (var db = new SqliteConnection($"Filename={dbpath}"))
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

        var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, databaseName);
        using (var db =
           new SqliteConnection($"Filename={dbpath}"))
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
        var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, databaseName);
        using var db = new SqliteConnection($"Filename={dbpath}");
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
        var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, databaseName);
        using var db = new SqliteConnection($"Filename={dbpath}");
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
        var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, databaseName);
        using var db = new SqliteConnection($"Filename={dbpath}");
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
        var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, databaseName);
        using var db = new SqliteConnection($"Filename={dbpath}");
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
        var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, databaseName);
        using var db = new SqliteConnection($"Filename={dbpath}");
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
        var dbpath = Path.Combine(ApplicationData.Current.LocalFolder.Path, databaseName);
        using var db = new SqliteConnection($"Filename={dbpath}");
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

    public static string ToRfc3339String(this DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", DateTimeFormatInfo.InvariantInfo);
    }
}
