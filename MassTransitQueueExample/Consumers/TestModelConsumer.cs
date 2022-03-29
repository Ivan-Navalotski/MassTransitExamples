using System.Threading.Tasks;
using MassTransit;
using MassTransitQueueExample.Models;
using Microsoft.Extensions.Logging;

namespace MassTransitQueueExample.Consumers
{
    /// <summary>
    /// TestModelConsumer
    /// </summary>
    public class TestModelConsumer: IConsumer<TestModel>
    {
        private readonly ILogger<TestModelConsumer> _logger;

        /// <inheritdoc cref="TestModelConsumer"/>
        public TestModelConsumer(ILogger<TestModelConsumer> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Consume
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task Consume(ConsumeContext<TestModel> context)
        {
            await Task.Run(() =>
            {
                _logger.LogInformation("Value: {Value}", context.Message.Value);
            });
        }
    }
}
