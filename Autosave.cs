using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage;

namespace Furtherance;

internal class Autosave
{
    public static void WriteAutosave(string name, DateTime startTime, DateTime stopTime, string tags)
    {
        string[] task =
        {
            name, Database.ToRfc3339String(startTime), Database.ToRfc3339String(stopTime), tags
        };

        File.WriteAllLinesAsync(GetAutosavePath(), task);
    }

    public static List<string> ReadAutosave()
    {
        return File.ReadAllLines(GetAutosavePath()).ToList();
    }

    public static bool AutosaveExists()
    {
        return File.Exists(GetAutosavePath());
    }

    public static void DeleteAutosave()
    {
        if (AutosaveExists())
        {
            try
            {
                File.Delete(GetAutosavePath());
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
                return;
            }
        }
    }

    private static string GetAutosavePath()
    {
        return Path.Combine(ApplicationData.Current.LocalFolder.Path, "autosave.txt");
    }
}
