using System;
using System.IO;

namespace NStorage.Tests
{
    public static class Consts
    {
        public const string TestFilesFolderPath = @"E:\PROJECTS\NStorage\NStorage\NStorage.Tests\TestData";

        public static string GetLargeTestDataSetFolder()
        {
            var testFilesFolderPath = Consts.TestFilesFolderPath;
            if (!Directory.Exists(testFilesFolderPath) || Directory.GetFiles(testFilesFolderPath).Length == 0)
                throw new Exception($"No test data present ! Please execute \"init.ps1\" to fill test folder with data");

            return testFilesFolderPath;
        }
    }
}
