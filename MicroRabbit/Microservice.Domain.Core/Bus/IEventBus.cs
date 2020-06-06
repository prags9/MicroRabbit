using Microservice.Domain.Core.Commands;
using Microservice.Domain.Core.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microservice.Domain.Core.Bus
{
    public interface IEventBus
    {
        //Put restriction on the type of T
        Task SendCommand<T>(T command) where T : Command;

        //In C# we use @ when we use a reserve keyword. Putting restriction on type of T.
        void Publish<T>(T @event) where T : Event;

        //T : Event Type. TH: Event Handler
        void Subscriber<T, TH>()
            where T : Event
            where TH : IEventHandler<T>;
    }
}
