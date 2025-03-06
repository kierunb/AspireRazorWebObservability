using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;

namespace RazorWebApp.Pages
{
    [OutputCache(PolicyName = "Expire20")]
    public class TestModel : PageModel
    {

        public string Date { get; set; } = string.Empty;

       
        public void OnGet()
        {
            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
