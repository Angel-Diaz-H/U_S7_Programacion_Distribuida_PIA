using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;
using System.Text;

namespace TuProyecto.Controllers
{
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _env;

        // Simple in-memory branches list used by Sucursales/Ordenar
        private static readonly List<BranchModel> _branches = new()
        {
            new BranchModel{ Id=1, StateKey="nuevoleon", Name="Bowlly's San Pedro" },
            new BranchModel{ Id=2, StateKey="nuevoleon", Name="Bowlly's Monterrey Centro" },
            new BranchModel{ Id=3, StateKey="jalisco", Name="Bowlly's Zapopan" },
            new BranchModel{ Id=4, StateKey="chihuahua", Name="Bowlly's Chihuahua Centro" },
            new BranchModel{ Id=5, StateKey="coahuila", Name="Bowlly's Saltillo Centro" },
            new BranchModel{ Id=6, StateKey="queretaro", Name="Bowlly's Querétaro Centro" },
            new BranchModel{ Id=7, StateKey="tamaulipas", Name="Bowlly's Tampico" },
            new BranchModel{ Id=8, StateKey="bajacalifornia", Name="Bowlly's Tijuana Centro" },
            new BranchModel{ Id=9, StateKey="veracruz", Name="Bowlly's Buenavista" },
            new BranchModel{ Id=10, StateKey="quintanaroo", Name="Bowlly's Cancún Centro" }
        };

        public HomeController(IWebHostEnvironment env)
        {
            _env = env;
        }

        // Página principal
        public IActionResult Index()
        {
            return View();
        }

        // Página de Iniciar Sesión (GET)
        [HttpGet]
        public IActionResult IniciarSesion()
        {
            return View();
        }

        [HttpPost]
        public IActionResult IniciarSesion(string username, string password)
        {
            try
            {
                // Ruta al JSON de usuarios dentro de wwwroot/data/users.json
                var dataPath = System.IO.Path.Combine(_env.WebRootPath ?? string.Empty, "data", "users.json");
                if (!System.IO.File.Exists(dataPath))
                {
                    ViewBag.ErrorMessage = "No se encontró el archivo de usuarios.";
                    return View();
                }

                // Leer el archivo intentando UTF-8 y hacer fallback a la codificación por defecto del sistema si es necesario
                string json = string.Empty;
                var bytes = System.IO.File.ReadAllBytes(dataPath);
                try
                {
                    json = Encoding.UTF8.GetString(bytes);
                    // Quick check: if replacement character found, try fallback
                    if (json.Contains("?"))
                    {
                        json = Encoding.Default.GetString(bytes);
                    }
                }
                catch
                {
                    json = Encoding.Default.GetString(bytes);
                }

                var users = JsonSerializer.Deserialize<List<UserModel>>(json) ?? new List<UserModel>();

                var user = users.FirstOrDefault(u => u.Username == username && u.Password == password);
                if (user != null)
                {
                    // Store basic user info in session for demo purposes
                    HttpContext.Session.SetString("LoggedInUser", user.Username);
                    HttpContext.Session.SetString("LoggedInDisplayName", user.DisplayName ?? user.Username);

                    TempData["SuccessMessage"] = $"Bienvenido, {user.DisplayName ?? user.Username}";
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ViewBag.ErrorMessage = "Usuario o contraseña incorrectos";
                }
            }
            catch (System.Exception ex)
            {
                ViewBag.ErrorMessage = "Error al validar el usuario.";
            }

            return View();
        }

        public IActionResult Sucursales()
        {
            return View();
        }

        public IActionResult Contacto()
        {
            return View();
        }

        // Acción para la vista Nosotros
        public IActionResult Nosotros()
        {
            return View();
        }

        // Acción para la vista Reto (Healthy Challenge)
        public IActionResult Reto()
        {
            return View();
        }

        // GET: Ordenar (form)
        [HttpGet]
        public IActionResult Ordenar()
        {
            var username = HttpContext.Session.GetString("LoggedInUser");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("IniciarSesion");
            }

