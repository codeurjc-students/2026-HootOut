using Amazon.S3.Model;
using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.FileStorage;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.FileStorage.Configuration;
using HootOut.MediaFiles.Search;
using Microsoft.Extensions.Options;
using Moq;
using System.Net.Mime;
using System.Text;

namespace HootOut.FileStorage.IntegrationTests.Services
{
    [Collection("IntegrationTests")]
    public class FileStorageServiceTests : IClassFixture<TestS3Container>, IClassFixture<PostgresTestContainer>, IAsyncLifetime
    {
        TestS3Container s3Container;
        private IContainer container;
        private ClearAllTables clearTables;
        private IFileStorageService fileStorageService;

        private const string bucketName = "test";

        public FileStorageServiceTests(TestS3Container s3Container, PostgresTestContainer postgresContainer)
        {
            this.s3Container = s3Container ?? throw new ArgumentNullException(nameof(s3Container));

            var optionsMock = new Mock<IOptions<FileStorageSettings>>();
            optionsMock.Setup(x => x.Value).Returns(new FileStorageSettings
            {
                S3Bucket = bucketName,
                MaxFileBytes = 2000
            });

            var builder = postgresContainer.RegisterTestPersistenceProvider();
            builder.RegisterInstance(optionsMock.Object).As<IOptions<FileStorageSettings>>().SingleInstance();

            container = builder.Build();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(postgresContainer.postgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority); ;

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }

            this.fileStorageService = container.Resolve<IFileStorageService>();
        }
        public async ValueTask InitializeAsync()
        {
            await clearTables.ClearTablesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await clearTables.ClearTablesAsync();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task FileStorageService_UploadFileAsync_Ok()
        {
            var ct = TestContext.Current.CancellationToken;
            var text = "this is a test";
            var fileName = "test_file";
            var contentType = new ContentType().ToString();

            var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var result = await fileStorageService.UploadFileAsync(fileName, fileStream, contentType, Guid.NewGuid(), ct);

            result.Should().NotBeNull();
            result.FileName.Should().Be(fileName);
            result.ContentType.Should().Be(contentType);
            result.BytesSize.Should().Be(Encoding.UTF8.GetBytes(text).LongLength);

            var mediaFileSearch = container.Resolve<IMediaFileSearch>();
            var fileDb = await mediaFileSearch.GetMediaFileByIdAsync(result.Id, ct);
            fileDb.Should().NotBeNull();
            fileDb.Id.Should().Be(result.Id);

            var signedUrl = await container.Resolve<IMediaUrlService>().GetDownloadUrlAsync(result.Id, result.ContentType);
            signedUrl.Should().NotBeNullOrEmpty();
            result.Url.Should().Be(signedUrl);
        }
    }
}
