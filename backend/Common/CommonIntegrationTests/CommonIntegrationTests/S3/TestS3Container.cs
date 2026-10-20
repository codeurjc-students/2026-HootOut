using Amazon.S3;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using HootOut.FileStorage.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace HootOut.CommonIntegrationTests.S3
{
    public class TestS3Container : IAsyncLifetime
    {  
        private readonly IContainer rustfsContainer = new ContainerBuilder("rustfs/rustfs:latest")
            .WithPortBinding(9000, true)
            .WithEnvironment("RUSTFS_ACCESS_KEY", "s3_access")
            .WithEnvironment("RUSTFS_SECRET_KEY", "s3_secret") 
            .WithWaitStrategy(Wait.ForUnixContainer().UntilExternalTcpPortIsAvailable(9000))
            .Build();

        public async ValueTask InitializeAsync()
        {
            await rustfsContainer.StartAsync();

            Environment.SetEnvironmentVariable("S3_ACCESS_KEY", "s3_access");
            Environment.SetEnvironmentVariable("S3_SECRET_KEY", "s3_secret");
            Environment.SetEnvironmentVariable("S3_URL", $"http://{rustfsContainer.Hostname}:{rustfsContainer.GetMappedPublicPort(9000)}");
            Environment.SetEnvironmentVariable("S3_Public_URL", $"http://{rustfsContainer.Hostname}:{rustfsContainer.GetMappedPublicPort(9000)}"); 
        }

        public async ValueTask DisposeAsync()
        {
            await rustfsContainer.DisposeAsync();
            GC.SuppressFinalize(this);
        } 
    }
}
