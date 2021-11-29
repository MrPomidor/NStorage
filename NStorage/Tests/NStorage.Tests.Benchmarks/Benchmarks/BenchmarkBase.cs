using System;
using System.IO;

namespace NStorage.Tests.Benchmarks.Benchmarks
{
    public abstract class BenchmarkBase
    {
        protected string GetTempTestFolderPath(string testName)
        {
            var guid = Guid.NewGuid().ToString();
            var solutionFolder = Directory.GetCurrentDirectory();
            var path = Path.Combine(solutionFolder, testName, guid);
            Directory.CreateDirectory(path);
            return path;
        }

        protected void CleanupTest(string tempTestFolder)
        {
            Directory.Delete(tempTestFolder, true);
        }
    }
}
