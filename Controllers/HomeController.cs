using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using System.Text;
using System.Text.Json;
using TuProyecto.Models;

namespace TuProyecto.Controllers
{
    public partial class HomeController : Controller
    {
        private readonly IWebHostEnvironment _env;

        private static readonly object _ordersLock = new object();

        // Simple in-memory branches list used by Sucursales/Ordenar
        private static readonly List<BranchModel> _branches = new()
        {
            // Nuevo León
            new BranchModel{ Id=1, StateKey="nuevoleon", Name="Bowlly's San Pedro" },
            new BranchModel{ Id=2, StateKey="nuevoleon", Name="Bowlly's Monterrey Centro" },
            new BranchModel{ Id=11, StateKey="nuevoleon", Name="Bowlly's Valle Oriente" },

            // Jalisco
            new BranchModel{ Id=3, StateKey="jalisco", Name="Bowlly's Zapopan" },
            new BranchModel{ Id=12, StateKey="jalisco", Name="Bowlly's Guadalajara Centro" },

            // Chihuahua
            new BranchModel{ Id=4, StateKey="chihuahua", Name="Bowlly's Chihuahua Centro" },
            new BranchModel{ Id=13, StateKey="chihuahua", Name="Bowlly's Norte" },

            // Coahuila
            new BranchModel{ Id=5, StateKey="coahuila", Name="Bowlly's Saltillo Centro" },
            new BranchModel{ Id=14, StateKey="coahuila", Name="Bowlly's Ramos Arizpe" },

            // Querétaro
            new BranchModel{ Id=6, StateKey="queretaro", Name="Bowlly's Querétaro Centro" },
            new BranchModel{ Id=15, StateKey="queretaro", Name="Bowlly's Juriquilla" },

            // Tamaulipas
            new BranchModel{ Id=7, StateKey="tamaulipas", Name="Bowlly's Tampico" },
            new BranchModel{ Id=16, StateKey="tamaulipas", Name="Bowlly's Madero" },

            // Baja California
            new BranchModel{ Id=8, StateKey="bajacalifornia", Name="Bowlly's Tijuana Centro" },
            new BranchModel{ Id=17, StateKey="bajacalifornia", Name="Bowlly's Rosarito" },

            // Veracruz
            new BranchModel{ Id=9, StateKey="veracruz", Name="Bowlly's Buenavista" },
            new BranchModel{ Id=18, StateKey="veracruz", Name="Bowlly's Boca del Río" },

            // Quintana Roo
            new BranchModel{ Id=10, StateKey="quintanaroo", Name="Bowlly's Cancún Centro" },
            new BranchModel{ Id=19, StateKey="quintanaroo", Name="Bowlly's Playa del Carmen" }
        };

        public HomeController(IWebHostEnvironment env)
        {
            _env = env;
        }

        private string GetOrdersPath() => Path.Combine(_env.WebRootPath ?? string.Empty, "data", "orders.json");
        private string GetOrdersDebugPath() => Path.Combine(_env.WebRootPath ?? string.Empty, "data", "orders_debug.log");

        private void LogOrderActivity(string text)
        {
            try
            {
                var path = GetOrdersDebugPath();
                var dir = Path.GetDirectoryName(path) ?? string.Empty;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var entry = DateTime.UtcNow.ToString("o") + " - " + text + "\n";
                lock (_ordersLock)
                {
                    System.IO.File.AppendAllText(path, entry, Encoding.UTF8);
                }
            }
            catch
            {
                // swallow logging errors
            }
        }

        // Read orders without locking (used for availability); lock when writing
        private List<OrderModel> ReadOrdersSafe()
        {
            var ordersPath = GetOrdersPath();
            try
            {
                if (!System.IO.File.Exists(ordersPath)) return new List<OrderModel>();
                var json = System.IO.File.ReadAllText(ordersPath, Encoding.UTF8);
                var list = JsonSerializer.Deserialize<List<OrderModel>>(json) ?? new List<OrderModel>();

                // normalize missing Status and set default to active
                var needSave = false;
                foreach (var o in list)
                {
                    if (string.IsNullOrWhiteSpace(o.Status))
                    {
                        o.Status = "active";
                        needSave = true;
                    }
                }

                if (needSave)
                {
                    try
                    {
                        var write = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                        System.IO.File.WriteAllText(ordersPath, write, Encoding.UTF8);
                    }
                    catch
                    {
                        // ignore write errors
                    }
                }

                return list;
            }
            catch
            {
                return new List<OrderModel>();
            }
        }

