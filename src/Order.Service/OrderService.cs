using Order.Data;
using Order.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Order.Service
{
    public class OrderService : IOrderService
    {
        private readonly IOrderRepository _orderRepository;

        public OrderService(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        public async Task<IEnumerable<OrderSummary>> GetOrdersAsync(CancellationToken token = default)
        {
            var orders = await _orderRepository.GetOrdersAsync(token);
            return orders;
        }

        public async Task<OrderDetail> GetOrderByIdAsync(Guid orderId, CancellationToken token = default)
        {
            var order = await _orderRepository.GetOrderByIdAsync(orderId, token);
            return order;
        }

        public async Task<IEnumerable<OrderSummary>> GetOrdersByStatusAsync(string status, CancellationToken token = default)
        {
            var orders = await _orderRepository.GetOrdersByStatusAsync(status, token);
            return orders;
        }

        public async Task<IEnumerable<OrderSummary>> GetOrdersByStatusAsync(Guid statusId, CancellationToken token = default)
        {
            var orders = await _orderRepository.GetOrdersByStatusAsync(statusId, token);
            return orders;
        }

        public async Task<StatusUpdateResult> UpdateOrderStatusAsync(Guid orderId, UpdateOrderStatusRequest newStatus, CancellationToken token = default)
        {
            var result = await _orderRepository.UpdateOrderStatusAsync(orderId, newStatus, token);
            return result;       
        }

        public async Task<Guid> CreateOrderAsync(CreateOrderRequest createOrderRequest, CancellationToken token = default)
        {
            var order = await _orderRepository.CreateOrderAsync(createOrderRequest, token);
            return order;
        }

        public async Task<decimal> GetFinishedOrdersProfitForMonthAsync(CancellationToken token = default)
        {
            var result = await _orderRepository.GetFinishedOrdersProfitForMonthAsync(token);
            return result;
        }
    }
}
