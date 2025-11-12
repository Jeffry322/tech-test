using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Order.Model;

public sealed class CreateOrderRequest
{
    public required Guid ResellerId { get; init; }
    public required Guid CustomerId { get; init; }
    // Orders creating with status "Created"
    // Fist time testing I was using status Id, and was just assigning it
    // to order status Id. But everytime I restart database same status would have different Id
    // so I change it to status name and fetch status from DB by it's name
    public string StatusName { get; } = "Created";
    
    public ICollection<CreateOrderItemRequest> Items { get; set; } = [];
}