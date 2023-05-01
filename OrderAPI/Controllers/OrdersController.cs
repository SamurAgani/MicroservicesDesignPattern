using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrderAPI.DTOs;
using OrderAPI.Models;
using Shared;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OrderAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;

        public OrdersController(AppDbContext context, IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
        }

        [HttpPost]
        public async Task<IActionResult> Create(OrderCreateDto orderCreateDto)
        {
            var newOrder = new Models.Order()
            {
               BuyerId = orderCreateDto.BuyerId,
               Status = OrderStatus.Suspend,
               Address = new Address() { District = orderCreateDto.address.District, Line = orderCreateDto.address.Line, Province = orderCreateDto.address.Province },
               CreatedDate = DateTime.Now
            };
            orderCreateDto.orderItems.ForEach(x =>
            {
                newOrder.Items.Add(new OrderItem() { Count = x.Count, Price = x.Price, ProductId = x.ProductId, });
            });
            await _context.AddAsync(newOrder);
            await _context.SaveChangesAsync();

            var newOrderCreatedEvent = new OrderCreatedEvent()
            {
                BuyerId = orderCreateDto.BuyerId,
                OrderId = newOrder.Id,
                Payment = new PaymentMessage() { CardName = orderCreateDto.payment.CardNumber, CardNumber = orderCreateDto.payment.CardNumber, 
                                                 CVV = orderCreateDto.payment.CVV,Expiration = orderCreateDto.payment.Expiration,
                                                 TotalPrice = orderCreateDto.orderItems.Sum(x=>x.Price*x.Count)}
            };
            orderCreateDto.orderItems.ForEach(x =>
            {
                newOrderCreatedEvent.orderItems.Add(new OrderItemMessage() { Count = x.Count, ProductId = x.ProductId, });
            });

            await _publishEndpoint.Publish(newOrderCreatedEvent);
            return Ok();
        }
    }
}
