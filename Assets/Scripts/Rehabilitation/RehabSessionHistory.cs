using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>One finished exercise session.</summary>
[Serializable]
public class RehabSessionRecord
{
    public string exercise;
    public string date;
    public string difficulty;
    public string hand;
    public int reps;
    public int attempts;
    public float totalSeconds;
    public float avgSecondsPerRep;
    public float bestRepSeconds;
    public float maxReachMetres;
    public float avgReachMetres;
    public float accuracyPercent;
}

[Serializable]
public class RehabHistoryFile
{
    public List<RehabSessionRecord> sessions = new List<RehabSessionRecord>();
}

/// <summary>
/// Saves every finished session to a JSON file on the device
/// (Application.persistentDataPath/rehab_sessions.json).
/// </summary>
public static class RehabSessionHistory
{
    private static string FilePath
    {
        get { return Path.Combine(Application.persistentDataPath, "rehab_sessions.json"); }
    }

    public static List<RehabSessionRecord> LoadAll()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var data = JsonUtility.FromJson<RehabHistoryFile>(File.ReadAllText(FilePath));
                if (data != null && data.sessions != null)
                    return data.sessions;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("Could not read rehab history: " + e.Message);
        }
        return new List<RehabSessionRecord>();
    }

    public static void Add(RehabSessionRecord record)
    {
        record.date = DateTime.Now.ToString("dd MMM yyyy HH:mm");
        var all = LoadAll();
        all.Add(record);
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(new RehabHistoryFile { sessions = all }, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning("Could not save rehab history: " + e.Message);
        }
    }

    public static List<RehabSessionRecord> ForExercise(string exercise)
    {
        return LoadAll().FindAll(r => r.exercise == exercise);
    }

    public static RehabSessionRecord GetLast(string exercise)
    {
        var list = ForExercise(exercise);
        return list.Count > 0 ? list[list.Count - 1] : null;
    }
}
