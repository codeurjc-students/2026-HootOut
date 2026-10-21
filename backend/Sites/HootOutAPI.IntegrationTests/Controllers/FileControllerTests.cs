using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.Authentication.Responses;
using HootOut.Contracts.MediaFile.Responses;
using HootOut.HootOutAPI.IntegrationTests.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace HootOut.HootOutAPI.IntegrationTests.Controllers
{
    [Collection("IntegrationTests")]
    public class FileControllerTests : TestControllerBase, IClassFixture<APIFixture>, IDisposable, IAsyncLifetime
    {
        private readonly ILifetimeScope container;
        private ClearAllTables clearTables;

        public FileControllerTests(APIFixture apiFixture)
        {
            httpClient = apiFixture.CreateHttpsClient();
            container = apiFixture.AutofacRoot.BeginLifetimeScope();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(apiFixture.PostgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority);

            foreach (var value in defaultValues)
            {
                value.InitAsync().Wait();
            }
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
        public async Task FileUpload_ReturnsOk_One()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);


            var text = "this is a test";
            var fileName = "test_file.txt";
            var bytes = Encoding.UTF8.GetBytes(text);
            var form = new MultipartFormDataContent();
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

            form.Add(content, "file", fileName);

            var response = await SendAuthorizeHttpRequestFile(
               HttpMethod.Post, "/api/v1/files", form, auth!.AccessToken!, ct);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var fileUploadResponse = await response.Content.ReadFromJsonAsync<UploadFileResponse>(ct);
            fileUploadResponse.Should().NotBeNull();
            fileUploadResponse.FileName.Should().Be(fileName);
            fileUploadResponse.BytesSize.Should().Be(bytes.LongLength);
        }

        [Fact]
        public async Task FileUpload_BadRequest_SpoofFileExtension()
        {
            var ct = TestContext.Current.CancellationToken;
            await RegisterAsync(ct);

            var loginResponse = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);


            var text = "this is a test";
            var fileName = "test_file.png";
            var bytes = Encoding.UTF8.GetBytes(text);
            var form = new MultipartFormDataContent();
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");

            form.Add(content, "file", fileName);

            var response = await SendAuthorizeHttpRequestFile(
               HttpMethod.Post, "/api/v1/files", form, auth!.AccessToken!, ct);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        public void Dispose()
        {
            container.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
