using NStorage.Exceptions;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Xunit;

namespace NStorage.Tests.Integration.FullScenarious
{
    /// <summary>
    /// Checking correct behavior when using encryption
    /// </summary>
    [Collection("Sequential")]
    public class EncryptionReadWriteTests : IntegrationTestBase
    {
        public EncryptionReadWriteTests() : base("Integration/EncryptionReadWrite") { }

        [Fact]
        public void ReadData_InvalidEncryptionKey_ShouldThrowException()
        {
            // arrange
            var streamInfo = new StreamInfo { IsEncrypted = true };
            var storageConfiguration = GetConfiguration(_aesKey);
            var file = Directory.EnumerateFiles(_dataFolder, "*", SearchOption.AllDirectories).First();
            using (var storage = new BinaryStorage(storageConfiguration))
            {
                AddFile(storage, file, streamInfo);
            }
            byte[] newAesKey;
            using (var aes = Aes.Create())
            {
                newAesKey = aes.Key;
            }
            var invalidStorageConfiguration = GetConfiguration(newAesKey);

            // act
            using (var storage = new BinaryStorage(invalidStorageConfiguration))
            {
                // assert
                var exception = Assert.Throws<InvalidEncryptionKeyException>(() => storage.Get(file));
                Assert.NotNull(exception);
            }
        }

        private StorageConfiguration GetConfiguration(byte[] aesKey) => new StorageConfiguration(_storageFolder)
                .EnableEncryption(aesKey);
    }
}
