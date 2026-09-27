using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.RabbitMQ.Configuration
{
    public interface IRabbitConnectionFactory
    { 
        public ConnectionFactory GetConnectionFactory();
    }
}
