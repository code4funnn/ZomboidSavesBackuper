using ZomboidSavesBackuper;

using (Mutex mutex = new Mutex(false, "ZomboidAutoSaver"))
{
    if (!mutex.WaitOne(0, false))
    {
        Console.WriteLine("Another instance is already running! Terminating...");
        Thread.Sleep(5000);
        return;
    }

    var backuper = new SavesBackuper();
    backuper.Start();
}