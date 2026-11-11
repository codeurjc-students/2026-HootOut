using Autofac;
using Dapper;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.CommonIntegrationTests.S3;
using HootOut.Contracts.Common.Saver;
using HootOut.Infraestructure.DI;
using HootOut.MediaFiles.Entities;
using HootOut.MediaFiles.Repositories;

namespace HootOut.PostgreSQL.IntegrationTests.Repositories.MediaFiles
{
    [Collection("IntegrationTests")]
    public class MediaFileRepositoryTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly IMediaFileRepository repository;

        private readonly ISaver<MediaFile> saver;

        private readonly IPersistenceProvider provider;

        public MediaFileRepositoryTests(PostgresTestContainer postgresContainer)
        {
            var builder = new ContainerBuilder();
            new RegistrationManager().RegisterAllAssemblies(builder);
            builder.RegisterInstance(postgresContainer.postgreSQLProvider!).As<IPersistenceProvider>().SingleInstance();
            container = builder.Build();

            provider = container.Resolve<IPersistenceProvider>();
            Assert.Same(postgresContainer.postgreSQLProvider, provider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority);

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }

            repository = container.Resolve<IMediaFileRepository>();
            saver = container.Resolve<ISaver<MediaFile>>();
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
        public async Task GetMediaFileById_Existing_ReturnsMappedEntity()
        {
            var ct = TestContext.Current.CancellationToken;
            var expected = BuildMediaFile("photo.png");
            await saver.SaveAsync(expected, ct);

            var result = await repository.GetMediaFileByIdAsync(expected.Id, ct);

            result.Should().NotBeNull();
            // timestamptz stores microseconds while DateTime has 100 ns ticks
            result.Should().BeEquivalentTo(expected, options => options
                .Using<DateTime>(ctx => ctx.Subject.Should().BeCloseTo(ctx.Expectation, TimeSpan.FromMilliseconds(1)))
                .WhenTypeIs<DateTime>());
        }

        [Fact]
        public async Task GetMediaFileById_NotExisting_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            await saver.SaveAsync(BuildMediaFile("other.png"), ct);

            var result = await repository.GetMediaFileByIdAsync(Guid.NewGuid(), ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetMediaFileById_EmptyTable_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;

            var result = await repository.GetMediaFileByIdAsync(Guid.NewGuid(), ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetMediaFileById_Deleted_ReturnsNull()
        {
            var ct = TestContext.Current.CancellationToken;
            var mediaFile = BuildMediaFile("deleted.png");
            await saver.SaveAsync(mediaFile, ct);
            await MarkAsDeletedAsync(mediaFile.Id, ct);

            var result = await repository.GetMediaFileByIdAsync(mediaFile.Id, ct);

            result.Should().BeNull();
        }

        [Fact]
        public async Task GetMediaFileById_Many_ReturnsRequestedOne()
        {
            var ct = TestContext.Current.CancellationToken;
            var first = BuildMediaFile("1.png");
            var second = BuildMediaFile("2.png");
            var third = BuildMediaFile("3.png");
            await saver.SaveAsync(first, ct);
            await saver.SaveAsync(second, ct);
            await saver.SaveAsync(third, ct);

            var result = await repository.GetMediaFileByIdAsync(second.Id, ct);

            result.Should().NotBeNull();
            result.Id.Should().Be(second.Id);
            result.FileName.Should().Be("2.png");
        }

        [Fact]
        public async Task GetMediaFileById_CancelledToken_ThrowsOperationCanceled()
        {
            var mediaFile = BuildMediaFile("photo.png");
            await saver.SaveAsync(mediaFile, TestContext.Current.CancellationToken);

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => repository.GetMediaFileByIdAsync(mediaFile.Id, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        private static MediaFile BuildMediaFile(string fileName)
        {
            var now = DateTime.UtcNow;
            return new MediaFile
            {
                Id = Guid.NewGuid(),
                FileName = fileName,
                Checksum = Guid.NewGuid().ToString("N"),
                ContentType = "image/png",
                BytesSize = 1024,
                Url = $"http://localhost/media/{fileName}",
                OwnerId = Guid.NewGuid(),
                CreatedAt = now,
                ModifiedAt = now
            };
        }

        private async Task MarkAsDeletedAsync(Guid id, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                """UPDATE "HootOut"."MediaFile" SET "Deleted" = true WHERE "Id" = @Id""",
                new { Id = id },
                cancellationToken: ct));
        }
    }
}
