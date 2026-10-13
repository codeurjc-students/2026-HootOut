using Autofac;
using HootOut.ChatServers.Services;
using HootOut.Contracts.ChatServers.Services;
using HootOut.Infraestructure.DI;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.ChatServers
{
    public sealed class RegistrationContianer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<ChatServerService>().As<IChatServerService>().SingleInstance();
        }
    }
}
