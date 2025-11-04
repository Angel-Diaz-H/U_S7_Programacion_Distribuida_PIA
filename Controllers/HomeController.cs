using Microsoft.AspNetCore.Mvc;

namespace TuProyecto.Controllers
{
    public class HomeController : Controller
    {
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
    
            string userValido = "claudia";
            string passValida = "1234";

            if (username == userValido && password == passValida)
            {
            
                ViewBag.SuccessMessage = $"Bienvenido, {username}";
                return RedirectToAction("Index", "Home");
            }
            else
            {
          
                ViewBag.ErrorMessage = "Usuario o contraseña incorrectos";
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
    }
}
