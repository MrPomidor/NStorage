using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NStorage.Tests.Integration
{
    /// <summary>
    /// Test encapsulate general success scenario, whewre several threads are reading and writing from storage in a paralell
    /// All data should be saved correctly without losses
    /// </summary>
    public class Success1
    {
        [Fact]
        public void Test()
        {
            // Prepare for tests
            var tempTestFolder = GetTempTestFolderPath("Integration/Success1");
            (var storagePath, var testDataPath) = GetMainArgs(tempTestFolder);

            // act
            NStorage.App.Program.Main(new[] { testDataPath, storagePath });

            // cleanup test
            // TODO cleanup in special xUnit class
            CleanupTest(tempTestFolder);
        }

        private string GetTempTestFolderPath(string testName)
        {
            var guid = Guid.NewGuid().ToString();
            var solutionFolder = Directory.GetCurrentDirectory();
            var path = Path.Combine(solutionFolder, testName, guid);
            Directory.CreateDirectory(path);
            return path;
        }

        private (string storageFilesFolder, string testFilderFolder) GetMainArgs(string tempTestFolder)
        {
            var testDataPath = GetLargeTestDataSetFolder();
            var storagePath = Path.Combine(tempTestFolder, "Storage");
            Directory.CreateDirectory(storagePath);
            return (storagePath, testDataPath);
        }

        private string GetLargeTestDataSetFolder()
        {
            var testFilesFolderPath = @"E:\PROJECTS\NStorage\NStorage\NStorage.Tests\TestData";
            if (!Directory.Exists(testFilesFolderPath) || Directory.GetFiles(testFilesFolderPath).Length == 0)
                throw new Exception($"No test data present ! Please execute \"init.ps1\" to fill test folder with data");

            return testFilesFolderPath;
        }

        private void CleanupTest(string tempTestFolder)
        {
            Directory.Delete(tempTestFolder, true);
        }
    }
}
