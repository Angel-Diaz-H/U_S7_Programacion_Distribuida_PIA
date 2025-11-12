using System.Collections.Generic;

namespace TuProyecto.Models
{
    public class OrderListViewModel
    {
        public List<OrderModel> ActiveOrders { get; set; } = new List<OrderModel>();
        public List<OrderModel> PastOrders { get; set; } = new List<OrderModel>();
    }
}