        // Append order atomically using exclusive FileStream to avoid cross-process overwrite
        private bool AppendOrderAtomic(OrderModel newOrder, out string? error)
        {
            error = null;
            var ordersPath = GetOrdersPath();
            var dir = Path.GetDirectoryName(ordersPath) ?? string.Empty;
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                using (var fs = new FileStream(ordersPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    List<OrderModel> orders = new List<OrderModel>();
                    if (fs.Length > 0)
                    {
                        fs.Seek(0, SeekOrigin.Begin);
                        using (var sr = new StreamReader(fs, Encoding.UTF8, leaveOpen: true))
                        {
                            var existing = sr.ReadToEnd();
                            try { orders = JsonSerializer.Deserialize<List<OrderModel>>(existing) ?? new List<OrderModel>(); } catch { orders = new List<OrderModel>(); }
                        }
                    }

                    // normalize username and date for comparisons
                    var usernameNorm = (newOrder.Username ?? string.Empty).Trim();
                    var reservationDateStr = newOrder.Date;

                    // check exact duplicate
                    var duplicateExact = orders.Any(o => string.Equals((o.Username ?? string.Empty).Trim(), usernameNorm, StringComparison.OrdinalIgnoreCase)
                                                         && o.Date == reservationDateStr && o.Hour == newOrder.Hour && o.BranchId == newOrder.BranchId);
                    if (duplicateExact)
                    {
                        error = "Ya existe una reservación idéntica.";
                        LogOrderActivity($"REJECT duplicateExact username={usernameNorm} date={reservationDateStr} hour={newOrder.Hour} branch={newOrder.BranchId}");
                        return false;
                    }

                    // check same day per user (use normalized username)
                    var existingSameDay = orders.Any(o => string.Equals((o.Username ?? string.Empty).Trim(), usernameNorm, StringComparison.OrdinalIgnoreCase)
                                                          && o.Date == reservationDateStr);
                    if (existingSameDay)
                    {
                        error = "Ya tienes una reservación para esa fecha. No puedes reservar más de una vez por día.";
                        LogOrderActivity($"REJECT existingSameDay username={usernameNorm} date={reservationDateStr}");
                        return false;
                    }

                    // check max 3 active distinct future dates
                    var todayStr = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
                    var futureReservations = orders.Where(o => string.Equals((o.Username ?? string.Empty).Trim(), usernameNorm, StringComparison.OrdinalIgnoreCase)
                                                              && String.Compare(o.Date, todayStr) >= 0)
                                                   .Select(o => o.Date)
                                                   .Distinct()
                                                   .Count();
                    LogOrderActivity($"INFO beforeInsert username={usernameNorm} existingOrders={orders.Count} futureDistinctDates={futureReservations}");
                    if (futureReservations >= 3)
                    {
                        error = "Has alcanzado el límite de 3 reservaciones activas. Cancela alguna para crear una nueva.";
                        LogOrderActivity($"REJECT futureLimit username={usernameNorm} futureDistinctDates={futureReservations}");
                        return false;
                    }

                    // check daily persons capacity (max 200)
                    var totalPersonsForDate = orders.Where(o => o.Date == reservationDateStr).Sum(o => o.Persons);
                    if (totalPersonsForDate + newOrder.Persons > 200)
                    {
                        error = "No hay capacidad suficiente para esa fecha (límite diario alcanzado).";
                        LogOrderActivity($"REJECT dailyCapacity date={reservationDateStr} totalPersons={totalPersonsForDate} requested={newOrder.Persons}");
                        return false;
                    }

                    // check slot capacity
                    var sameSlotCount = orders.Count(o => o.Date == reservationDateStr && o.Hour == newOrder.Hour);
                    if (sameSlotCount >= 10)
                    {
                        error = "Lo sentimos, ya se alcanzó el límite de 10 reservaciones para esa hora.";
                        LogOrderActivity($"REJECT slotFull date={reservationDateStr} hour={newOrder.Hour} count={sameSlotCount}");
                        return false;
                    }

                    // assign next id safely
                    var nextId = orders.Any() ? orders.Max(o => o.Id) + 1 : 1;
                    newOrder.Id = nextId;
                    orders.Add(newOrder);

                    // append and write back: overwrite file from start
                    fs.SetLength(0);
                    fs.Seek(0, SeekOrigin.Begin);
                    using (var sw = new StreamWriter(fs, Encoding.UTF8, leaveOpen: true))
                    {
                        var write = JsonSerializer.Serialize(orders, new JsonSerializerOptions { WriteIndented = true });
                        sw.Write(write);
                        sw.Flush();
                    }
                }

                LogOrderActivity($"OK inserted username={newOrder.Username} id={newOrder.Id} date={newOrder.Date} hour={newOrder.Hour} branch={newOrder.BranchId}");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                LogOrderActivity($"ERROR append username={newOrder.Username} ex={ex.Message}");
                return false;
            }
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
            // If user is already logged in redirect to MiCuenta
            var current = HttpContext.Session.GetString("LoggedInUser");
            if (!string.IsNullOrEmpty(current))
            {
                return RedirectToAction("MiCuenta");
            }

            // expose registration success if present
            if (TempData.ContainsKey("RegistrationSuccess")) ViewBag.SuccessMessage = TempData["RegistrationSuccess"];
            if (TempData.ContainsKey("RegistrationError")) ViewBag.ErrorMessage = TempData["RegistrationError"];
            if (TempData.ContainsKey("LoginSuccess")) ViewBag.SuccessMessage = TempData["LoginSuccess"];
            return View();
        }

