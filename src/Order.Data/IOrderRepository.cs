using Order.Model;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Order.Data
{
    public interface IOrderRepository
    {
        Task<IEnumerable<OrderSummary>> GetOrdersAsync(CancellationToken token = default);

        Task<OrderDetail> GetOrderByIdAsync(Guid orderId, CancellationToken token = default);
        Task<IEnumerable<OrderSummary>> GetOrdersByStatusAsync(string status, CancellationToken token = default);
        Task<IEnumerable<OrderSummary>> GetOrdersByStatusAsync(Guid status, CancellationToken token = default);
        Task<StatusUpdateResult> UpdateOrderStatusAsync(Guid orderId, UpdateOrderStatusRequest newStatus, CancellationToken token = default);
        Task<Guid> CreateOrderAsync(CreateOrderRequest createOrderRequest, CancellationToken token = default);
        Task<decimal> GetFinishedOrdersProfitForMonthAsync(CancellationToken token = default);
    }
}
