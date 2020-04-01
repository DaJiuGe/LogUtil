using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LogUtil
{
    public class LogLib
    {
        public enum LogMode
        {
            Synchro,
            SynchroWithStreamOpenOnce,
            Async
        }

        public enum LogLevel
        {
            Info,
            Debug,
            Warn,
            Error
        }

        public enum Tag
        {
            None,
            ProcedureIn,
            ProcedureOut,
            InterfaceIn,
            InterfaceOut,
            TestStart,
            TestStop
        }

        private static LogMode _logMode = LogMode.Synchro;
        private static string _logDir = "Log";
        private static string _logFile = ".\\log.txt";
        private static string _dateTime = $"{DateTime.Now:yyyy-MM-dd}";
        private static ConcurrentQueue<string> _logQueue = new ConcurrentQueue<string>();
        private static CancellationTokenSource _cts = null;
        private static object _logSignLock = new object();
        private static int _logSign = 0;
        private static object _flushLock = new object();
        private static int _flushCount = 0;
        private const int _flushTarget = 100;
        private static object _synchroLock = new object();
        private static StreamWriter _synchronizedStream = null;

        public static void Activate(LogMode logMode = LogMode.Synchro)
        {
            CreateLogFile();
            _logMode = logMode;
            if (_logMode == LogMode.Async)
            {
                _cts = new CancellationTokenSource();
                Task.Run(LogTask, _cts.Token);
            }
            else if (_logMode == LogMode.SynchroWithStreamOpenOnce)
            {
                _synchronizedStream = (StreamWriter)TextWriter.Synchronized(new StreamWriter(_logFile, true, Encoding.UTF8));
            }
            else
            {
                // Do Nothing
            }
        }

        private static void CreateLogFile()
        {
            if (!Directory.Exists(_logDir))
            {
                Directory.CreateDirectory(_logDir);
            }
            _logFile = $"{_logDir}\\{DateTime.Now:yyyy-MM-dd}.txt";
        }

        private static void LogTask()
        {
            using (StreamWriter sw = new StreamWriter(_logFile, true, Encoding.UTF8))
            {
                while (true)
                {
                    if (_cts.Token.IsCancellationRequested)
                    {
                        sw.Flush();
                        _cts = null;
                        break;
                    }

                    _logQueue.TryDequeue(out string newItem);

                    string date = $"{DateTime.Now:yyyy-MM-dd}";
                    if (!_dateTime.Equals(date))
                    {
                        CreateLogFile();
                        LogSynchro(newItem);
                        CancellationTokenSource cts = _cts;
                        _cts = new CancellationTokenSource();
                        Task.Run(LogTask, _cts.Token);
                        cts.Cancel();
                        cts = null;
                        return;
                    }

                    if (newItem != null)
                    {
                        if (newItem.Equals("[End]"))
                        {
                            lock (_logSignLock)
                            {
                                _logSign--;
                            }
                        }
                        else
                        {
                            sw.WriteLine(newItem);
                            _flushCount++;
                            if (_flushCount == _flushTarget)
                            {
                                sw.Flush();
                                _flushCount = 0;
                            }
                        }
                    }
                }
            }
        }

        public static void Release()
        {
            if (_logMode == LogMode.Async)
            {
                _cts?.Cancel();
            }
            else if (_logMode == LogMode.SynchroWithStreamOpenOnce)
            {
                _synchronizedStream?.Flush();
                _synchronizedStream?.Dispose();
                _synchronizedStream?.Close();
            }
            else
            {
                // Do Nothing
            }
        }

        public static void LogBegin()
        {
            if (_logMode == LogMode.Async)
            {
                lock (_logSignLock)
                {
                    _logSign++;
                }
            }
            else
            {
                // Do Nothing
            }
        }

        public static void LogEnd()
        {
            if (_logMode == LogMode.Async)
            {
                _logQueue.Enqueue("[End]");
            }
            else
            {
                // Do Nothing
            }
        }

        public static bool IsLogFinished()
        {
            return (_logSign == 0 && _logQueue.Count == 0);
        }

        public static void Log(string msg, LogLevel logLevel = LogLevel.Info, Tag tag = Tag.None)
        {
            string levelStr;
            switch (logLevel)
            {
                case LogLevel.Debug:
                    levelStr = "[D]";
                    break;
                case LogLevel.Warn:
                    levelStr = "[W]";
                    break;
                case LogLevel.Error:
                    levelStr = "[E]";
                    break;
                default:
                    levelStr = "[I]";
                    break;
            }

            string tagStr;
            switch (tag)
            {
                case Tag.InterfaceIn:
                    tagStr = "[Intf][In]";
                    break;
                case Tag.InterfaceOut:
                    tagStr = "[Intf][Out]";
                    break;
                case Tag.ProcedureIn:
                    tagStr = "[Proc][In]";
                    break;
                case Tag.ProcedureOut:
                    tagStr = "[Proc][Out]";
                    break;
                case Tag.TestStart:
                    tagStr = "[Test][Start]";
                    break;
                case Tag.TestStop:
                    tagStr = "[Test][Stop]";
                    break;
                default:
                    tagStr = string.Empty;
                    break;
            }

            string logItem = $"[{DateTime.Now:HH:mm:ss-fff}]{levelStr} : {tagStr}{msg}";

            if (_logMode == LogMode.Async)
            {
                LogAsync(logItem);
            }
            else if (_logMode == LogMode.Synchro)
            {
                LogSynchro(logItem);
            }
            else
            {
                CreateLogFile();
                LogSynchroWithStreamOpenOnce(logItem);
            }
        }

        private static void LogAsync(string msg)
        {
            _logQueue.Enqueue(msg);
        }

        private static void LogSynchro(string msg)
        {
            CreateLogFile();
            lock (_synchroLock)
            {
                using (StreamWriter sw = new StreamWriter(_logFile, true, Encoding.UTF8))
                {
                    sw.WriteLine(msg);
                    sw.Flush();
                }
            }
        }

        private static void LogSynchroWithStreamOpenOnce(string msg)
        {
            string date = $"{DateTime.Now:yyyy-MM-dd}";
            if (!_dateTime.Equals(date))
            {
                Release();
                Activate(LogMode.SynchroWithStreamOpenOnce);
            }
            _synchronizedStream.WriteLine(msg);
            lock (_flushLock)
            {
                _flushCount++;
                if (_flushCount == _flushTarget)
                {
                    _synchronizedStream.Flush();
                    _flushCount = 0;
                }
            }
        }
    }
}