using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared;
using StockAPI.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StockAPI.Consumer
{
    public class OrderCreatedEventConsumer : IConsumer<OrderCreatedEvent>
    {
        private readonly AppDbContext _context;
        private ILogger<OrderCreatedEventConsumer> _logger;
        private readonly ISendEndpointProvider _sendEndpoint;
        private readonly IPublishEndpoint _publishEndpoint;

        public OrderCreatedEventConsumer(AppDbContext context, ILogger<OrderCreatedEventConsumer> logger, ISendEndpointProvider sendEndpoint, IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _logger = logger;
            _sendEndpoint = sendEndpoint;
            _publishEndpoint = publishEndpoint;
        }

        public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
        {
            var stockResult = new List<bool>();
            foreach(var item in context.Message.orderItems)
            {
                stockResult.Add(await _context.stocks.AnyAsync(x => x.ProductId == item.ProductId && x.Count > item.Count));

            }
            if (stockResult.All(x => x.Equals(true)))
            {
                foreach(var item in context.Message.orderItems)
                {
                    var stock = await _context.stocks.FirstOrDefaultAsync(x => x.ProductId == item.ProductId);
                    if(stock != null)
                    {
                        stock.Count -= item.Count;

                    }
                }
                _logger.LogInformation($"Stock was reserved for buyer Id: {context.Message.BuyerId}");
                var sendEndpoint = await _sendEndpoint.GetSendEndpoint(new System.Uri($"queue:{RabbitMQSettings.StockReservedEventQueueName}"));

                StockReservedEvent stockReservedEvent = new StockReservedEvent() {
                    Payment = context.Message.Payment,
                    BuyerId = context.Message.BuyerId,
                    OrderId = context.Message.OrderId,
                    OrderItems = context.Message.orderItems
                
                };
                await sendEndpoint.Send(stockReservedEvent);
            }
            else
            {
                await _publishEndpoint.Publish(new StockNotReservedEvent()
                {
                   OrderId = context.Message.OrderId,
                   Message = "Not enough stock" 
                });
                _logger.LogInformation($"Not enough stock: {context.Message.BuyerId}");
            }
        }
    }
}
