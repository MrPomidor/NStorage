using System;
using System.IO;

namespace NStorage.Samples.EventListenerDemo
{
    // TODO write some use case(s) with console
    public class Program
    {
        private static readonly ConsoleColor DefaultForegroundColor = Console.ForegroundColor;

        private static IBinaryStorage _storage;
        private static NStorageEventListener _eventListener;

        public static void Main(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                WriteError("Expecting path to storage directory");
                return;
            }
            if (args.Length > 1)
            {
                WriteError("Expecting path to storage directory. Additional arguments not supported");
            }

            var storagePath = args[0];
            if (!Directory.Exists(storagePath))
            {
                WriteError($"Incorrect folder path: {storagePath}");
                return;
            }

            try
            {
                _eventListener = new NStorageEventListener();
                _storage = new BinaryStorage(new StorageConfiguration(storagePath).SetFlushModeDeferred(100));
                while (true)
                {
                    WriteInfo("Please, write path to file (or \":q\" to exit) and press ENTER:");
                    var filePath = Console.ReadLine();
                    if (filePath == ":q")
                    {
                        break;
                    }
                    if (!File.Exists(filePath))
                    {
                        WriteError($"Incorrect file path: {filePath}");
                        continue;
                    }
                    using (var stream = File.OpenRead(filePath))
                    {
                        _storage.Add(filePath, stream, StreamInfo.Compressed);
                    }
                    continue;
                }
            }
            finally
            {
                try
                {
                    _storage?.Dispose();
                    _storage = null;
                }
                catch
                {
                    // TODO error message
                }

                WriteInfo("==============================================");
                WriteInfo("Counters statistict:");
                var countersSnapshot = _eventListener.GetCountersSnapshot();
                foreach (var key in countersSnapshot.Keys)
                {
                    var value = countersSnapshot[key];
                    WriteInfo($"{key}\t\t:{value}");
                }

                try
                {
                    _eventListener?.Dispose();
                    _eventListener = null;
                }
                catch
                {
                    // TODO error message
                }
            }

            WriteInfo("Press any key ...");
            Console.ReadKey();
        }

        private static void WriteInfo(string message)
        {
            Console.WriteLine(message);
        }

        private static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ForegroundColor = DefaultForegroundColor;
        }

    }

}