        private List<UserModel> LoadUsersFromWebRoot(out string? error)
        {
            error = null;
            try
            {
                var dataPath = System.IO.Path.Combine(_env.WebRootPath ?? string.Empty, "data", "users.json");
                if (!System.IO.File.Exists(dataPath))
                {
                    error = "users.json not found";
                    return new List<UserModel>();
                }

                string json;
                try
                {
                    // Attempt simple read with UTF8
                    json = System.IO.File.ReadAllText(dataPath, Encoding.UTF8);
                }
                catch
                {
                    // fallback to default encoding
                    json = System.IO.File.ReadAllText(dataPath, Encoding.Default);
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                List<UserModel> users;
                try
                {
                    users = JsonSerializer.Deserialize<List<UserModel>>(json, options) ?? new List<UserModel>();
                }
                catch (Exception ex)
                {
                    // Try to clean common problematic characters and retry
                    var cleaned = new string(json.Where(c => !char.IsControl(c) || c == '\r' || c == '\n' || c == '\t').ToArray());
                    try
                    {
                        users = JsonSerializer.Deserialize<List<UserModel>>(cleaned, options) ?? new List<UserModel>();
                    }
                    catch (Exception inner)
                    {
                        error = $"Deserialization failed: {ex.Message}; retry failed: {inner.Message}";
                        return new List<UserModel>();
                    }
                }

                // Ensure each user has an integer Id; assign incremental ids if missing (0)
                var needSave = false;
                int maxId = users.Any() ? users.Max(u => u.Id) : 0;
                foreach (var u in users)
                {
                    if (u.Id <= 0)
                    {
                        maxId++;
                        u.Id = maxId;
                        needSave = true;
                    }
                }

                if (needSave)
                {
                    try
                    {
                        var writeOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                        var updated = JsonSerializer.Serialize(users, writeOptions);
                        System.IO.File.WriteAllText(dataPath, updated, Encoding.UTF8);
                    }
                    catch
                    {
                        // ignore save errors here
                    }
                }

                return users;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return new List<UserModel>();
            }
        }

        [HttpPost]
        public IActionResult IniciarSesion(string username, string password)
        {
            try
            {
                // Normalize inputs (trim)
                var inputUser = (username ?? string.Empty).Trim();
                var inputPass = (password ?? string.Empty).Trim();

                var users = LoadUsersFromWebRoot(out var loadError);

                if (!string.IsNullOrEmpty(loadError))
                {
                    ViewBag.ErrorMessage = "Error al cargar usuarios: " + loadError;
                    return View();
                }

                // Find user by username OR email (case-insensitive)
                var found = users.FirstOrDefault(u =>
                    string.Equals((u.Username ?? string.Empty).Trim(), inputUser, StringComparison.OrdinalIgnoreCase)
                    || string.Equals((u.Email ?? string.Empty).Trim(), inputUser, StringComparison.OrdinalIgnoreCase));

                if (found == null)
                {
                    ViewBag.ErrorMessage = $"Usuario no encontrado.";
                    return View();
                }

                // Check password
                if (!string.Equals((found.Password ?? string.Empty).Trim(), inputPass, StringComparison.Ordinal))
                {
                    ViewBag.ErrorMessage = "Contraseña incorrecta.";
                    return View();
                }

                // Success: set both username and numeric id if available
                HttpContext.Session.SetString("LoggedInUser", found.Username);
                HttpContext.Session.SetString("LoggedInDisplayName", found.DisplayName ?? found.Username);
                HttpContext.Session.SetString("LoggedInEmail", found.Email ?? string.Empty);
                if (found.Id > 0)
                {
                    HttpContext.Session.SetInt32("LoggedInUserId", found.Id);
                }

                TempData["LoginSuccess"] = $"Bienvenido, {found.DisplayName ?? found.Username}";
                return RedirectToAction("Index", "Home");
            }
            catch (System.Exception ex)
            {
                ViewBag.ErrorMessage = "Error al validar el usuario: " + ex.Message;
            }

            return View();
        }

        // Helper: smart title case formatting (used for storing FullName)
        private static string ToTitleCaseSmart(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input ?? string.Empty;
            input = input.Trim();
            var lowerWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "de", "la", "las", "del", "los", "y", "e" };
            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p.Length <= 3 && lowerWords.Contains(p.ToLowerInvariant()) && i != 0)
                {
                    parts[i] = p.ToLowerInvariant();
                }
                else
                {
                    parts[i] = char.ToUpper(p[0]) + p.Substring(1).ToLowerInvariant();
                }
            }
            return string.Join(' ', parts);
        }

        public IActionResult Sucursales()
        {
            return View();
        }

        public IActionResult Contacto()
        {
            return View();
        }

        // Página del aviso de privacidad
        public IActionResult AvisoPrivacidad()
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
            var username = (HttpContext.Session.GetString("LoggedInUser") ?? string.Empty).Trim();
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

            var today = DateOnly.FromDateTime(DateTime.Today);
            if (reservationDate < today)
            {
                TempData["OrderError"] = "No puedes reservar en una fecha anterior al día de hoy.";
                return RedirectToAction("Ordenar");
            }

            // max 15 days ahead
            var maxAllowed = today.AddDays(15);
            if (reservationDate > maxAllowed)
            {
                TempData["OrderError"] = "La fecha máxima para reservar es dentro de 15 días desde hoy.";
                return RedirectToAction("Ordenar");
            }

            // ensure hour format HH:00
            if (!System.Text.RegularExpressions.Regex.IsMatch(hour, "^\\d{2}:00$"))
            {
                TempData["OrderError"] = "Hora inválida.";
                return RedirectToAction("Ordenar");
            }

            // same-day must be at least now + 2 hours (minute-accurate)
            if (int.TryParse(hour.Substring(0, 2), out var hourInt))
            {
                var slotDateTime = new DateTime(reservationDate.Year, reservationDate.Month, reservationDate.Day, hourInt, 0, 0);
                if (slotDateTime < DateTime.Now)
                {
                    TempData["OrderError"] = "No puedes reservar en una fecha u hora pasadas.";
                    return RedirectToAction("Ordenar");
                }
                if (reservationDate == DateOnly.FromDateTime(DateTime.Now))
                {
                    var earliest = DateTime.Now.AddHours(2);
                    if (slotDateTime < earliest)
                    {
                        TempData["OrderError"] = "Para reservas del mismo día, la hora debe ser al menos 2 horas después de la hora actual.";
                        return RedirectToAction("Ordenar");
                    }
                }
            }

            var branch = _branches.FirstOrDefault(b => b.Id == branchId);
            if (branch == null)
            {
                TempData["OrderError"] = "Sucursal no válida.";
                return RedirectToAction("Ordenar");
            }

            var newOrder = new OrderModel
            {
                Username = username,
                DisplayName = display,
                BranchId = branch.Id,
                BranchName = branch.Name,
                Date = reservationDate.ToString("yyyy-MM-dd"),
                Hour = hour,
                Persons = persons,
                Notes = notes,
                CreatedAt = DateTime.UtcNow,
                Status = "active"
            };

            if (!AppendOrderAtomic(newOrder, out var err))
            {
                TempData["OrderError"] = err ?? "Error al guardar la orden.";
                return RedirectToAction("Ordenar");
            }

            TempData["OrderSuccess"] = "Reservación confirmada";
            TempData["OrderInfo"] = JsonSerializer.Serialize(new { newOrder.Id, newOrder.BranchName, newOrder.Date, newOrder.Hour, newOrder.Persons });
            return RedirectToAction("Ordenar");
        }

        // Demo account page showing session user info
        public IActionResult MiCuenta()
        {
            var username = HttpContext.Session.GetString("LoggedInUser");
            if (string.IsNullOrEmpty(username))
            {
                return RedirectToAction("IniciarSesion");
            }

            var users = LoadUsersFromWebRoot(out var loadError);
            var user = users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

            ViewData["Username"] = username;
            ViewData["DisplayName"] = HttpContext.Session.GetString("LoggedInDisplayName") ?? username;
            ViewData["Email"] = HttpContext.Session.GetString("LoggedInEmail") ?? string.Empty;

            if (user != null)
            {
                ViewData["FullName"] = user.DisplayName ?? username;
                ViewData["DateOfBirth"] = user.DateOfBirth ?? string.Empty;
                // expose name parts for editing form
                ViewData["FirstName"] = user.FirstName ?? string.Empty;
                ViewData["MiddleName"] = user.MiddleName ?? string.Empty;
                ViewData["LastName"] = user.LastName ?? string.Empty;
            }
            else
            {
                ViewData["FullName"] = ViewData["DisplayName"];
                ViewData["DateOfBirth"] = string.Empty;
                ViewData["FirstName"] = string.Empty;
                ViewData["MiddleName"] = string.Empty;
                ViewData["LastName"] = string.Empty;
            }

            return View();
        }

        // POST: Edit account details (username + name parts). Email is readonly.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditAccount(string username, string firstName, string middleName, string lastName)
        {
            var current = HttpContext.Session.GetString("LoggedInUser");
            if (string.IsNullOrEmpty(current)) return RedirectToAction("IniciarSesion");

            username = (username ?? string.Empty).Trim();
            firstName = (firstName ?? string.Empty).Trim();
            middleName = string.IsNullOrWhiteSpace(middleName) ? null : middleName.Trim();
            lastName = (lastName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            {
                TempData["EditError"] = "Por favor completa usuario y nombre.";
                return RedirectToAction("MiCuenta");
            }

            var users = LoadUsersFromWebRoot(out var loadError);
            if (!string.IsNullOrEmpty(loadError))
            {
                TempData["EditError"] = "Error al leer usuarios: " + loadError;
                return RedirectToAction("MiCuenta");
            }

            // find the current user record by the original session username (case-insensitive)
            var user = users.FirstOrDefault(u => string.Equals(u.Username, current, StringComparison.OrdinalIgnoreCase));
            if (user == null)
            {
                TempData["EditError"] = "Usuario no encontrado.";
                return RedirectToAction("MiCuenta");
            }

            // if username changed, ensure uniqueness
            if (!string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase))
            {
                if (users.Any(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)))
                {
                    TempData["EditError"] = "El nombre de usuario ya está en uso.";
                    return RedirectToAction("MiCuenta");
                }
            }

            // Apply changes
            user.Username = username;
            user.FirstName = ToTitleCaseSmart(firstName);
            user.MiddleName = string.IsNullOrWhiteSpace(middleName) ? null : ToTitleCaseSmart(middleName!);
            user.LastName = ToTitleCaseSmart(lastName);
            user.FullName = string.Join(' ', new[] { user.FirstName, user.MiddleName, user.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

            // Save back to users.json
            var filePath = System.IO.Path.Combine(_env.WebRootPath ?? string.Empty, "data", "users.json");
            try
            {
                var writeOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                var updatedJson = JsonSerializer.Serialize(users, writeOptions);
                var dir = System.IO.Path.GetDirectoryName(filePath) ?? string.Empty;
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(filePath, updatedJson, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                TempData["EditError"] = "Error al guardar cambios: " + ex.Message;
                return RedirectToAction("MiCuenta");
            }

            // Update session values if username or display name changed
            HttpContext.Session.SetString("LoggedInUser", user.Username);
            HttpContext.Session.SetString("LoggedInDisplayName", user.DisplayName ?? user.Username);

            TempData["EditSuccess"] = "Datos actualizados correctamente.";
            return RedirectToAction("MiCuenta");
        }

        // Logout demo
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("LoggedInUser");
            HttpContext.Session.Remove("LoggedInDisplayName");
            HttpContext.Session.Remove("LoggedInEmail");

            // Clear any auth-related TempData to avoid showing previous success messages after logout
            if (TempData.ContainsKey("LoginSuccess")) TempData.Remove("LoginSuccess");
            if (TempData.ContainsKey("RegistrationSuccess")) TempData.Remove("RegistrationSuccess");
            if (TempData.ContainsKey("RegistrationError")) TempData.Remove("RegistrationError");

            return RedirectToAction("IniciarSesion");
        }

        // Acción para obtener disponibilidad por fecha y sucursal
        [HttpGet]
        public IActionResult GetAvailability(string date, int branchId = 0)
        {
            if (string.IsNullOrEmpty(date)) return Json(new { success = false, message = "Fecha requerida" });

            var orders = ReadOrdersSafe();
            var result = new Dictionary<string, int>();
            // hours from 09:00 to 20:00 (same as ViewData Hours)
            for (int h = 9; h < 21; h++)
            {
                var hour = h.ToString("D2") + ":00";
                var count = orders.Count(o => o.Date == date && o.Hour == hour);
                var remaining = Math.Max(0, 10 - count);
                result[hour] = remaining;
            }

            // compute remaining persons capacity for the full day (max 200)
            var totalPersonsForDate = orders.Where(o => o.Date == date).Sum(o => o.Persons);
            var dateRemaining = Math.Max(0, 200 - totalPersonsForDate);

            return Json(new { success = true, availability = result, dateRemaining });
        }

        // Generic error page used by exception handler
        public IActionResult Error()
        {
            return View("Error");
        }

        // Friendly 404 handler
        [HttpGet]
        public IActionResult Error404()
        {
            Response.StatusCode = 404;
            return View("Error404");
        }

        // GET: List orders for current user (active + history)
        [HttpGet]
        public IActionResult MyOrders()
        {
            var username = HttpContext.Session.GetString("LoggedInUser");
            if (string.IsNullOrEmpty(username)) return RedirectToAction("IniciarSesion");

            var orders = ReadOrdersSafe();
            var today = DateOnly.FromDateTime(DateTime.Today);
            var userOrders = orders.Where(o => string.Equals(o.Username, username, StringComparison.OrdinalIgnoreCase))
                                   .OrderByDescending(o => o.Date).ThenByDescending(o => o.Hour)
                                   .ToList();

            var active = new List<OrderModel>();
            var past = new List<OrderModel>();

            foreach (var o in userOrders)
            {
                if (DateOnly.TryParse(o.Date, out var d))
                {
                    if (string.Equals(o.Status, "active", StringComparison.OrdinalIgnoreCase) && d >= today)
                        active.Add(o);
                    else
                        past.Add(o);
                }
                else
                {
                    // if date cannot be parsed, treat as past
                    past.Add(o);
                }
            }

            ViewData["ActiveOrders"] = active;
            ViewData["PastOrders"] = past;
            LogOrderActivity($"MyOrders render username={username} active={active.Count} past={past.Count}");
            return PartialView("_MyOrdersPartial");
        }

        // Debug endpoint: return user's orders as JSON (active + past)
        [HttpGet]
        public IActionResult MyOrdersJson()
        {
            var username = HttpContext.Session.GetString("LoggedInUser");
            if (string.IsNullOrEmpty(username)) return Json(new { success = false, message = "Not authenticated" });

            var orders = ReadOrdersSafe();
            var today = DateOnly.FromDateTime(DateTime.Today);
            var userOrders = orders.Where(o => string.Equals(o.Username, username, StringComparison.OrdinalIgnoreCase))
                                   .OrderByDescending(o => o.Date).ThenByDescending(o => o.Hour)
                                   .ToList();

            var active = new List<OrderModel>();
            var past = new List<OrderModel>();

            foreach (var o in userOrders)
            {
                if (DateOnly.TryParse(o.Date, out var d))
                {
                    if (string.Equals(o.Status, "active", StringComparison.OrdinalIgnoreCase) && d >= today)
                        active.Add(o);
                    else
                        past.Add(o);
                }
                else
                {
                    past.Add(o);
                }
            }

            LogOrderActivity($"DEBUG MyOrdersJson user={username} total={userOrders.Count} active={active.Count} past={past.Count}");

            return Json(new { success = true, active, past });
        }

        // POST: Cancel order (mark status cancelled)
        [HttpPost]
        public IActionResult CancelOrder([FromBody] IdDto dto)
        {
            if (dto == null) return BadRequest("invalid payload");
            var username = HttpContext.Session.GetString("LoggedInUser");
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var ordersPath = GetOrdersPath();
            lock (_ordersLock)
            {
                List<OrderModel> orders = new();
                if (System.IO.File.Exists(ordersPath))
                {
                    try { orders = JsonSerializer.Deserialize<List<OrderModel>>(System.IO.File.ReadAllText(ordersPath, Encoding.UTF8)) ?? new List<OrderModel>(); } catch { orders = new List<OrderModel>(); }
                }

                var target = orders.FirstOrDefault(o => o.Id == dto.Id);
                if (target == null) return NotFound();
                if (!string.Equals(target.Username, username, StringComparison.OrdinalIgnoreCase)) return Forbid();

                target.Status = "cancelled";
                try { var write = JsonSerializer.Serialize(orders, new JsonSerializerOptions { WriteIndented = true }); System.IO.File.WriteAllText(ordersPath, write, Encoding.UTF8); LogOrderActivity($"OK cancelled username={username} id={dto.Id}"); return Ok(new { success = true }); }
                catch { LogOrderActivity($"ERROR cancel username={username} id={dto.Id}"); return StatusCode(500, "Error al actualizar la orden."); }
            }
        }

        // POST: Delete order
        [HttpPost]
        public IActionResult DeleteOrder([FromBody] IdDto dto)
        {
            if (dto == null) return BadRequest("invalid payload");
            var username = HttpContext.Session.GetString("LoggedInUser");
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            var ordersPath = GetOrdersPath();
            lock (_ordersLock)
            {
                List<OrderModel> orders = new();
                if (System.IO.File.Exists(ordersPath))
                {
                    try { orders = JsonSerializer.Deserialize<List<OrderModel>>(System.IO.File.ReadAllText(ordersPath, Encoding.UTF8)) ?? new List<OrderModel>(); } catch { orders = new List<OrderModel>(); }
                }

                var target = orders.FirstOrDefault(o => o.Id == dto.Id);
                if (target == null) return NotFound();
                if (!string.Equals(target.Username, username, StringComparison.OrdinalIgnoreCase)) return Forbid();

                orders.RemoveAll(o => o.Id == dto.Id);
                try { var write = JsonSerializer.Serialize(orders, new JsonSerializerOptions { WriteIndented = true }); System.IO.File.WriteAllText(ordersPath, write, Encoding.UTF8); LogOrderActivity($"OK deleted username={username} id={dto.Id}"); return Ok(new { success = true }); }
                catch { LogOrderActivity($"ERROR delete username={username} id={dto.Id}"); return StatusCode(500, "Error al eliminar la orden."); }
            }
        }

        // POST: submit edit (only change hour, persons, notes allowed) - enforce same validations
        [HttpPost]
        public IActionResult EditOrderSubmit([FromBody] EditOrderDto dto)
        {
            if (dto == null) return BadRequest("invalid payload");
            var username = HttpContext.Session.GetString("LoggedInUser");
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.Hour) || !System.Text.RegularExpressions.Regex.IsMatch(dto.Hour, "^\\d{2}:00$"))
                return BadRequest("Hora inválida.");

            var ordersPath = GetOrdersPath();
            lock (_ordersLock)
            {
                List<OrderModel> orders = new();
                if (System.IO.File.Exists(ordersPath))
                {
                    try { orders = JsonSerializer.Deserialize<List<OrderModel>>(System.IO.File.ReadAllText(ordersPath, Encoding.UTF8)) ?? new List<OrderModel>(); } catch { orders = new List<OrderModel>(); }
                }

                var ord = orders.FirstOrDefault(o => o.Id == dto.Id);
                if (ord == null) return NotFound();
                if (!string.Equals(ord.Username, username, StringComparison.OrdinalIgnoreCase)) return Forbid();

                if (!DateOnly.TryParse(ord.Date, out var reservationDate)) return BadRequest("Fecha inválida en la orden.");
                if (DateOnly.FromDateTime(DateTime.Today) > reservationDate) return BadRequest("No se pueden editar reservaciones pasadas.");

                if (int.TryParse(dto.Hour.Substring(0,2), out var hInt))
                {
                    var slotDateTime = new DateTime(reservationDate.Year, reservationDate.Month, reservationDate.Day, hInt, 0, 0);
                    if (slotDateTime < DateTime.Now) return BadRequest("No puedes mover la reservación a una hora pasada.");
                    if (reservationDate == DateOnly.FromDateTime(DateTime.Now) && slotDateTime < DateTime.Now.AddHours(2)) return BadRequest("Para reservas del mismo día, la hora debe ser al menos 2 horas después de la hora actual.");
                }

                var duplicateExact = orders.Any(o => o.Id != dto.Id && string.Equals(o.Username, username, StringComparison.OrdinalIgnoreCase)
                                                     && o.Date == ord.Date && o.Hour == dto.Hour && o.BranchId == ord.BranchId);
                if (duplicateExact) return BadRequest("Ya existe una reservación idéntica para esa hora.");

                var sameSlotCount = orders.Count(o => o.Date == ord.Date && o.Hour == dto.Hour && o.Id != dto.Id);
                if (sameSlotCount >= 10) return BadRequest("Lo sentimos, ya se alcanzó el límite de 10 reservaciones para esa hora.");

                // Comprobar la capacidad diaria: suma de personas para la fecha, <= 200
                var totalPersonsExcludingThis = orders.Where(o => o.Date == ord.Date && o.Id != ord.Id).Sum(o => o.Persons);
                if (totalPersonsExcludingThis + dto.Persons > 200)
                    return BadRequest("No hay capacidad suficiente para la cantidad de personas en esa fecha.");

                // apply changes (only hour, persons, notes)
                ord.Hour = dto.Hour;
                ord.Persons = dto.Persons;
                ord.Notes = dto.Notes;

                try { var write = JsonSerializer.Serialize(orders, new JsonSerializerOptions { WriteIndented = true }); System.IO.File.WriteAllText(ordersPath, write, Encoding.UTF8); LogOrderActivity($"OK edited username={username} id={dto.Id} hour={dto.Hour}"); return Ok(new { success = true }); }
                catch { LogOrderActivity($"ERROR edit username={username} id={dto.Id}"); return StatusCode(500, "Error al guardar cambios."); }
            }
        }

        // GET: Edit order (form)
        [HttpGet]
        public IActionResult EditOrder(int id)
        {
            var username = HttpContext.Session.GetString("LoggedInUser");
            if (string.IsNullOrEmpty(username)) return RedirectToAction("IniciarSesion");

            var orders = ReadOrdersSafe();
            var order = orders.FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound();
            if (!string.Equals(order.Username, username, StringComparison.OrdinalIgnoreCase)) return Forbid();

            // only allow editing future orders
            var todayStr = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
            if (String.Compare(order.Date, todayStr) < 0) return BadRequest("No se pueden editar reservaciones pasadas.");

            ViewData["Branches"] = _branches;
            return PartialView("_EditOrderPartial", order);
        }
    }

    public class BranchModel
    {
        public int Id { get; set; }
        public string StateKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class IdDto
    {
        public int Id { get; set; }
    }

    public class EditOrderDto
    {
        public int Id { get; set; }
        public string Hour { get; set; } = string.Empty;
        public int Persons { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
