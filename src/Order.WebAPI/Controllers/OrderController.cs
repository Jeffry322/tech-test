#nullable enable
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Order.Model;
using Order.Service;
using Order.WebAPI.Validators;

namespace Order.WebAPI.Controllers
{
    [ApiController]
    [Route("orders")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IValidator<CreateOrderRequest> _createOrderValidator;

        public OrderController(IOrderService orderService,
            IValidator<CreateOrderRequest> createOrderValidator)
        {
            _orderService = orderService;
            _createOrderValidator = createOrderValidator;
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Get(CancellationToken token = default)
        {
            var orders = await _orderService.GetOrdersAsync(token);
            return Ok(orders);
        }

        [HttpGet("{orderId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOrderById(
            Guid orderId,
            CancellationToken token = default)
        {
            var order = await _orderService.GetOrderByIdAsync(orderId, token);
            if (order != null)
            {
                return Ok(order);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet("status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOrdersByStatus(
            [FromQuery] string? statusName,
            [FromQuery] string? statusId,
            CancellationToken token = default)
        {
            if (Guid.TryParse(statusId, out var parsedId))
            {
                var result = await _orderService.GetOrdersByStatusAsync(
                    parsedId, token);
                return Ok(result);
            }

            if (!string.IsNullOrWhiteSpace(statusName))
            {
                var result = await _orderService.GetOrdersByStatusAsync(
                    statusName, token);
                return Ok(result);
            }

            return Ok(Enumerable.Empty<OrderSummary>());
        }

        [HttpGet("profit")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOrderProfit(CancellationToken token = default)
        {
            var result = await _orderService.GetFinishedOrdersProfitForMonthAsync(token);
            return Ok(result);
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateOrder(
            [FromBody] CreateOrderRequest createOrderRequest,
            CancellationToken token = default)
        {
            _createOrderValidator.ValidateAndThrow(createOrderRequest);
            var orderId = await _orderService.CreateOrderAsync(createOrderRequest, token);
            return CreatedAtAction(nameof(GetOrderById), new { orderId }, orderId);
        }

        [HttpPatch("{orderId:guid}/status")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateOrderStatus(
            [FromRoute] Guid orderId,
            [FromBody] UpdateOrderStatusRequest newStatus,
            CancellationToken token = default)
        {
            var updated = await _orderService.UpdateOrderStatusAsync(orderId, newStatus, token);
            return updated switch
            {
                StatusUpdateResult.NotFound => NotFound(),
                StatusUpdateResult.Updated => NoContent(),
                StatusUpdateResult.NoChange => NoContent(),
                StatusUpdateResult.InvalidStatus => BadRequest("Status not found"),
                _ => BadRequest(),
            };
        }
    }
}