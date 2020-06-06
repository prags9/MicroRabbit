using MediatR;
using Microservice.Domain.Core.Bus;
using Microservice.Domain.Core.Commands;
using Microservice.Domain.Core.Events;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroRabbit.Infra.Bus
{
    public sealed class RabbitMqBus : IEventBus
    {
        private readonly IMediator _mediator;
        private readonly Dictionary<string, List<Type>> _handlers;
        private readonly List<Type> _eventTypes;

        public RabbitMqBus(IMediator mediator)
        {
            _mediator = mediator;
            _handlers = new Dictionary<string, List<Type>>();
            _eventTypes = new List<Type>();
        }

        public void Publish<T>(T @event) where T : Event
        {
            var factory = new ConnectionFactory() { HostName = "localhost" }; 

            using(var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                var eventName = @event.GetType().Name;

                channel.QueueDeclare(eventName, false, false, false, null);

                var message = JsonConvert.SerializeObject(@event);
                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish("", eventName, false, null);
            }
        }

        public Task SendCommand<T>(T command) where T : Command
        {
            return _mediator.Send(command);
        }

        public void Subscriber<T, TH>()
            where T : Event
            where TH : IEventHandler<T>
        {
            var eventName = typeof(T).Name;
            var handlerType = typeof(TH);

            if (!_eventTypes.Contains(typeof(T)))
            {
                _eventTypes.Add(typeof(T));
            }
            if (!_handlers.ContainsKey(eventName))
            {
                _handlers.Add(eventName, new List<Type>());
            }
            if(_handlers[eventName].Any(s => s.GetType() == handlerType))
            {
                throw new ArgumentException($"Handler type {handlerType.Name} already registered for '{eventName}'", nameof(handlerType));
            }

            _handlers[eventName].Add(handlerType);
            StartBasicConsume<T>();
        }

        private void StartBasicConsume<T>() where T : Event
        {
            //Async consumer
            var factory = new ConnectionFactory() { HostName = "localhost", DispatchConsumersAsync = true};
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();

            var eventName = typeof(T).Name;

            channel.QueueDeclare(eventName, false, false, false, null);
            //async type
            var consumer = new AsyncEventingBasicConsumer(channel);

            //Delegate is a pplacehoder for an event. It is a pointer to a method. += is a syntax for method pointer.
            //This is an event handler. When we want to register an event handler with an event we must follow this pattern.
            // The subscription implementation might be: += 
            // The unsubscription implementation might be: -=
            consumer.Received += Consumer_Received;     //Now subscribe event to event handler
            channel.BasicConsume(eventName, true, consumer);
        }

        private async Task Consumer_Received(object sender, BasicDeliverEventArgs e)
        {
            var eventName = e.RoutingKey;
            //e.Body is of type ReadOnlyMemory. Use Span property to convert message to string without additional memory allocation
            //Encoding.UTF8.GetString has an overload for `ReadOnlyMemory'
            var message = Encoding.UTF8.GetString(e.Body.Span);

            try
            {
                await ProcessEvent(eventName, message).ConfigureAwait(false);
            }
            catch (Exception)
            {

                throw;
            }
        }

        private async Task ProcessEvent(string eventName, string message)
        {
            if (_handlers.ContainsKey(eventName))
            {
                var subscriptions = _handlers[eventName];
                foreach(var subscription in subscriptions)
                {
                    var handler = Activator.CreateInstance(subscription);
                    if (handler == null) continue;
                    var eventType = _eventTypes.SingleOrDefault(s => s.Name == eventName);
                    var @event = JsonConvert.DeserializeObject(message, eventType);
                    //This is event handler
                    var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);
                    //Does the main work of routing to the correct handler
                    await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { @event });
                }
            }
        }
    }
}
