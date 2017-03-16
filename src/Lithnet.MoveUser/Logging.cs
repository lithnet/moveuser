using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace Lithnet.Moveuser
{
    internal class Logging
    {
        public const string AppInstanceSeperator = "*************************************************************************************";
        public const string SectionSeperator = "-------------------------------------------------------------------";
        private static System.IO.StreamWriter logWriter;
        private static string logFile = string.Empty;
        private static string logFileName = Application.ProductName + ".log";
        public static LogLevel CurrentLogLevel = LogLevel.Info;

        public enum LogLevel : int
        {
            None = 0,
            Errors = 1,
            Warning = 2,
            Info = 4,
            DetailedInfo = 8,
            Debug = 16
        }

        public Logging()
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                CurrentLogLevel = LogLevel.Debug;
            }
        }

        ~Logging()
        {
            try
            {
                Logging.logWriter.Close();
            }
            catch
            {
            }
        }

        public static string LogFile
        {
            get { return Logging.logFile; }
            set { Logging.logFile = value; }
        }

        public static void Log(string message, bool timeStamp = true, bool addProcedureName = true, LogLevel logLevel = LogLevel.Info)
        {
            WriteToLog(message, timeStamp, addProcedureName, logLevel);
        }

        public static void Log(string message, string[] Params, bool timeStamp = true, bool addProcedureName = true, LogLevel logLevel = LogLevel.Info)
        {
            message = ReplaceParams(message, Params);
            WriteToLog(message, timeStamp, addProcedureName, logLevel);
        }

        public static void Log(string message, bool timeStamp = true, bool addProcedureName = true, LogLevel logLevel = LogLevel.Info, params string[] Params)
        {
            message = ReplaceParams(message, Params);
            WriteToLog(message, timeStamp, addProcedureName, logLevel);
        }

        public static void Log(string message, LogLevel logLevel = LogLevel.Info, params string[] Params)
        {
            bool timeStamp = true;
            bool addProcedureName = true;

            message = ReplaceParams(message, Params);
            WriteToLog(message, timeStamp, addProcedureName, logLevel);
        }

        public static void Log(string message, LogLevel logLevel = LogLevel.Info)
        {
            bool timeStamp = true;
            bool addProcedureName = true;

            WriteToLog(message, timeStamp, addProcedureName, logLevel);
        }

        public static void Log(string message)
        {
            bool timeStamp = true;
            bool addProcedureName = true;
            LogLevel logLevel = LogLevel.Info;

            WriteToLog(message, timeStamp, addProcedureName, logLevel);
        }

        public static void Log(string message, params string[] Params)
        {
            bool timeStamp = true;
            bool addProcedureName = true;
            LogLevel logLevel = LogLevel.Info;
            message = ReplaceParams(message, Params);
            WriteToLog(message, timeStamp, addProcedureName, logLevel);
        }

        private static void WriteToLog(string message, bool timeStamp = true, bool addProcedureName = true, LogLevel logLevel = LogLevel.Info)
        {
            if (Logging.logWriter == null)
                Logging.logWriter = GetLogWriter();
            Debug.WriteLine(message);

            if (CurrentLogLevel >= logLevel)
            {
                Console.WriteLine(message);

                if (addProcedureName)
                    message = GetCallingMethod() + ": " + message;

                if (timeStamp)
                    Logging.logWriter.Write(System.DateTime.Now.ToShortDateString() + " " + System.DateTime.Now.ToLongTimeString() + ": ");

                Logging.logWriter.WriteLine(message);
                Logging.logWriter.Flush();
            }

        }

        public static void Log(System.Exception ex, LogLevel logLevel)
        {
            Logging.Log(ex, false, logLevel);
        }

        public static void Log(System.Exception ex, bool email = false, LogLevel logLevel = LogLevel.Info)
        {
            string message = string.Empty;

            message += "*************************************************" + Environment.NewLine;
            message += "An exception has occured in " + GetCallingMethod() + Environment.NewLine;
            message += "Type: " + ex.GetType().ToString() + Environment.NewLine;
            if ((ex) is System.ComponentModel.Win32Exception)
                message += "Win32 Error Code: " + ((System.ComponentModel.Win32Exception)ex).ErrorCode + Environment.NewLine;
            if ((ex) is System.ComponentModel.Win32Exception)
                message += "Win32 Native Error Code: " + ((System.ComponentModel.Win32Exception)ex).NativeErrorCode + Environment.NewLine;
            if ((ex) is System.Net.WebException)
                message += "Status: " + ((System.Net.WebException)ex).Status.ToString() + Environment.NewLine;
            message += "Message: " + ex.Message + Environment.NewLine;
            if (ex.InnerException != null)
                message += "InnerException Message: " + ex.InnerException.Message + Environment.NewLine;
            message += "Source: " + ex.Source + Environment.NewLine;
            message += "TargetSite: " + ex.TargetSite.ToString() + Environment.NewLine;
            message += "StackTrace: " + ex.StackTrace + Environment.NewLine;
            message += "*************************************************" + Environment.NewLine;

            try
            {
                Logging.Log(message, timeStamp: false, logLevel: logLevel);
            }
            catch (Exception)
            {
                //Application.Log.WriteException(ex2, TraceEventType.Critical, ex.StackTrace);
                //My.Application.Log.DefaultFileLogWriter.Flush();
            }

            if (email)
            {
                //ExceptionHandler.TrySendHtmlError(ex, ExceptionHandler.ExceptionReportToEmailAddressList);
            }


        }

        private static string ReplaceParams(string message, string[] Params)
        {
            for (int x = 0; x <= Params.Length - 1; x++)
            {
                if (!Params[x].IsNullOrWhiteSpace())
                {
                    message = message.Replace("{" + x + "}", Params[x]);
                }
            }

            return message;
        }

        public static System.IO.FileStream GetLogStream()
        {
            try
            {
                System.IO.FileStream logFileStream = new System.IO.FileStream(Logging.logFile, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                return logFileStream;
            }
            catch (Exception ex)
            {
                Logging.Log(ex);
                return null;
            }
        }

        private static System.IO.StreamWriter GetLogWriter(bool truncateLog = false)
        {
            System.IO.FileStream logFileStream = null;
            System.IO.StreamWriter logWriter = null;
            System.IO.FileMode filemode = System.IO.FileMode.Append;
            if (truncateLog) filemode = System.IO.FileMode.Create;

            string logFileFromCommandLine = Environment.ExpandEnvironmentVariables(GetLogFileFromCommandLine());

            if (Logging.logFile.IsNullOrWhiteSpace())
            {
                if (!logFileFromCommandLine.IsNullOrWhiteSpace())
                {
                    Logging.logFile = System.IO.Path.GetFullPath(logFileFromCommandLine);
                }
                else
                {
                    //Log file not specified on the command line, try lets use the current app path and create a log file here
                    Logging.logFile = System.IO.Path.GetDirectoryName(Application.ExecutablePath) + "\\" + Logging.logFileName;
                }
            }


            if (!System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(Logging.logFile)))
            {
                try
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Logging.logFile));
                }
                catch
                {
                    //could not create the folder where the user specified the log file should go, so fall back to the temp folder
                    Logging.logFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Logging.logFileName);
                }
            }

            string errorMsg = string.Empty;
            try
            {

                logFileStream = new System.IO.FileStream(Logging.logFile, filemode, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite);
                logWriter = new System.IO.StreamWriter(logFileStream);
                return logWriter;
            }
            catch (UnauthorizedAccessException)
            {
                //we cannot write to this location, fall back to temp folder

                goto OpenTempLog;
            }
            catch (Exception ex)
            {
                if (System.IO.File.Exists(Logging.logFile))
                {
                    //Its possible a process that hasnt exited yet from our elevation is still using the log file. 
                    //Wait 5 seconds then try again
                    System.Threading.Thread.Sleep(5000);
                    try
                    {
                        // LogWriter = New System.IO.StreamWriter(_LogFile, True)
                        logFileStream = new System.IO.FileStream(Logging.logFile, filemode, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite);
                        logWriter = new System.IO.StreamWriter(logFileStream);
                    }
                    catch (Exception)
                    {
                        errorMsg = ex.Message;
                        goto OpenTempLog;
                    }
                }
                else
                {
                    errorMsg = ex.Message;
                    goto OpenTempLog;
                }
            }
            OpenTempLog:

            try
            {
                Logging.logFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Logging.logFileName);
                logFileStream = new System.IO.FileStream(Logging.logFile, filemode, System.IO.FileAccess.Write, System.IO.FileShare.ReadWrite);
                logWriter = new System.IO.StreamWriter(logFileStream);
                //LogWriter = New System.IO.StreamWriter(_LogFile, True)
                return logWriter;
            }
            catch (Exception)
            {
                //if (!Switch_Silent)
                //{
                //    TryHideProgressWindow();
                //    Interaction.MsgBox("The log file " + _LogFile + " could not be opened, and a new log file could not be created in the temp folder " + Environment.NewLine + ErrorMsg);
                //}
                //SystemManagement.TerminateWithError(ERR_CANT_OPEN_LOG_FILE);
            }

            return null;

        }

        private static string GetLogFileFromCommandLine()
        {
            string logFileToUse = string.Empty;


            foreach (string argument in Environment.GetCommandLineArgs())
            {
                // string argument = argument_loopVariable;
                //For Each argument In System.Environment.GetCommandLineArgs
                if (argument.ToLower().StartsWith("/logfile:"))
                {
                    //a config file was specified
                    logFileToUse = argument.Substring("/logfile:".Length, argument.Length - "/logfile:".Length);

                }
                else if (argument.ToLower().StartsWith("/log:"))
                {
                    //a config file was specified
                    logFileToUse = argument.Substring("/log:".Length, argument.Length - "/log:".Length);
                }
            }

            logFileToUse = logFileToUse.Trim('"');

            return logFileToUse;

        }

        private static string GetCallingMethod()
        {
            try
            {
                StackTrace stackTrace = new StackTrace();

                foreach (System.Diagnostics.StackFrame stackFrame in stackTrace.GetFrames())
                {
                    if (stackFrame.GetMethod().DeclaringType.FullName != stackTrace.GetFrame(0).GetMethod().DeclaringType.FullName)
                        return stackFrame.GetMethod().Name;
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        public static void CloseLog()
        {
            try
            {
                Logging.logWriter.Flush();
                Logging.logWriter.Close();
                Logging.logWriter = null;
            }
            catch
            {
            }
        }

        public static void ClearLog()
        {
            CloseLog();
            Logging.logWriter = GetLogWriter(true);
        }

        public static string GetLog()
        {
            try
            {
                System.IO.FileStream logFileStream = new System.IO.FileStream(Logging.logFile, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                System.IO.StreamReader logRead = new System.IO.StreamReader(logFileStream);
                string logContents = logRead.ReadToEnd();
                logRead.Close();
                logFileStream.Close();

                return logContents;
            }
            catch (Exception ex)
            {
                Logging.Log(ex);
                return string.Empty;
            }
        }
    }
}