using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NStorage.Tests
{
    public class IntegrationTest
    {
        [Fact]
        public void Test1()
        {
            // Prepare for tests
            var testFolder = "IntegrationTest/Test1";
            var guid = Guid.NewGuid().ToString();
            var folder = Directory.GetCurrentDirectory();
            var path = Path.Combine(folder, testFolder, guid);
            var testDataPath = Path.Combine(path, "TestData");
            var storagePath = Path.Combine(path, "Storage");
            Directory.CreateDirectory(testDataPath);
            Directory.CreateDirectory(storagePath);
            PrepareForTest(testDataPath);

            // act
            NStorage.App.Program.Main(new[] { testDataPath, storagePath });


            // cleanup test
            CleanupTest(path);
        }

        private void PrepareForTest(string testDataPath)
        {
            var folder = Directory.GetCurrentDirectory();
            var testFilesFolderPath = Path.Combine(folder, "TestFiles");

            foreach (var file in Directory.GetFiles(testFilesFolderPath))
            {
                var fileInfo = new FileInfo(file);
                var source = fileInfo.FullName;
                var dest = Path.Combine(testDataPath, fileInfo.Name);
                File.Copy(source, dest);
            }
        }

        private void CleanupTest(string testFolder)
        {
            Directory.Delete(testFolder, true);
        }
    }
}
