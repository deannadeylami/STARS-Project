using UnityEngine;
using System;

[Serializable]
public class LogEntry
{
    public string timestamp;
    public string level;
    public string system;
    public string message;
    public object data;
}

public static class Logging
{
    public static void Log(string system, string message, object data = null)
    {
        Write("INFO", system, message, data);
    }

    public static void Warning(string system, string message, object data = null)
    {
        Write("WARNING", system, message, data);
    }

    public static void Error(string system, string message, object data = null)
    {
        Write("ERROR", system, message, data);
    }

    private static void Write(string level, string system, string message, object data)
    {
        LogEntry entry = new LogEntry
        {
            timestamp = DateTime.UtcNow.ToString("o"),
            level = level,
            system = system,
            message = message,
            data = data
        };
        string json = JsonUtility.ToJson(entry, true);

        if (level == "ERROR")
            Debug.LogError(json);
        else if (level == "WARNING")
            Debug.LogWarning(json);
        else
            Debug.Log(json);
    }
}

