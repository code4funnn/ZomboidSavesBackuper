namespace ZomboidSavesBackuper.Logger
{
    public class ConsoleLogger : ILogger
    {
        private readonly ILogEntryBuilder _logEntryBuilder;

        public ConsoleLogger(ILogEntryBuilder logEntryBuilder)
        {
            _logEntryBuilder = logEntryBuilder;
        }

        public void Log(string message, LogType logType = LogType.Info)
        {
            Console.WriteLine(_logEntryBuilder.Build(message, logType));
        }
    }
}
