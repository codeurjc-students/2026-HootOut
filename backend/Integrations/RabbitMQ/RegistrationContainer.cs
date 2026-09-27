using Autofac;
using HootOut.Contracts.Common.Messages.Services;
using HootOut.Infraestructure.DI;
using HootOut.RabbitMQ.Configuration;
using HootOut.RabbitMQ.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.RabbitMQ
{
    public class RegistrationContainer : IRegistrationContainer
    {
        public void Register(ContainerBuilder builder)
        {
            builder.RegisterType<RabbitEnvironmentConnectionFactory>().As<IRabbitConnectionFactory>().SingleInstance();
            builder.RegisterType<RabbitPubSubService>().As<IPubSubService>().SingleInstance();
        }
    }
}
