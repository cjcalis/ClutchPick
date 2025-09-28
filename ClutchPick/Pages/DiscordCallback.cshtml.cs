using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ClutchPick.Pages
{
    public class DiscordCallbackModel : PageModel
    {
        private readonly string _connectionString = "Server=tcp:placebo.database.windows.net,1433;Initial Catalog=clutchPick;Persist Security Info=False;User ID=placeboadmin;Password=NiteTheory!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

        public async Task<IActionResult> OnGetAsync()
        {
            // Ensure user is authenticated by Discord
            var result = await HttpContext.AuthenticateAsync("Discord");
            if (!result.Succeeded || result.Principal == null)
            {
                return RedirectToPage("/Index");
            }

            var discordId = result.Principal.FindFirstValue("urn:discord:id");
            var username = result.Principal.FindFirstValue("urn:discord:username");

            if (string.IsNullOrEmpty(discordId))
            {
                return RedirectToPage("/Index");
            }

            int userId;

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Check if the user already exists
                using (var checkCmd = new SqlCommand("select UserID from Users where DiscordID = @DiscordID", conn))
                {
                    checkCmd.Parameters.AddWithValue("@DiscordID", discordId);
                    var resultId = checkCmd.ExecuteScalar();
                    if (resultId != null)
                    {
                        userId = (int)resultId;
                    }
                    else
                    {
                        // Insert new user
                        using (var insertCmd = new SqlCommand("insert into Users (DiscordID, UserName) output inserted.UserID values (@DiscordID, @UserName)", conn))
                        {
                            insertCmd.Parameters.AddWithValue("@DiscordID", discordId);
                            insertCmd.Parameters.AddWithValue("@UserName", username ?? "");
                            userId = (int)insertCmd.ExecuteScalar();
                        }
                    }
                }
            }

            // Sign in the user with a cookie
            var claims = new[]
            {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username ?? "DiscordUser")
        };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToPage("/Dashboard");
        }
    }
}