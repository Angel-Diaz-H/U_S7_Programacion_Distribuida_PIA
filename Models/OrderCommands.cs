namespace TuProyecto.Models
{
    public class IdDto
    {
        public int Id { get; set; }
    }

    public class EditOrderDto
    {
        public int Id { get; set; }
        public string? Hour { get; set; }
        public int Persons { get; set; }
        public string? Notes { get; set; }
    }
}
