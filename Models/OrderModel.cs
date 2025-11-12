using System;

namespace TuProyecto.Models
{
    public class OrderModel
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty; // yyyy-MM-dd
        public string Hour { get; set; } = string.Empty; // HH:00
        public int Persons { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        // status: active, cancelled, completed
        public string Status { get; set; } = "active";
    }
}
