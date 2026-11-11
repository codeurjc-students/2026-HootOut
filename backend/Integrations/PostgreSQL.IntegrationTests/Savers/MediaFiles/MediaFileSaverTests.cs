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
using Npgsql;

namespace HootOut.PostgreSQL.IntegrationTests.Savers.MediaFiles
{
    [Collection("IntegrationTests")]
    public class MediaFileSaverTests : IClassFixture<PostgresTestContainer>, IClassFixture<TestS3Container>, IAsyncLifetime
    {
        private readonly IContainer container;

        private readonly ClearAllTables clearTables;

        private readonly ISaver<MediaFile> saver;

        private readonly IPersistenceProvider provider;

        public MediaFileSaverTests(PostgresTestContainer postgresContainer)
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
        public async Task SaveAsync_ValidMediaFile_PersistsAllColumns()
        {
            var ct = TestContext.Current.CancellationToken;
            var mediaFile = BuildMediaFile("photo.png");

            await saver.SaveAsync(mediaFile, ct);

            var row = await GetRowAsync(mediaFile.Id, ct);
            row.Should().NotBeNull();
            row.Id.Should().Be(mediaFile.Id);
            row.FileName.Should().Be(mediaFile.FileName);
            row.Checksum.Should().Be(mediaFile.Checksum);
            row.ContentType.Should().Be(mediaFile.ContentType);
            row.BytesSize.Should().Be(mediaFile.BytesSize);
            row.Url.Should().Be(mediaFile.Url);
            row.OwnerId.Should().Be(mediaFile.OwnerId!.Value);
            row.Deleted.Should().BeFalse();
            // timestamptz stores microseconds while DateTime has 100 ns ticks
            row.CreatedAt.Should().BeCloseTo(mediaFile.CreatedAt, TimeSpan.FromMilliseconds(1));
            row.ModifiedAt.Should().BeCloseTo(mediaFile.ModifiedAt, TimeSpan.FromMilliseconds(1));
        }

        [Fact]
        public async Task SaveAsync_Many_PersistsEveryRow()
        {
            var ct = TestContext.Current.CancellationToken;

            await saver.SaveAsync(BuildMediaFile("1.png"), ct);
            await saver.SaveAsync(BuildMediaFile("2.png"), ct);
            await saver.SaveAsync(BuildMediaFile("3.png"), ct);

            var count = await CountRowsAsync(ct);
            count.Should().Be(3);
        }

        [Fact]
        public async Task SaveAsync_DuplicateId_ThrowsWrappedUniqueViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var mediaFile = BuildMediaFile("photo.png");
            await saver.SaveAsync(mediaFile, ct);

            var duplicate = BuildMediaFile("duplicate.png");
            duplicate.Id = mediaFile.Id;

            var act = () => saver.SaveAsync(duplicate, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>()
                .WithMessage($"*{mediaFile.Id}*");
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);

            var row = await GetRowAsync(mediaFile.Id, ct);
            row!.FileName.Should().Be("photo.png");
            (await CountRowsAsync(ct)).Should().Be(1);
        }

        [Fact]
        public async Task SaveAsync_NullOwnerId_ThrowsWrappedNotNullViolation()
        {
            var ct = TestContext.Current.CancellationToken;
            var mediaFile = BuildMediaFile("photo.png");
            mediaFile.OwnerId = null;

            var act = () => saver.SaveAsync(mediaFile, ct);

            var exception = await act.Should().ThrowExactlyAsync<Exception>();
            exception.WithInnerException<PostgresException>()
                .Which.SqlState.Should().Be(PostgresErrorCodes.NotNullViolation);

            (await CountRowsAsync(ct)).Should().Be(0);
        }

        [Fact]
        public async Task SaveAsync_CancelledToken_ThrowsOperationCanceledAndDoesNotPersist()
        {
            var mediaFile = BuildMediaFile("photo.png");
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var act = () => saver.SaveAsync(mediaFile, cts.Token);

            // The token is already cancelled when opening the connection, which happens outside the try/catch
            await act.Should().ThrowAsync<OperationCanceledException>();
            (await CountRowsAsync(TestContext.Current.CancellationToken)).Should().Be(0);
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

        private async Task<MediaFileRow?> GetRowAsync(Guid id, CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.QuerySingleOrDefaultAsync<MediaFileRow>(new CommandDefinition(
                """
                    SELECT "Id", "FileName", "Checksum", "ContentType", "BytesSize", "Url", "OwnerId", "Deleted", "CreatedAt", "ModifiedAt"
                    FROM "HootOut"."MediaFile"
                    WHERE "Id" = @Id
                """,
                new { Id = id },
                cancellationToken: ct));
        }

        private async Task<long> CountRowsAsync(CancellationToken ct)
        {
            await using var conn = await provider.OpenConnectionAsync(ct);
            return await conn.ExecuteScalarAsync<long>(new CommandDefinition(
                """SELECT COUNT(*) FROM "HootOut"."MediaFile" """,
                cancellationToken: ct));
        }

        /// <summary>
        /// Raw table row, including the Deleted column that the entity does not expose.
        /// </summary>
        private sealed class MediaFileRow
        {
            public Guid Id { get; set; }
            public string FileName { get; set; } = string.Empty;
            public string Checksum { get; set; } = string.Empty;
            public string ContentType { get; set; } = string.Empty;
            public long BytesSize { get; set; }
            public string Url { get; set; } = string.Empty;
            public Guid OwnerId { get; set; }
            public bool Deleted { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ModifiedAt { get; set; }
        }
    }
}
