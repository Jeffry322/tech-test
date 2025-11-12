using Microsoft.EntityFrameworkCore;
using Order.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Order.Data.Entities;
using Order.Data.Exceptions;

namespace Order.Data
{
    public class OrderRepository : IOrderRepository
    {
        private readonly OrderContext _orderContext;

        public OrderRepository(OrderContext orderContext)
        {
            _orderContext = orderContext;
        }

        public async Task<IEnumerable<OrderSummary>> GetOrdersAsync(CancellationToken token = default)
        {
            var orders = await _orderContext.Order
                .Include(x => x.Items)
                .Include(x => x.Status)
                .Select(OrderProjections.ToSummary)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync(token);

            return orders;
        }

        public async Task<OrderDetail> GetOrderByIdAsync(Guid orderId, CancellationToken token = default)
        {
            var orderIdBytes = orderId.ToByteArray();

            var order = await _orderContext.Order
                .Where(x => _orderContext.Database.IsInMemory()
                    ? x.Id.SequenceEqual(orderIdBytes)
                    : x.Id == orderIdBytes)
                .Select(x => new OrderDetail
                {
                    Id = new Guid(x.Id),
                    ResellerId = new Guid(x.ResellerId),
                    CustomerId = new Guid(x.CustomerId),
                    StatusId = new Guid(x.StatusId),
                    StatusName = x.Status.Name,
                    CreatedDate = x.CreatedDate,
                    TotalCost = x.Items.Sum(i => i.Quantity * i.Product.UnitCost).Value,
                    TotalPrice = x.Items.Sum(i => i.Quantity * i.Product.UnitPrice).Value,
                    Items = x.Items.Select(i => new Model.OrderItem
                    {
                        Id = new Guid(i.Id),
                        OrderId = new Guid(i.OrderId),
                        ServiceId = new Guid(i.ServiceId),
                        ServiceName = i.Service.Name,
                        ProductId = new Guid(i.ProductId),
                        ProductName = i.Product.Name,
                        UnitCost = i.Product.UnitCost,
                        UnitPrice = i.Product.UnitPrice,
                        TotalCost = i.Product.UnitCost * i.Quantity.Value,
                        TotalPrice = i.Product.UnitPrice * i.Quantity.Value,
                        Quantity = i.Quantity.Value
                    })
                }).SingleOrDefaultAsync(token);

            return order;
        }

        public async Task<IEnumerable<OrderSummary>> GetOrdersByStatusAsync(
            string status,
            CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return [];
            }
            
            var orders = await _orderContext.Order
                .Include(x => x.Items)
                .Include(x => x.Status)
                .Where(o => status == o.Status.Name)
                .Select(OrderProjections.ToSummary)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync(token);

            return orders;
        }

        public async Task<IEnumerable<OrderSummary>> GetOrdersByStatusAsync(
            Guid statusId,
            CancellationToken token = default)
        {
            var statusIdBytes = statusId.ToByteArray();
            
            var orders = await _orderContext.Order
                .Include(x => x.Items)
                .Include(x => x.Status)
                .Where(x => _orderContext.Database.IsInMemory()
                    ? x.Status.Id.SequenceEqual(statusIdBytes)
                    : x.Status.Id == statusIdBytes)
                .Select(OrderProjections.ToSummary)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync(token);

            return orders;
        }

        public async Task<StatusUpdateResult> UpdateOrderStatusAsync(
            Guid orderId,
            UpdateOrderStatusRequest newStatus,
            CancellationToken token = default)
        {
            var orderIdBytes = orderId.ToByteArray();
            
            var order = await _orderContext.Order
                .Include(o => o.Status)
                .Where(x => _orderContext.Database.IsInMemory()
                    ? x.Id.SequenceEqual(orderIdBytes)
                    : x.Id == orderIdBytes)
                .SingleOrDefaultAsync(token);
            
            if (order is null)
            {
                return StatusUpdateResult.NotFound;
            }
            
            byte[] newStatusIdBytes = null;
            
            if (!string.IsNullOrWhiteSpace(newStatus.NewStatusName))
            {
                newStatusIdBytes = await GetStatusIdByStatusName(newStatus.NewStatusName, token);
                
                if (newStatusIdBytes is null)
                {
                    return StatusUpdateResult.InvalidStatus;
                }
                if (newStatusIdBytes.SequenceEqual(order.StatusId))
                {
                    return StatusUpdateResult.NoChange;
                }
            }
            else if (newStatus.NewStatusId.HasValue)
            {
                newStatusIdBytes = newStatus.NewStatusId.Value.ToByteArray();
                var newStatusName = await GetStatusNameByStatusIdAsync(newStatusIdBytes, token);

                if (string.IsNullOrWhiteSpace(newStatusName))
                {
                    return StatusUpdateResult.InvalidStatus;
                }

                if (newStatusIdBytes.SequenceEqual(order.StatusId))
                {
                    return StatusUpdateResult.NoChange;
                }
            }
            else
            {
                return StatusUpdateResult.InvalidStatus;
            }
            
            order.StatusId = newStatusIdBytes;
                
            await _orderContext.SaveChangesAsync(token);
                
            return StatusUpdateResult.Updated;
        }

