namespace ZomboidSavesBackuper.Logger
{
    public class FileLogger : ILogger
    {
        private readonly string _fileName;
        private readonly ILogEntryBuilder _logEntryBuilder;
        public FileLogger(string fileName, ILogEntryBuilder logEntryBuilder)
        {
            _fileName = fileName;
            _logEntryBuilder = logEntryBuilder;

            if (File.Exists(_fileName))
            {
                File.Delete(_fileName);
            }
        }

        public void Log(string message, LogType logType = LogType.Info)
        {
            File.AppendAllText(_fileName, _logEntryBuilder.Build(message, logType) + "\r\n");
        }
    }
}
