using Autofac;
using HootOut.CommonIntegrationTests.PostgreSQL;
using HootOut.Infraestructure.DI;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.CommonIntegrationTests
{
    internal class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<ClearAllTables>().AsSelf().SingleInstance();
        }
    }
}
