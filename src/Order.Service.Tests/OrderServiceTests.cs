using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NUnit.Framework;
using Order.Data;
using Order.Data.Entities;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Order.Data.Exceptions;
using Order.Model;
using OrderItem = Order.Data.Entities.OrderItem;

namespace Order.Service.Tests
{
    public class OrderServiceTests
    {
        private IOrderService _orderService;
        private IOrderRepository _orderRepository;
        private OrderContext _orderContext;
        private DbConnection _connection;

        private readonly byte[] _orderStatusCreatedId = Guid.NewGuid().ToByteArray();
        private readonly byte[] _orderStatusInProgressId = Guid.NewGuid().ToByteArray();
        private readonly byte[] _orderStatusCompleted = Guid.NewGuid().ToByteArray();
        private readonly byte[] _orderServiceEmailId = Guid.NewGuid().ToByteArray();
        private readonly byte[] _orderProductEmailId = Guid.NewGuid().ToByteArray();


        [SetUp]
        public async Task Setup()
        {
            var options = new DbContextOptionsBuilder<OrderContext>()
                .UseSqlite(CreateInMemoryDatabase())
                .EnableDetailedErrors(true)
                .EnableSensitiveDataLogging(true)
                .Options;

            _connection = RelationalOptionsExtension.Extract(options).Connection;

            _orderContext = new OrderContext(options);
            _orderContext.Database.EnsureDeleted();
            _orderContext.Database.EnsureCreated();

            _orderRepository = new OrderRepository(_orderContext);
            _orderService = new OrderService(_orderRepository);

            await AddReferenceDataAsync(_orderContext);
        }

        [TearDown]
        public void TearDown()
        {
            _connection.Dispose();
            _orderContext.Dispose();
        }


        private static DbConnection CreateInMemoryDatabase()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            return connection;
        }

