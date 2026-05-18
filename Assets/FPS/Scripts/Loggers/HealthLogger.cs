using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

public class HealthLogger
{
    private string filePath;
    private int intervalMs;
    private Thread loggingThread;
    public bool isRunning;

    public int FakeHealth = 10000;
    public HealthLogger(string fileName = "position_log.csv", int intervalMs = 100)
    {
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


            string line = $"{timestamp}-{FakeHealth}\n";

            File.AppendAllText(filePath, line);

            Thread.Sleep(intervalMs);
        }
    }

}