using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace TuProyecto.Controllers
{
    public class AccountController : Controller
    {
        // 🔹 Variable de entorno para ubicar archivos
        private readonly IWebHostEnvironment _env;

        // 🔹 Constructor para inyectar el entorno
        public AccountController(IWebHostEnvironment env)
        {
            _env = env;
        }

        // 🔹 Clase interna User con correo
        private class User
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string Email { get; set; } // nuevo campo
        }

        // 🔹 Método para cargar usuarios
        private List<User> LoadUsers()
        {
            var filePath = Path.Combine(_env.WebRootPath ?? string.Empty, "data", "users.json");
            if (!System.IO.File.Exists(filePath))
                return new List<User>();

            var json = System.IO.File.ReadAllText(filePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<User>>(json, options) ?? new List<User>();
        }

        // 🔹 GET Login
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // 🔹 POST Login
        [HttpPost]
        public IActionResult Login(string username, string password)
        {
            var inputUser = (username ?? string.Empty).Trim();
            var inputPass = (password ?? string.Empty).Trim();

            var users = LoadUsers();
            var user = users.Find(u => string.Equals((u.Username ?? string.Empty).Trim(), inputUser, System.StringComparison.OrdinalIgnoreCase)
                                     && string.Equals((u.Password ?? string.Empty).Trim(), inputPass, System.StringComparison.Ordinal));

            if (user != null)
            {
                // opcional: guardar usuario y correo en sesión
                HttpContext.Session.SetString("LoggedInUser", user.Username);
                HttpContext.Session.SetString("LoggedInEmail", user.Email ?? string.Empty);

                return RedirectToAction("Index", "Home");
            }

            ViewBag.ErrorMessage = "Usuario o contraseña incorrectos.";
            return View();
        }

        // 🔹 GET CrearCuenta - render the view placed under Views/Home
        [HttpGet]
        public IActionResult CrearCuenta()
        {
            // Only show messages specifically about registration
            if (TempData.ContainsKey("RegistrationSuccess")) ViewBag.SuccessMessage = TempData["RegistrationSuccess"];
            if (TempData.ContainsKey("RegistrationError")) ViewBag.ErrorMessage = TempData["RegistrationError"];

            return View("~/Views/Home/CrearCuenta.cshtml");
        }

        // 🔹 POST Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(string username, string email, string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
            {
                ViewBag.ErrorMessage = "Todos los campos son obligatorios.";
                return View("~/Views/Home/CrearCuenta.cshtml");
            }

            if (password != confirmPassword)
            {
                ViewBag.ErrorMessage = "Las contraseñas no coinciden.";
                return View("~/Views/Home/CrearCuenta.cshtml");
            }

            // Ruta al JSON dentro de wwwroot/data
            var filePath = Path.Combine(_env.WebRootPath ?? string.Empty, "data", "users.json");
            List<User> users = new List<User>();

            if (System.IO.File.Exists(filePath))
            {
                var json = System.IO.File.ReadAllText(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                users = JsonSerializer.Deserialize<List<User>>(json, options) ?? new List<User>();
            }

            if (users.Exists(u => string.Equals((u.Username ?? string.Empty).Trim(), (username ?? string.Empty).Trim(), System.StringComparison.OrdinalIgnoreCase)))
            {
                ViewBag.ErrorMessage = "El usuario ya existe.";
                return View("~/Views/Home/CrearCuenta.cshtml");
            }

            users.Add(new User { Username = username.Trim(), Email = email.Trim(), Password = password.Trim() });

            var updatedJson = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });

            try
            {
                // Ensure directory exists
                var dir = Path.GetDirectoryName(filePath) ?? string.Empty;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                System.IO.File.WriteAllText(filePath, updatedJson, System.Text.Encoding.UTF8);
            }
            catch
            {
                // Use a specific registration error key so it doesn't clash with other success messages
                TempData["RegistrationError"] = "Error al guardar el usuario.";
                return RedirectToAction("CrearCuenta");
            }

            TempData["RegistrationSuccess"] = "Cuenta creada exitosamente. Ahora puedes iniciar sesión.";
            return RedirectToAction("IniciarSesion", "Home");
        }
    }
}