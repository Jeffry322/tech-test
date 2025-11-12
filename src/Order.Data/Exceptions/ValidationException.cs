using System;
using System.Collections.Generic;
using System.Linq;
using Order.Model;

namespace Order.Data.Exceptions;

public sealed class ValidationException : Exception
{
    public IReadOnlyList<ValidationError> Errors { get; set; } = [];

    public ValidationException(IEnumerable<ValidationError> errors)
        :base("One or more validation failures have occurred.")
    {
        Errors = errors.ToArray();
    }
}