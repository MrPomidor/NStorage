using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace NStorage.App
{
	// TODO move to test
    public class Program
    {
		public static void Main(string[] args)
		{
			// Storage folder - folder where NStorage repository files should be created
			// Data folder - folder with files to store in the storage

			if (args.Length < 2
				|| !Directory.Exists(args[0])
				|| !Directory.Exists(args[1]))
			{
				throw new ArgumentException("Usage: NStorage.App.exe StorageFolder DataFolder");
			}

			Run(storageFolder: args[0], dataFolder: args[1], null);
		}

		public static void Run(string storageFolder, string dataFolder, int? take = 10, StorageConfiguration storageConfiguration = null, StreamInfo streamInfo = null)
        {
			streamInfo = streamInfo ?? StreamInfo.Empty;

			if (!Directory.Exists(dataFolder) || !Directory.Exists(storageFolder))
			{
				throw new ArgumentException("Usage: NStorage.App.exe InputFolder StorageFolder");
			}

			storageConfiguration = storageConfiguration ?? new StorageConfiguration(storageFolder);

			var files = Directory.EnumerateFiles(dataFolder, "*", SearchOption.AllDirectories);
			files = take.HasValue ? files.Take(take.Value) : files;

			// Create storage and add data
			Console.WriteLine("Creating storage from " + dataFolder);
			Stopwatch sw = Stopwatch.StartNew();
			using (var storage = new BinaryStorage(storageConfiguration))
			{
				files
					.AsParallel().WithDegreeOfParallelism(4).ForAll(s =>
					{
						AddFile(storage, s, streamInfo);
					});

			}
			Console.WriteLine("Time to create: " + sw.Elapsed);

			// Open storage and read data
			Console.WriteLine("Verifying data");
			sw = Stopwatch.StartNew();
			using (var storage = new BinaryStorage(storageConfiguration))
			{
				files
					.AsParallel().WithDegreeOfParallelism(4).ForAll(s =>
					{
						using (var resultStream = storage.Get(s))
						{
							using (var sourceStream = new FileStream(s, FileMode.Open, FileAccess.Read))
							{
								if (sourceStream.Length != resultStream.Length)
								{
									throw new Exception(string.Format("Length did not match: Source - '{0}', Result - {1}", sourceStream.Length, resultStream.Length));
								}

								byte[] hash1, hash2;
								using (MD5 md5 = MD5.Create())
								{
									hash1 = md5.ComputeHash(sourceStream);

									md5.Initialize();
									hash2 = md5.ComputeHash(resultStream);
								}

								if (!hash1.SequenceEqual(hash2))
								{
									throw new Exception(string.Format("Hashes do not match for file - '{0}'  ", s));
								}
							}
						}
					});
			}
			Console.WriteLine("Time to verify: " + sw.Elapsed);
		}

		static void AddFile(IBinaryStorage storage, string fileName, StreamInfo streamInfo)
		{
			using (var file = new FileStream(fileName, FileMode.Open))
			{
				storage.Add(fileName, file, streamInfo);
			}
		}

		//static void AddBytes(IBinaryStorage storage, string key, byte[] data)
		//{
		//	StreamInfo streamInfo = new StreamInfo();
		//	using (MD5 md5 = MD5.Create())
		//	{
		//		streamInfo.Hash = md5.ComputeHash(data);
		//	}
		//	streamInfo.Length = data.Length;
		//	streamInfo.IsCompressed = false;

		//	using (var ms = new MemoryStream(data))
		//	{
		//		storage.Add(key, ms, streamInfo);
		//	}
		//}

		//static void Dump(IBinaryStorage storage, string key, string fileName)
		//{
		//	using (var file = new FileStream(fileName, FileMode.Create))
		//	{
		//		storage.Get(key).CopyTo(file);
		//	}
		//}
	}
}
