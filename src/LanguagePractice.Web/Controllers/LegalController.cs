using Microsoft.AspNetCore.Mvc;

namespace LanguagePractice.Web.Controllers;

public class LegalController : Controller
{
    [HttpGet("/about")]
    public IActionResult About() => View();

    [HttpGet("/privacy")]
    public IActionResult Privacy() => View();

    [HttpGet("/terms")]
    public IActionResult Terms() => View();

    [HttpGet("/cookies")]
    public IActionResult Cookies() => View();

    [HttpGet("/community")]
    public IActionResult Community() => View();

    [HttpGet("/contact")]
    public IActionResult Contact() => View();
}
