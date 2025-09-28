using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using ClutchPick.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace ClutchPick.Pages.Players
{
    public class PlayerDetailModel : PageModel
    {
        private readonly string _connectionString =
            "Server=tcp:placebo.database.windows.net,1433;Initial Catalog=clutchPick;Persist Security Info=False;User ID=placeboadmin;Password=NiteTheory!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

        public FantasyPlayerViewModel Player { get; set; } = new();
        public List<PlayerStats> Stats { get; set; } = new();

        public List<FantasyPlayerViewModel> MyLineup { get; set; } = new();
        public decimal BudgetRemaining { get; set; } = 50_000;

        [BindProperty(SupportsGet = true)]
        public int PlayerID { get; set; }

        [BindProperty]
        public int AddPlayerID { get; set; }

        [BindProperty]
        public int RemovePlayerID { get; set; }

        public bool ShowAddModal { get; set; } = false;
        public string AddPlayerMessage { get; set; } = "";
        public bool CanReplacePlayer { get; set; } = false;
        public int PendingAddPlayerID { get; set; }

        public void OnGet()
        {
            LoadPlayer();
            LoadStats();
            LoadUserLineup();
        }

        private void LoadPlayer()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    selct lp.PlayerID, lp.InGameName, lp.Salary, lp.League, lp.Rating,
                           t.TeamName
                    from LeaguePlayers lp
                    left join Teams t ON lp.TeamID = t.TeamID
                    where lp.PlayerID=@PlayerID", conn);
                cmd.Parameters.AddWithValue("@PlayerID", PlayerID);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Player = new FantasyPlayerViewModel
                    {
                        PlayerID = Convert.ToInt32(reader["PlayerID"]),
                        InGameName = reader["InGameName"].ToString(),
                        Salary = reader["Salary"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Salary"]),
                        League = reader["League"]?.ToString() ?? "",
                        Team = reader["TeamName"]?.ToString() ?? "N/A",
                        Rating = reader["Rating"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Rating"])
                    };
                }
            }
            catch (Exception ex)
            {
                ViewData["Debug"] = ex.Message;
            }
        }

        private void LoadStats()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                using var cmd = new SqlCommand(@"
                    select w.WeekNumber , ps.Goals, ps.Assists, ps.Saves, ps.Shots, ps.GameWins, ps.SeriesSweepBonus, ps.Points AS TotalPoints
                    from PlayerStats ps
                    join Weeks w ON ps.WeekID=w.WeekID
                    where ps.PlayerID=@PlayerID
                    order by w.WeekNumber ASC", conn);
                cmd.Parameters.AddWithValue("@PlayerID", PlayerID);
                using var reader = cmd.ExecuteReader();
                Stats.Clear();
                while (reader.Read())
                {
                    Stats.Add(new PlayerStats
                    {
                        WeekID = Convert.ToInt32(reader["WeekNumber"] ),
                        Goals = Convert.ToInt32(reader["Goals"]),
                        Assists = Convert.ToInt32(reader["Assists"]),
                        Saves = Convert.ToInt32(reader["Saves"]),
                        Shots = Convert.ToInt32(reader["Shots"]),
                        GameWins = Convert.ToInt32(reader["GameWins"]),
                        SeriesSweepBonus = Convert.ToInt32(reader["SeriesSweepBonus"]),
                        TotalPoints = Convert.ToDecimal(reader["TotalPoints"])
                    });
                }
            }
            catch { }
        }

        private void LoadUserLineup()
        {
            try
            {
                var discordId = User.FindFirst("urn:discord:id")?.Value;
                if (string.IsNullOrEmpty(discordId)) return;

                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                int userId = GetOrCreateUser(conn, discordId);

                // Get current week
                int weekId = GetCurrentWeekId(conn);

                // Load user's lineup
                using var cmd = new SqlCommand(@"
                    select lp.PlayerID, lp.InGameName, lp.Salary
                    from UserPicks up
                    join LeaguePlayers lp on up.PlayerID=lp.PlayerID
                    where up.UserID=@UserID and up.WeekID=@WeekID", conn);
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@WeekID", weekId);

                using var reader = cmd.ExecuteReader();
                MyLineup.Clear();
                BudgetRemaining = 50_000;

                while (reader.Read())
                {
                    var p = new FantasyPlayerViewModel
                    {
                        PlayerID = Convert.ToInt32(reader["PlayerID"]),
                        InGameName = reader["InGameName"].ToString(),
                        Salary = Convert.ToDecimal(reader["Salary"])
                    };
                    MyLineup.Add(p);
                    BudgetRemaining -= p.Salary;
                }
            }
            catch { }
        }

        public IActionResult OnPostAddPlayer()
        {
            PendingAddPlayerID = AddPlayerID;
            var discordId = User.FindFirst("urn:discord:id")?.Value;
            if (string.IsNullOrEmpty(discordId)) return RedirectToPage();

            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            int userId = GetOrCreateUser(conn, discordId);
            int weekId = GetCurrentWeekId(conn);

            LoadUserLineup(); // refresh lineup to calculate budget

            var playerSalary = GetPlayerSalary(conn, AddPlayerID);
            if (MyLineup.Count >= 5)
            {
                ShowAddModal = true;
                AddPlayerMessage = "Your lineup is full! Remove a player to add this one.";
                CanReplacePlayer = true;
                return Page();
            }
            if (BudgetRemaining < playerSalary)
            {
                ShowAddModal = true;
                AddPlayerMessage = "Adding this player would exceed your budget.";
                CanReplacePlayer = BudgetRemaining + MyLineup.Max(p => p.Salary) >= playerSalary;
                return Page();
            }

            // Add player normally
            using var cmd = new SqlCommand("insert into UserPicks (UserID, PlayerID, WeekID) values (@UserID,@PlayerID,@WeekID)", conn);
            cmd.Parameters.AddWithValue("@UserID", userId);
            cmd.Parameters.AddWithValue("@PlayerID", AddPlayerID);
            cmd.Parameters.AddWithValue("@WeekID", weekId);
            cmd.ExecuteNonQuery();

            return RedirectToPage(new { PlayerID = AddPlayerID });
        }

        public IActionResult OnPostReplacePlayer()
        {
            var discordId = User.FindFirst("urn:discord:id")?.Value;
            if (string.IsNullOrEmpty(discordId)) return RedirectToPage();

            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            int userId = GetOrCreateUser(conn, discordId);
            int weekId = GetCurrentWeekId(conn);

            // Remove selected player
            using (var cmd = new SqlCommand("delete from UserPicks where UserID=@UserID and PlayerID=@PlayerID and WeekID=@WeekID", conn))
            {
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@PlayerID", RemovePlayerID);
                cmd.Parameters.AddWithValue("@WeekID", weekId);
                cmd.ExecuteNonQuery();
            }

            // Add pending player
            using (var cmd = new SqlCommand("insert into UserPicks (UserID, PlayerID, WeekID) values (@UserID,@PlayerID,@WeekID)", conn))
            {
                cmd.Parameters.AddWithValue("@UserID", userId);
                cmd.Parameters.AddWithValue("@PlayerID", PendingAddPlayerID);
                cmd.Parameters.AddWithValue("@WeekID", weekId);
                cmd.ExecuteNonQuery();
            }

            return RedirectToPage(new { PlayerID = PendingAddPlayerID });
        }

        private int GetOrCreateUser(SqlConnection conn, string discordId)
        {
            using var cmd = new SqlCommand("SELECT UserID FROM Users WHERE DiscordID=@DiscordID", conn);
            cmd.Parameters.AddWithValue("@DiscordID", discordId);
            var res = cmd.ExecuteScalar();
            if (res != null && res != DBNull.Value)
                return Convert.ToInt32(res);

            using var insert = new SqlCommand("insert into Users (DiscordID, UserName) OUTPUT INSERTED.UserID values (@DiscordID,@UserName)", conn);
            insert.Parameters.AddWithValue("@DiscordID", discordId);
            insert.Parameters.AddWithValue("@UserName", User.Identity?.Name ?? "");
            return Convert.ToInt32(insert.ExecuteScalar());
        }

        private int GetCurrentWeekId(SqlConnection conn)
        {
            using var cmd = new SqlCommand("SELECT TOP 1 WeekID FROM Weeks ORDER BY WeekID DESC", conn);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private decimal GetPlayerSalary(SqlConnection conn, int playerId)
        {
            using var cmd = new SqlCommand("SELECT Salary FROM LeaguePlayers WHERE PlayerID=@PlayerID", conn);
            cmd.Parameters.AddWithValue("@PlayerID", playerId);
            return Convert.ToDecimal(cmd.ExecuteScalar());
        }
    }
}