        public async Task<Guid> CreateOrderAsync(
            CreateOrderRequest createOrderRequest,
            CancellationToken token = default)
        {
            var productIds = createOrderRequest
                .Items.Select(i => i.ProductId)
                .ToArray();
            
            var serviceIds = await GetServiceIdsByProductIds(productIds, token);
            var newOrderId = Guid.NewGuid();
            
            var status = await _orderContext.OrderStatus
                .SingleOrDefaultAsync(s => s.Name == createOrderRequest.StatusName, token);
            
            List<Entities.OrderItem> items = [];
            items.AddRange(createOrderRequest.Items
                .Select(item => new Entities.OrderItem
            {
                Id = Guid.NewGuid().ToByteArray(),
                ProductId = item.ProductId.ToByteArray(),
                ServiceId = serviceIds[item.ProductId].ToByteArray(),
                Quantity = item.Quantity,
            }));

            var order = new Data.Entities.Order
            {
                Id = newOrderId.ToByteArray(),
                ResellerId = createOrderRequest.ResellerId.ToByteArray(),
                CustomerId = createOrderRequest.CustomerId.ToByteArray(),
                CreatedDate = DateTime.UtcNow,
                Status = status,
                Items = items
            };

            await _orderContext.AddAsync(order, token);
            await _orderContext.SaveChangesAsync(token);
            
            return new Guid(order.Id);
        }

        public async Task<decimal> GetFinishedOrdersProfitForMonthAsync(CancellationToken token = default)
        {
            var monthAgo = DateTime.UtcNow.AddMonths(-1);
            var now = DateTime.UtcNow;
            
            var result = await _orderContext.Order
                .Include(o => o.Status)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Where(o => o.Status.Name == "Completed")
                .Where(o => o.CreatedDate >= monthAgo && o.CreatedDate <= now)
                .Select(o => o.Items.Sum(i =>
                    (i.Product.UnitPrice - i.Product.UnitCost) * i.Quantity.Value))
                .SumAsync(token);
            
            return result;
        }

        private async Task<Dictionary<Guid, Guid>> GetServiceIdsByProductIds(
            IEnumerable<Guid> productIds,
            CancellationToken token = default)
        {
            var result = new Dictionary<Guid, Guid>();

            foreach (var productId in productIds)
            {
                var productIdBytes = productId.ToByteArray();

                var product = await _orderContext.OrderProduct
                    .SingleOrDefaultAsync(p => p.Id == productIdBytes, token);
                
                if (product is null)
                {
                    throw new ProductNotFoundException(productId);
                }
                
                result[productId] = new Guid(product.ServiceId);
            }

            return result;
        }
        
        private async Task<string> GetStatusNameByStatusIdAsync(
            byte[] newStatusIdBytes,
            CancellationToken token = default)
        {
            var newStatusName = await _orderContext.OrderStatus
                .Where(os => os.Id == newStatusIdBytes)
                .Select(os => os.Name)
                .SingleOrDefaultAsync(token);
            return newStatusName;
        }

        private async Task<byte[]> GetStatusIdByStatusName(
            string newStatusName,
            CancellationToken token = default)
        {
            var newStatusId = await _orderContext.OrderStatus
                .Where(os => os.Name == newStatusName)
                .Select(os => os.Id)
                .SingleOrDefaultAsync(token);
            return newStatusId;
        }
    }
    
    public static class OrderProjections
    {
        public static readonly Expression<Func<Entities.Order, OrderSummary>> ToSummary =
            x => new OrderSummary
            {
                Id = new Guid(x.Id),
                ResellerId = new Guid(x.ResellerId),
                CustomerId = new Guid(x.CustomerId),
                StatusId = new Guid(x.StatusId),
                StatusName = x.Status.Name,
                ItemCount = x.Items.Count,
                TotalCost = x.Items.Sum(i => i.Quantity * i.Product.UnitCost).Value,
                TotalPrice = x.Items.Sum(i => i.Quantity * i.Product.UnitPrice).Value,
                CreatedDate = x.CreatedDate
            };
    }
}