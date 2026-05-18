using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

public class PositionLogger
{
    private Transform transformToLog;
    private string filePath;
    private int intervalMs;
    private Thread loggingThread;
    public bool isRunning;

    
    public PositionLogger(Transform transformToLog, string fileName = "position_log.csv", int intervalMs = 100)
    {
        this.transformToLog = transformToLog;
        this.intervalMs = intervalMs;

        filePath = Path.Combine(Application.persistentDataPath, fileName);
    }

    public void Start()
    {
        isRunning = true;
        loggingThread = new Thread(LogLoop);
        loggingThread.IsBackground = true;
        loggingThread.Start();
    }

    public void Stop()
    {
        isRunning = false;
        loggingThread?.Join();
    }

    private void LogLoop()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (isRunning)
        {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Vector3 pos = Vector3.zero;

            lock (locker)
            {
                pos = latestPosition;
            }

            string line = $"{timestamp}-{pos.x:F6}-{pos.y:F6}-{pos.z:F6}\n";

            File.AppendAllText(filePath, line);

            Thread.Sleep(intervalMs);
        }
    }


    private Vector3 latestPosition;
    private object locker = new object();
    public void UpdatePosition()
    {
        if (transformToLog != null)
        {
            lock (locker)
            {
                latestPosition = transformToLog.position;
                if (!Application.isPlaying)
                    Stop();
            }
        }
    }
}