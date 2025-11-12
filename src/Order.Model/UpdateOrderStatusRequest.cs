#nullable enable
using System;

namespace Order.Model;

public record UpdateOrderStatusRequest(Guid? NewStatusId, string? NewStatusName);