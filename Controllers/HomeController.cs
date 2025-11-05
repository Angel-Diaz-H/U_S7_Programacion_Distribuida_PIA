using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.Text.Json;

namespace TuProyecto.Controllers
{
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _env;

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

                var json = System.IO.File.ReadAllText(dataPath);
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
    }

    // Simple model for deserializing users.json
    public class UserModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? Email { get; set; }
    }
}
