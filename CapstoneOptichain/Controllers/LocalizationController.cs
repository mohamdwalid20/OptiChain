using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System;

namespace CapstoneOptichain.Controllers
{
    public class LocalizationController : Controller
    {
        [HttpPost]
        [HttpGet]
        public IActionResult SetLanguage(string culture, string returnUrl = "/")
        {
            try
            {
                // استخدم اسم ثابت للكوكي
                const string cultureCookieName = ".AspNetCore.Culture";

                // امسح الكوكي القديم
                Response.Cookies.Delete(cultureCookieName, new CookieOptions
                {
                    Path = "/",
                    Expires = DateTimeOffset.Now.AddDays(-1)
                });

                // لو إنجليزي، ارجع من غير ما تحط كوكي جديدة
                if (culture == "en" || string.IsNullOrEmpty(culture))
                {
                    return Redirect(returnUrl);
                }


                Response.Cookies.Append(
                    cultureCookieName,
                    $"c={culture}|uic={culture}",
                    new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddYears(1),
                        Path = "/",
                        IsEssential = true,
                        Secure = Request.IsHttps,
                        SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax
                    }
                );

                return Redirect(returnUrl);
            }
            catch (Exception ex)
            {

                return Redirect(returnUrl);
            }
        }

        [HttpGet]
        public IActionResult CurrentCulture()
        {
            var rqf = HttpContext.Features.Get<IRequestCultureFeature>();
            var culture = rqf?.RequestCulture.UICulture.Name ?? "en";
            return Json(new { culture });
        }
    }
}