        [Test]
        public async Task GetOrdersAsync_ReturnsCorrectNumberOfOrders()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 1);

            var orderId2 = Guid.NewGuid();
            await AddOrder(orderId2, 2);

            var orderId3 = Guid.NewGuid();
            await AddOrder(orderId3, 3);

            // Act
            var orders = await _orderService.GetOrdersAsync();

            // Assert
            Assert.AreEqual(3, orders.Count());
        }

        [Test]
        public async Task GetOrdersAsync_ReturnsOrdersWithCorrectTotals()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 1);

            var orderId2 = Guid.NewGuid();
            await AddOrder(orderId2, 2);

            var orderId3 = Guid.NewGuid();
            await AddOrder(orderId3, 3);

            // Act
            var orders = await _orderService.GetOrdersAsync();

            // Assert
            var order1 = orders.SingleOrDefault(x => x.Id == orderId1);
            var order2 = orders.SingleOrDefault(x => x.Id == orderId2);
            var order3 = orders.SingleOrDefault(x => x.Id == orderId3);

            Assert.AreEqual(0.8m, order1.TotalCost);
            Assert.AreEqual(0.9m, order1.TotalPrice);

            Assert.AreEqual(1.6m, order2.TotalCost);
            Assert.AreEqual(1.8m, order2.TotalPrice);

            Assert.AreEqual(2.4m, order3.TotalCost);
            Assert.AreEqual(2.7m, order3.TotalPrice);
        }

        [Test]
        public async Task GetOrderByIdAsync_ReturnsCorrectOrder()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 1);

            // Act
            var order = await _orderService.GetOrderByIdAsync(orderId1);

            // Assert
            Assert.AreEqual(orderId1, order.Id);
        }

        [Test]
        public async Task GetOrderByIdAsync_ReturnsCorrectOrderItemCount()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 1);

            // Act
            var order = await _orderService.GetOrderByIdAsync(orderId1);

            // Assert
            Assert.AreEqual(1, order.Items.Count());
        }

        [Test]
        public async Task GetOrderByIdAsync_ReturnsOrderWithCorrectTotals()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOrder(orderId1, 2);

            // Act
            var order = await _orderService.GetOrderByIdAsync(orderId1);

            // Assert
            Assert.AreEqual(1.6m, order.TotalCost);
            Assert.AreEqual(1.8m, order.TotalPrice);
        }

        [Test]
        public async Task GetOrderByStatus_ReturnsOrdersWithCorrectStatus()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            var orderId2 = Guid.NewGuid();
            await AddOrderInProgress(orderId1, 2);
            await AddOrder(orderId2, 2);

            // Act
            var orders = await _orderService
                .GetOrdersByStatusAsync(new Guid(_orderStatusInProgressId));

            var ordersByStatusName = await _orderService.GetOrdersByStatusAsync("In Progress");
            
            // Assert
            foreach (var order in orders)
            {
                Assert.AreEqual(new Guid(_orderStatusInProgressId), order.StatusId);
            }
            
            foreach (var order in ordersByStatusName)
            {
                Assert.AreEqual(new Guid(_orderStatusInProgressId), order.StatusId);
            }
        }
        
        [Test]
        public async Task GetOrderByStatus_ShouldReturnCorrectCount_WhenOrderWithStatusFound()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            var orderId2 = Guid.NewGuid();
            await AddOrderInProgress(orderId1, 2);
            await AddOrder(orderId2, 2);

            // Act
            var orders = await _orderService
                .GetOrdersByStatusAsync(new Guid(_orderStatusInProgressId));

            // Assert
            Assert.AreEqual(1, orders.Count());
        }
        
        [Test]
        public async Task GetOrdersByStatus_ShouldReturnEmptyCollection_WhenNoOrderWithStatusFound()
        {
            // Arrange ;
            var orderId2 = Guid.NewGuid();
            await AddOrder(orderId2, 2);

            // Act
            var orders = await _orderService
                .GetOrdersByStatusAsync(new Guid(_orderStatusInProgressId));
            var orderByStatusName = await _orderService
                .GetOrdersByStatusAsync("In Progress");

            // Assert
            Assert.IsNotNull(orders);
            Assert.IsEmpty(orders);
            Assert.IsNotNull(orderByStatusName);
            Assert.IsEmpty(orderByStatusName);
        }

        [Test]
        public async Task GetFinishedOrdersProfitForMonthAsync_ShouldReturnZero_WhenNoRelevantOrdersFound()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddOldCompletedOrder(orderId1, 1);
            await AddOrderInProgress(Guid.NewGuid(), 1);
            
            // Act
            var profit = await _orderService.GetFinishedOrdersProfitForMonthAsync();
            
            // Assert
            Assert.AreEqual((decimal)0, profit);
        }
        
        [Test]
        public async Task GetFinishedOrdersProfitForMonthAsync_ShouldReturnCorrectProfit_ForRelevantOrders()
        {
            // Arrange
            var orderId1 = Guid.NewGuid();
            await AddRelevantCompletedOrder(orderId1, 2);
            
            // Act
            var profit = await _orderService.GetFinishedOrdersProfitForMonthAsync();
            
            // Assert
            Assert.AreEqual(0.2m, profit);
        }

        [Test]
        public async Task CreateOrderAsync_ShouldReturnOrderId_WhenOrderCreatedSuccessfully()
        {
            // Arrange
            var orderCreateRequest = new CreateOrderRequest
            {
                CustomerId = Guid.NewGuid(),
                ResellerId = Guid.NewGuid(),
                Items = new List<CreateOrderItemRequest>
                {
                    new()
                    {
                        ProductId = new Guid(_orderProductEmailId),
                        Quantity = 1
                    }
                }
            };
            
            // Act
            var orderIdResult = await _orderService.CreateOrderAsync(orderCreateRequest);
            
            // Assert
            Assert.NotNull(orderIdResult);
            Assert.AreNotEqual(orderIdResult, Guid.Empty);
            Assert.IsNotNull(orderIdResult);
        }

        [Test] public async Task CreateOrderAsync_ShouldThrow_WhenProductDoesntExist()
        {
            // Arrange
            var orderCreateRequest = new CreateOrderRequest
            {
                CustomerId = Guid.NewGuid(),
                ResellerId = Guid.NewGuid(),
                Items = new List<CreateOrderItemRequest>
                {
                    new()
                    {
                        ProductId = Guid.NewGuid(),
                        Quantity = 1
                    }
                }
            };
            
            // Act, Assert
            Assert.ThrowsAsync<ProductNotFoundException>(
                async () => await _orderService.CreateOrderAsync(orderCreateRequest));
        }
        
        [Test] public async Task UpdateOrderStatus_ShouldReturnUpdated_WhenOrderUpdated()
        {
            // Arrange
            var updateRequest = new UpdateOrderStatusRequest(null, "Completed");
            var orderId = Guid.NewGuid();
            await AddOrder(orderId, 1);
            
            // Act
            var result = await _orderService.UpdateOrderStatusAsync(orderId, updateRequest);
            
            // Act, Assert
            Assert.AreEqual(result, StatusUpdateResult.Updated);
        }
        
        [Test] public async Task UpdateOrderStatus_ShouldReturnNoChange_IfStatusWasTheSame()
        {
            // Arrange
            var updateRequest = new UpdateOrderStatusRequest(null, "Completed");
            var orderId = Guid.NewGuid();
            await AddRelevantCompletedOrder(orderId, 1);
            
            // Act
            var result = await _orderService.UpdateOrderStatusAsync(orderId, updateRequest);
            
            // Act, Assert
            Assert.AreEqual(result, StatusUpdateResult.NoChange);
        }
        
        [Test] public async Task UpdateOrderStatus_ShouldReturnNotFound_IfOrderNotFound()
        {
            // Arrange
            var updateRequest = new UpdateOrderStatusRequest(null, "Completed");
            var orderId = Guid.NewGuid();
            await AddRelevantCompletedOrder(orderId, 1);
            
            // Act
            var result = await _orderService.UpdateOrderStatusAsync(Guid.NewGuid(), updateRequest);
            
            // Act, Assert
            Assert.AreEqual(result, StatusUpdateResult.NotFound);
        }
        
        [Test] public async Task UpdateOrderStatus_ShouldReturnInvalidStatus_IfStatusNotFound()
        {
            // Arrange
            var updateRequest = new UpdateOrderStatusRequest(null, "asdadad");
            var updateRequest2 = new UpdateOrderStatusRequest(Guid.NewGuid(), null);
            var orderId = Guid.NewGuid();
            await AddRelevantCompletedOrder(orderId, 1);
            
            // Act
            var result = await _orderService.UpdateOrderStatusAsync(orderId, updateRequest);
            var result2 = await _orderService.UpdateOrderStatusAsync(orderId, updateRequest2);
            
            // Act, Assert
            Assert.AreEqual(result, StatusUpdateResult.InvalidStatus);
            Assert.AreEqual(result2, StatusUpdateResult.InvalidStatus);
        }
        
        [Test] public async Task UpdateOrderStatus_ShouldReturnStatusNotFound_IfNoStatusesSpecified()
        {
            // Arrange
            var updateRequest = new UpdateOrderStatusRequest(null, null);
            var orderId = Guid.NewGuid();
            await AddRelevantCompletedOrder(orderId, 1);
            
            // Act
            var result = await _orderService.UpdateOrderStatusAsync(orderId, updateRequest);
            
            // Act, Assert
            Assert.AreEqual(result, StatusUpdateResult.InvalidStatus);
        }
        
        private async Task AddOrder(Guid orderId, int quantity)
        {
            var orderIdBytes = orderId.ToByteArray();
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = orderIdBytes,
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = DateTime.Now,
                StatusId = _orderStatusCreatedId,
            });

            _orderContext.OrderItem.Add(new OrderItem
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderIdBytes,
                ServiceId = _orderServiceEmailId,
                ProductId = _orderProductEmailId,
                Quantity = quantity
            });

            await _orderContext.SaveChangesAsync();
        }
        
        private async Task AddRelevantCompletedOrder(Guid orderId, int quantity)
        {
            var orderIdBytes = orderId.ToByteArray();
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = orderIdBytes,
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = DateTime.Now.AddDays(-1),
                StatusId = _orderStatusCompleted,
            });

            _orderContext.OrderItem.Add(new OrderItem
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderIdBytes,
                ServiceId = _orderServiceEmailId,
                ProductId = _orderProductEmailId,
                Quantity = quantity
            });

            await _orderContext.SaveChangesAsync();
        }
        
        private async Task AddOldCompletedOrder(Guid orderId, int quantity)
        {
            var orderIdBytes = orderId.ToByteArray();
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = orderIdBytes,
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = DateTime.Now.AddMonths(-1),
                StatusId = _orderStatusCompleted,
            });

            _orderContext.OrderItem.Add(new OrderItem
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderIdBytes,
                ServiceId = _orderServiceEmailId,
                ProductId = _orderProductEmailId,
                Quantity = quantity
            });

            await _orderContext.SaveChangesAsync();
        }

        private async Task AddOrderInProgress(Guid orderId, int quantity)
        {
            var orderIdBytes = orderId.ToByteArray();
            _orderContext.Order.Add(new Data.Entities.Order
            {
                Id = orderIdBytes,
                ResellerId = Guid.NewGuid().ToByteArray(),
                CustomerId = Guid.NewGuid().ToByteArray(),
                CreatedDate = DateTime.Now,
                StatusId = _orderStatusInProgressId,
            });

            _orderContext.OrderItem.Add(new OrderItem
            {
                Id = Guid.NewGuid().ToByteArray(),
                OrderId = orderIdBytes,
                ServiceId = _orderServiceEmailId,
                ProductId = _orderProductEmailId,
                Quantity = quantity
            });

            await _orderContext.SaveChangesAsync();
        }
        
        private async Task AddReferenceDataAsync(OrderContext orderContext)
        {
            orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = _orderStatusCreatedId,
                Name = "Created",
            });

            orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = _orderStatusInProgressId,
                Name = "In Progress",
            });
            
            orderContext.OrderStatus.Add(new OrderStatus
            {
                Id = _orderStatusCompleted,
                Name = "Completed",
            });
            
            orderContext.OrderService.Add(new Data.Entities.OrderService
            {
                Id = _orderServiceEmailId,
                Name = "Email"
            });

            orderContext.OrderProduct.Add(new OrderProduct
            {
                Id = _orderProductEmailId,
                Name = "100GB Mailbox",
                UnitCost = 0.8m,
                UnitPrice = 0.9m,
                ServiceId = _orderServiceEmailId
            });

            await orderContext.SaveChangesAsync();
        }
    }
}
