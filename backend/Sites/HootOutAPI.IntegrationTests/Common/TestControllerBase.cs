using FluentAssertions;
using HootOut.Contracts.Authentication.Requests;
using HootOut.Contracts.Authentication.Responses;
using HootOut.Contracts.Users.Dtos.Request;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace HootOut.HootOutAPI.IntegrationTests.Common
{
    public class TestControllerBase
    {
        protected HttpClient httpClient;
        protected const string DefaultUsername = "Test1";
        protected const string DefaultEmail = "email@email.com";
        protected const string DefaultPassword = "test1";

        protected readonly JsonWebTokenHandler jtwHandler = new();

        protected async Task<AuthResponse> RegisterAsync(
            CancellationToken ct,
            string username = DefaultUsername,
            string email = DefaultEmail,
            string password = DefaultPassword)
        {
            var response = await httpClient.PostAsJsonAsync(
                "/api/v1/auth/register",
                new CreateUserRequest { Username = username, Email = email, Password = password },
                ct);
            response.IsSuccessStatusCode.Should().BeTrue("the test user must be registered before the scenario starts");

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
            auth.Should().NotBeNull();
            return auth!;
        }

        protected Task<HttpResponseMessage> LoginAsync(string email, string password, CancellationToken ct) =>
            httpClient.PostAsJsonAsync(
                "/api/v1/auth/login",
                new AuthRequest { Email = email, Password = password },
                ct);

        protected async Task<AuthResponse> LoginAndReadAsync(CancellationToken ct)
        {
            var response = await LoginAsync(DefaultEmail, DefaultPassword, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(ct);
            auth.Should().NotBeNull();
            return auth!;
        }

        protected async Task<HttpResponseMessage> SendAuthorizeHttpRequest<T>(HttpMethod method, string url, T? body, string accessToken, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            if (body != null)
            {
                request.Content = JsonContent.Create<T>(body);
            }
            return await httpClient.SendAsync(request, ct);
        }

        protected async Task<HttpResponseMessage> SendAuthorizeHttpRequest(HttpMethod method, string url, string accessToken, CancellationToken ct)
        {
            return await SendAuthorizeHttpRequest<object>(method, url, null, accessToken, ct);
        }

        protected Guid GetUserIdFromAccessToken(string? accessToken)
        {
            return Guid.Parse(jtwHandler.ReadJsonWebToken(accessToken!).GetClaim("sub").Value);
        }
    }
}
