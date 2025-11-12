using System;

namespace Order.Data.Exceptions;

public sealed class ProductNotFoundException : ApplicationException
{
    public ProductNotFoundException(Guid productId)
        :base($"Product with ID {productId} not found.")
    {
        
    }
}