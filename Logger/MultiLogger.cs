namespace ZomboidSavesBackuper.Logger
{
    public class MultiLogger: ILogger
    {
        private readonly ILogger[] _loggers;

        public MultiLogger(ILogger[] loggers)
        {
            _loggers = loggers;
        }

        public void Log(string message, LogType logType = LogType.Info)
        {
            foreach (var logger in _loggers)
            {
                logger.Log(message, logType);
            }
        }
    }
}
