using System;
using System.Collections.Generic;
using Order.Data.Exceptions;
using Order.Model;

namespace Order.WebAPI.Validators;

public sealed class CreateOrderValidator() : IValidator<CreateOrderRequest>
{
    public void ValidateAndThrow(CreateOrderRequest orderRequest)
    {
        var errors = new List<ValidationError>();
        
        if (orderRequest is null)
        {
            errors.Add(new ValidationError("", "Order request is null"));
            throw new ValidationException(errors);
        }
        
        if (orderRequest.Items is null || orderRequest.Items.Count == 0)
        {
            errors.Add(new ValidationError(nameof(orderRequest.Items),
                "Order items are null or empty"));
        }

        if (orderRequest.CustomerId == Guid.Empty)
        {
            errors.Add(new ValidationError(nameof(orderRequest.CustomerId),
                "Customer id is empty"));
        }
        
        if (orderRequest.ResellerId == Guid.Empty)
        {
            errors.Add(new ValidationError(nameof(orderRequest.ResellerId),
                "Reseller id is empty"));
        }

        if (orderRequest.Items is not null)
        {
            foreach (var orderItem in orderRequest.Items)
            {
                if (orderItem.ProductId == Guid.Empty)
                {
                    errors.Add(new ValidationError(nameof(orderItem.ProductId),
                        "Product id is empty"));
                }
                
                if (orderItem.Quantity <= 0)
                {
                    errors.Add(new ValidationError(nameof(orderItem.Quantity),
                        "Quantity must be greater than zero"));
                }

                if (orderItem.Quantity > 250)
                {
                    errors.Add(new ValidationError(nameof(orderItem.Quantity),
                        "Quantity must be less than or equal to 250"));
                }
            }
        }
        
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }
}