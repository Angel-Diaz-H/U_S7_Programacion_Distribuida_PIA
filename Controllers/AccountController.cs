using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using TuProyecto.Models;
using System;
using System.Globalization;
using System.Text.Encodings.Web;

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

        // 🔹 Método para cargar usuarios
        private List<UserModel> LoadUsers()
        {
            var filePath = Path.Combine(_env.WebRootPath ?? string.Empty, "data", "users.json");
            if (!System.IO.File.Exists(filePath))
                return new List<UserModel>();

            var json = System.IO.File.ReadAllText(filePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<UserModel>>(json, options) ?? new List<UserModel>();
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
                HttpContext.Session.SetString("LoggedInDisplayName", user.DisplayName ?? user.Username);

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
        public IActionResult Register(string username, string email, string password, string confirmPassword,
                                      string firstName, string lastName, string middleName, string dateOfBirth)
        {
            // Basic required fields
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(email)
                || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(dateOfBirth))
            {
                ViewBag.ErrorMessage = "Todos los campos marcados son obligatorios.";
                return View("~/Views/Home/CrearCuenta.cshtml");
            }

            // Password confirmation
            if (password != confirmPassword)
            {
                ViewBag.ErrorMessage = "Las contraseñas no coinciden.";
                return View("~/Views/Home/CrearCuenta.cshtml");
            }

            // Validate date of birth: parse and ensure between 1900 and 2013 and at least 12 years old
            DateOnly dob;
            if (!DateOnly.TryParseExact(dateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out dob))
            {
                // Try general parse (in case browser sends different format)
                if (!DateOnly.TryParse(dateOfBirth, CultureInfo.InvariantCulture, DateTimeStyles.None, out dob))
                {
                    ViewBag.ErrorMessage = "Fecha de nacimiento inválida.";
                    return View("~/Views/Home/CrearCuenta.cshtml");
                }
            }

            // Reject obviously wrong years and future dates
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (dob.Year < 1900 || dob.Year > 2013)
            {
                ViewBag.ErrorMessage = "La fecha de nacimiento debe estar entre 1900 y 2013.";
                return View("~/Views/Home/CrearCuenta.cshtml");
            }
            if (dob > today)
            {
                ViewBag.ErrorMessage = "Fecha de nacimiento no puede ser en el futuro.";
                return View("~/Views/Home/CrearCuenta.cshtml");
            }

            var minAllowed = today.AddYears(-12);
            if (dob > minAllowed)
            {
                ViewBag.ErrorMessage = "Debes tener al menos 12 años para crear una cuenta.";
                return View("~/Views/Home/CrearCuenta.cshtml");
            }

            // Ruta al JSON dentro de wwwroot/data
            var filePath = Path.Combine(_env.WebRootPath ?? string.Empty, "data", "users.json");
            List<UserModel> users = new List<UserModel>();

            if (System.IO.File.Exists(filePath))
            {
                var json = System.IO.File.ReadAllText(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                users = JsonSerializer.Deserialize<List<UserModel>>(json, options) ?? new List<UserModel>();
            }

            if (users.Exists(u => string.Equals((u.Username ?? string.Empty).Trim(), (username ?? string.Empty).Trim(), System.StringComparison.OrdinalIgnoreCase)))
            {
                ViewBag.ErrorMessage = "El usuario ya existe.";
                return View("~/Views/Home/CrearCuenta.cshtml");
            }

            var newUser = new UserModel
            {
                Username = username.Trim(),
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                Password = password.Trim(),
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim(),
                DateOfBirth = dob.ToString("yyyy-MM-dd")
            };

            users.Add(newUser);

            // Serialize using relaxed encoder to preserve accents instead of \u escapes
            var writeOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            var updatedJson = JsonSerializer.Serialize(users, writeOptions);

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