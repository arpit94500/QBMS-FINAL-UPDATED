using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QBMS1.Models;
using System.Diagnostics;

namespace QBMS1.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            Response.StatusCode = 500;
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        [Route("Home/StatusCode")]
        public IActionResult StatusCode(int code)
        {
            Response.StatusCode = code;
            ViewBag.StausCode = code;
            if (code == 404)
                ViewData["Title"] = "Page Not Found";
            else if (code == 403)
                ViewData["Title"] = "Access Denied";
            else
                ViewData["Title"] = "Request Error";
            return View("StatusCode");
        }
    }
}
