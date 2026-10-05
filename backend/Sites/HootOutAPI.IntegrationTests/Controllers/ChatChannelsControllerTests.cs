using Autofac;
using FluentAssertions;
using HootOut.CommonDomain.DefaultValues;
using HootOut.CommonDomain.Persistence;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Contracts.Users.Dtos;
using HootOut.HootOutAPI.IntegrationTests.Common;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace HootOut.HootOutAPI.IntegrationTests.Controllers
{

    public class ChatChannelsControllerTests : IClassFixture<APIFixture>, IDisposable, IAsyncLifetime
    {
        private readonly ILifetimeScope container;
        private readonly HttpClient httpClient;
        private ClearAllTables clearTables;

        public ChatChannelsControllerTests(APIFixture apiFixture)
        {
            httpClient = apiFixture.CreateClient();
            container = apiFixture.AutofacRoot.BeginLifetimeScope();

            var testPersistenceProvider = container.Resolve<IPersistenceProvider>();
            Assert.Same(apiFixture.PostgreSQLProvider, testPersistenceProvider);

            clearTables = container.Resolve<ClearAllTables>();
            var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority);

            foreach (var value in defaultValues)
            {
                value.Init();
            }
        }

        public ValueTask InitializeAsync()
        {
            clearTables?.ClearTables();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            clearTables?.ClearTables();
            return ValueTask.CompletedTask;
        } 
        public void Dispose()
        {
            container.Dispose();
        }
    }
}
