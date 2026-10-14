using Cafe.Api.Models.Entities;
using Cafe.Api.Models.Enums;
namespace Cafe.Api.Services.Orders;

public static class OrderQueries
{
    /// <summary>Counter orders show immediately; online orders only once paid.</summary>
    public static IQueryable<Order> VisibleToKitchen(this IQueryable<Order> q) =>
        q.Where(o => o.PaymentMethod == PaymentMethod.PayAtCounter
                  || o.PaymentStatus == PaymentStatus.Paid);
}