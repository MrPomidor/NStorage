namespace NStorage.Tests.Common
{
    public static class TestConsts
    {
        public const string BasePath = @"E:\PROJECTS\NStorage\NStorage\NStorage.Tests\";
        public const string LargeSizeTestFilesFolderPath = BasePath + "LargeTestData";
        public const string SmallSizeTestFilesFolderPath = BasePath + "SmallTestData";

        public static string GetLargeTestDataSetFolder()
        {
            var testFilesFolderPath = TestConsts.LargeSizeTestFilesFolderPath;
            if (!Directory.Exists(testFilesFolderPath) || Directory.GetFiles(testFilesFolderPath).Length == 0)
                throw new Exception($"No test data present ! Please execute \"init.ps1\" to fill test folder with data");

            return testFilesFolderPath;
        }

        public static string GetSmallTestDataSetFolder()
        {
            var testFilesFolderPath = TestConsts.SmallSizeTestFilesFolderPath;
            if (!Directory.Exists(testFilesFolderPath) || Directory.GetFiles(testFilesFolderPath).Length == 0)
                throw new Exception($"No test data present ! Please execute \"init.ps1\" to fill test folder with data");

            return testFilesFolderPath;
        }

        public static string GetDataSetFolder(TestDataSet dataSet)
        {
            switch (dataSet)
            {
                case TestDataSet.LargeFiles:
                    return GetLargeTestDataSetFolder();
                case TestDataSet.SmallFiles:
                    return GetSmallTestDataSetFolder();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