            // Provide a lightweight list of branches (anonymous objects) so the view can treat them as dynamic
            ViewData["Branches"] = _branches.Select(b => new { b.Id, b.Name }).ToList();
            // hours from 09 to 20
            ViewData["Hours"] = Enumerable.Range(9, 12).Select(h => h.ToString("D2") + ":00").ToList();
            return View();
        }

        // POST: Ordenar (submit)
        [HttpPost]
        public IActionResult Ordenar(int branchId, string date, string hour, int persons = 1, string notes = "")
        {
            var username = HttpContext.Session.GetString("LoggedInUser");
            var display = HttpContext.Session.GetString("LoggedInDisplayName");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("IniciarSesion");
            }

            // validate inputs
            if (branchId <= 0 || string.IsNullOrEmpty(date) || string.IsNullOrEmpty(hour))
            {
                TempData["OrderError"] = "Por favor completa sucursal, fecha y hora.";
                return RedirectToAction("Ordenar");
            }

            // parse date
            DateOnly reservationDate;
            try
            {
                reservationDate = DateOnly.Parse(date);
            }
            catch
            {
                TempData["OrderError"] = "Fecha inválida.";
                return RedirectToAction("Ordenar");
            }

            // server-side check: date must not be before today
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (reservationDate < today)
            {
                TempData["OrderError"] = "No puedes reservar en una fecha anterior al día de hoy.";
                return RedirectToAction("Ordenar");
            }

            // ensure hour format HH:00
            if (!System.Text.RegularExpressions.Regex.IsMatch(hour, "^\\d{2}:00$"))
            {
                TempData["OrderError"] = "Hora inválida.";
                return RedirectToAction("Ordenar");
            }

            // optional: prevent booking in the past (compare full DateTime)
            if (int.TryParse(hour.Substring(0, 2), out var hourInt))
            {
                var reservationDateTime = new DateTime(reservationDate.Year, reservationDate.Month, reservationDate.Day, hourInt, 0, 0);
                if (reservationDateTime < DateTime.Now)
                {
                    TempData["OrderError"] = "No puedes reservar en una fecha u hora pasadas.";
                    return RedirectToAction("Ordenar");
                }
            }

            // Load existing orders
            var ordersPath = System.IO.Path.Combine(_env.WebRootPath ?? string.Empty, "data", "orders.json");
            List<OrderModel> orders = new();
            if (System.IO.File.Exists(ordersPath))
            {
                try
                {
                    var bytes = System.IO.File.ReadAllBytes(ordersPath);
                    var json = Encoding.UTF8.GetString(bytes);
                    orders = JsonSerializer.Deserialize<List<OrderModel>>(json) ?? new List<OrderModel>();
                }
                catch
                {
                    // ignore and start empty
                    orders = new List<OrderModel>();
                }
            }

            // Rule: user cannot reserve more than once on the same date
            var existingSameDay = orders.Any(o => string.Equals(o.Username, username, StringComparison.OrdinalIgnoreCase)
                                                  && o.Date == reservationDate.ToString("yyyy-MM-dd"));
            if (existingSameDay)
            {
                TempData["OrderError"] = "Ya tienes una reservación para esa fecha. No puedes reservar más de una vez por día.";
                return RedirectToAction("Ordenar");
            }

            // Rule: user may have at most 3 active/future reservation dates
            var todayStr = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
            var futureReservations = orders.Where(o => string.Equals(o.Username, username, StringComparison.OrdinalIgnoreCase)
                                                      && String.Compare(o.Date, todayStr) >= 0)
                                           .Select(o => o.Date)
                                           .Distinct()
                                           .Count();
            if (futureReservations >= 3)
            {
                TempData["OrderError"] = "Has alcanzado el límite de 3 reservaciones activas. Cancela alguna para crear una nueva.";
                return RedirectToAction("Ordenar");
            }

            // Count reservations for same date and hour globally
            var sameSlotCount = orders.Count(o => o.Date == reservationDate.ToString("yyyy-MM-dd") && o.Hour == hour);
            if (sameSlotCount >= 10)
            {
                TempData["OrderError"] = "Lo sentimos, ya se alcanzó el límite de 10 reservaciones para esa hora.";
                return RedirectToAction("Ordenar");
            }

            var branch = _branches.FirstOrDefault(b => b.Id == branchId);
            if (branch == null)
            {
                TempData["OrderError"] = "Sucursal no válida.";
                return RedirectToAction("Ordenar");
            }

            var newOrder = new OrderModel
            {
                Id = (orders.Count > 0) ? orders.Max(o => o.Id) + 1 : 1,
                Username = username,
                DisplayName = display,
                BranchId = branch.Id,
                BranchName = branch.Name,
                Date = reservationDate.ToString("yyyy-MM-dd"),
                Hour = hour,
                Persons = persons,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };

            orders.Add(newOrder);

            // save file (ensure directory)
            try
            {
                var dir = System.IO.Path.GetDirectoryName(ordersPath) ?? string.Empty;
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                var write = JsonSerializer.Serialize(orders, new JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(ordersPath, write, Encoding.UTF8);
            }
            catch
            {
                TempData["OrderError"] = "Error al guardar la orden.";
                return RedirectToAction("Ordenar");
            }

            // success - show modal on GET
            TempData["OrderSuccess"] = "Reservación confirmada";
            TempData["OrderInfo"] = JsonSerializer.Serialize(new { newOrder.Id, newOrder.BranchName, newOrder.Date, newOrder.Hour, newOrder.Persons });

            return RedirectToAction("Ordenar");
        }

        // Demo account page showing session user info
        public IActionResult MiCuenta()
        {
            var username = HttpContext.Session.GetString("LoggedInUser");
            var display = HttpContext.Session.GetString("LoggedInDisplayName");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("IniciarSesion");
            }

            ViewData["Username"] = username;
            ViewData["DisplayName"] = display;
            return View();
        }

        // Logout demo
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("LoggedInUser");
            HttpContext.Session.Remove("LoggedInDisplayName");
            return RedirectToAction("Index");
        }

        // Acción para obtener disponibilidad por fecha y sucursal
        [HttpGet]
        public IActionResult GetAvailability(string date, int branchId = 0)
        {
            if (string.IsNullOrEmpty(date)) return Json(new { success = false, message = "Fecha requerida" });

            var ordersPath = System.IO.Path.Combine(_env.WebRootPath ?? string.Empty, "data", "orders.json");
            List<OrderModel> orders = new();
            if (System.IO.File.Exists(ordersPath))
            {
                try
                {
                    var bytes = System.IO.File.ReadAllBytes(ordersPath);
                    var json = Encoding.UTF8.GetString(bytes);
                    orders = JsonSerializer.Deserialize<List<OrderModel>>(json) ?? new List<OrderModel>();
                }
                catch
                {
                    orders = new List<OrderModel>();
                }
            }

            var result = new Dictionary<string, int>();
            // hours from 09:00 to 20:00 (same as ViewData Hours)
            for (int h = 9; h < 21; h++)
            {
                var hour = h.ToString("D2") + ":00";
                var count = orders.Count(o => o.Date == date && o.Hour == hour);
                var remaining = Math.Max(0, 10 - count);
                result[hour] = remaining;
            }

            return Json(new { success = true, availability = result });
        }
    }

    // Simple model for deserializing users.json
    public class UserModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
    }

    public class BranchModel
    {
        public int Id { get; set; }
        public string StateKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

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
    }
}
