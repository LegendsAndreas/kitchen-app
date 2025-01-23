using System.Diagnostics;
using System.Reflection;

namespace WebKitchen.Services;

public class ErrorService
{
    public class Logger
    {
        private string logPath = "";
        private string logName = "";

        public void SetCustomPath(string path)
        {
            logPath = path;
        }

        public void SetPath()
        {
            logPath = Path.Combine(Environment.CurrentDirectory, "Logs");
            logName = "log.txt";
        }

        public void SetPath(string folderName, string fileName)
        {
            Console.WriteLine(Environment.CurrentDirectory);
            logPath = Path.Combine(Environment.CurrentDirectory, folderName);
            logName = fileName + ".txt";
            Console.WriteLine(logPath);
        }

        public void Log(string logType, string message)
        {
            Console.WriteLine("Logging message...");

            var result = pathOrNameIsNotSet();
            if (result.status)
            {
                Console.WriteLine("Message did not get logged; " + result.message);
                return;
            }

            try
            {
                CheckDirectory();
                logMessage(logType, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error logging info: " + ex.Message);
                Console.WriteLine("StackTrace: " + ex.StackTrace);
                return;
            }
        }

        private (bool status, string message) pathOrNameIsNotSet()
        {
            if (logPath == "")
                return (true, "Log path is not set");

            if (logName == "")
                return (true, "Log name is not set");

            return (false, "");
        }

        private void logMessage(string type, string message)
        {
            var logText = $"{DateTime.Now}: [{type.ToUpper()}] {message}";
            using var logFile = File.AppendText(logPath + $"/{logName}");
            logFile.WriteLine(logText);
        }

        private void CheckDirectory()
        {
            bool exists = Directory.Exists(logPath);
            Console.WriteLine("exists: " + exists);
            if (!exists)
            {
                Console.WriteLine("Directory does not exist");
                Directory.CreateDirectory(logPath); // Create the folder if it doesn't exist
            }
        }
    }

    public class Error
    {
        public bool Status = false;
        public string Message = "";
    }
}