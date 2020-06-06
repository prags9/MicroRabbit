using MicroRabbit.Infra.Bus;
using Microservice.Domain.Core.Bus;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace MicroRabbit.Infra.IoC
{
    public class DependencyContainer
    {
        //we can use this across any microservice, mvc, presentation service or console app to register dependency container
        public static void RegisterServices(IServiceCollection services)
        {
            //Domain Bus
            services.AddTransient<IEventBus, RabbitMqBus>()
        }
    }
}
