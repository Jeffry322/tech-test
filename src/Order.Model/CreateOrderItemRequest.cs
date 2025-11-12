using System;
using System.ComponentModel.DataAnnotations;

namespace Order.Model;

public sealed class CreateOrderItemRequest
{
    public required Guid ProductId { get; init; }
    public required int Quantity { get; init; } 
}