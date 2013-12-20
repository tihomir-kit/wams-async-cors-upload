using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using WACU.Infrastructure;

namespace WACU.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            if (String.IsNullOrEmpty(AppSettings.WamsAccountName) || String.IsNullOrEmpty(AppSettings.WamsAccountKey))
                return RedirectToAction("ConfigError", "Home");

            return View();
        }

        public ActionResult ConfigError()
        {
            return View();
        }
    }
}
