using AspNet.Security.OAuth.Discord;
using ClutchPick.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

public class IndexModel : PageModel
{
    private readonly string _connectionString = "Server=tcp:placebo.database.windows.net,1433;Initial Catalog=clutchPick;Persist Security Info=False;User ID=placeboadmin;Password=NiteTheory!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

    public List<MatchViewModel> WeeklyMatches { get; set; } = new();
    public List<FantasyPlayerViewModel> TopPlayers { get; set; } = new();

    public void OnGet()
    {
        int currentWeekId = 0;

        using (var conn = new SqlConnection(_connectionString))
        {
            conn.Open();

            // Get current week safely
            using (var cmd = new SqlCommand("select top 1 WeekID from Weeks order by StartDate DESC", conn))
            {
                var result = cmd.ExecuteScalar();
                if (result != null)
                    currentWeekId = (int)result;
                else
                    return; // No weeks yet
            }


            // Load top fantasy players (TOP 10)
            using (var cmd = new SqlCommand(@"
                SELECT TOP 10 lp.InGameName, wp.Points
                FROM WeeklyPoints wp
                JOIN LeaguePlayers lp ON wp.PlayerID = lp.PlayerID
                WHERE wp.WeekID = @WeekID
                ORDER BY wp.Points DESC", conn))
            {
                cmd.Parameters.Add("@WeekID", System.Data.SqlDbType.Int).Value = currentWeekId -1;
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        TopPlayers.Add(new FantasyPlayerViewModel
                        {
                            InGameName = reader.GetString(0),
                            WeekPoints = reader.GetDecimal(1)
                        });
                    }
                }
            }
        }
    }

    public async Task<IActionResult> OnGetLoginAsync()
    {
        // Trigger Discord OAuth login
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = "/Dashboard"  // after login, send user to dashboard
        }, DiscordAuthenticationDefaults.AuthenticationScheme);
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }
}
