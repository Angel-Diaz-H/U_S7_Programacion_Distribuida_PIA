using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

namespace LoginJsonDemo.Controllers
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
            return JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
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
            var users = LoadUsers();
            var user = users.Find(u => u.Username == username && u.Password == password);

            if (user != null)
            {
                // opcional: guardar usuario y correo en sesión
                HttpContext.Session.SetString("LoggedInUser", user.Username);
                HttpContext.Session.SetString("LoggedInEmail", user.Email);

                return RedirectToAction("Index", "Home");
            }

            ViewBag.ErrorMessage = "Usuario o contraseña incorrectos.";
            return View();
        }

        // 🔹 GET CrearCuenta
        [HttpGet]
        public IActionResult CrearCuenta()
        {
            return View();
        }

        // 🔹 POST Register
        [HttpPost]
        public IActionResult Register(string username, string email, string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email))
            {
                ViewBag.ErrorMessage = "Todos los campos son obligatorios.";
                return View("CrearCuenta");
            }

            if (password != confirmPassword)
            {
                ViewBag.ErrorMessage = "Las contraseñas no coinciden.";
                return View("CrearCuenta");
            }

            // Ruta al JSON dentro de wwwroot/data
            var filePath = Path.Combine(_env.WebRootPath ?? string.Empty, "data", "users.json");
            List<User> users = new List<User>();

            if (System.IO.File.Exists(filePath))
            {
                var json = System.IO.File.ReadAllText(filePath);
                users = JsonSerializer.Deserialize<List<User>>(json) ?? new List<User>();
            }

            if (users.Exists(u => u.Username == username))
            {
                ViewBag.ErrorMessage = "El usuario ya existe.";
                return View("CrearCuenta");
            }

            users.Add(new User { Username = username, Email = email, Password = password });

            var updatedJson = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, updatedJson);

            TempData["SuccessMessage"] = "Cuenta creada exitosamente. Ahora puedes iniciar sesión.";
            return RedirectToAction("IniciarSesion", "Home");
        }
    }
}