using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DisplayConsole : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI consoleText;

    public bool _displayConsoleGUI;
    static string myLog = "";
    private string output;
    private string lastOutput;

    void OnEnable() 
    {
        Application.logMessageReceived += Log;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= Log;
    }

    public void Log(string logString, string stackTrace, LogType type)
    {
        if (lastOutput != logString)
        {
            lastOutput = logString;

            // Extract the relevant information from the stack trace
            string[] traceLines = stackTrace.Split('\n');
            string relevantInfo = "";
            if (traceLines.Length > 1)
            {
                relevantInfo = traceLines[1].Trim();
            }

            // Construct the log message with script line/number
            string logMessage = $"{logString}\n{relevantInfo}";

            // Append to the log
            myLog = logMessage + "\n" + myLog;

            // Truncate if necessary
            if (myLog.Length > 5000)
            {
                myLog = myLog.Substring(0, 4000);
            }
        }
    }


    void Update()
    {
        if (_displayConsoleGUI == false)
        consoleText.text = myLog;
    }

    void OnGUI()
    {
        if (_displayConsoleGUI == true)
        {
            myLog = GUI.TextArea(new Rect(10, 10, Screen.width - 100, Screen.height - 100), myLog);
        }
    }
}
