using ClutchPick.Models;
using ClutchPick.Models.ClutchPick.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

public class DashboardModel : PageModel
{
    private readonly string _connectionString = "Server=tcp:placebo.database.windows.net,1433;Initial Catalog=clutchPick;Persist Security Info=False;User ID=placeboadmin;Password=NiteTheory!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

    public string DiscordName { get; set; }
    public string DiscordAvatarUrl { get; set; }

    public Week CurrentWeek { get; set; } = new();
    public Week NextWeek { get; set; } = new();

    public List<FantasyPlayerViewModel> CurrentWeekLineup { get; set; } = new();
    public List<FantasyPlayerViewModel> NextWeekLineup { get; set; } = new();
    public List<FantasyPlayerViewModel> AvailablePlayers { get; set; } = new();

    public decimal CurrentWeekBudget { get; set; } = 50000;
    public decimal NextWeekBudget { get; set; } = 50000;

    public const int MaxSlots = 5;

    public int CurrentUserID { get; set; }

    [BindProperty] public int AddPlayerID { get; set; }
    [BindProperty] public int RemovePlayerID { get; set; }

    public void OnGet()
    {
        try
        {
            var discordId = User.FindFirst("urn:discord:id")?.Value;
            var avatarHash = User.FindFirst("urn:discord:avatar")?.Value;
            DiscordName = User.Identity?.Name ?? "Guest";

            DiscordAvatarUrl = !string.IsNullOrEmpty(discordId) && !string.IsNullOrEmpty(avatarHash)
                ? $"https://cdn.discordapp.com/avatars/{discordId}/{avatarHash}.png"
                : "https://cdn.discordapp.com/embed/avatars/0.png";

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            if (!string.IsNullOrEmpty(discordId))
            {
                CurrentUserID = GetOrCreateUserId(conn, discordId, DiscordName);
                TempData["Debug"] = $"Discord claim: {discordId} | Name: {DiscordName} | CurrentUserID set to {CurrentUserID}";
            }
            else
            {
                TempData["Debug"] = $"No Discord ID found, user not logged in via Discord.";
            }

            LoadWeeks(conn);
            TempData["Debug"] += $" | Loaded weeks: CurrentWeekID={CurrentWeek.WeekID}, NextWeekID={(NextWeek?.WeekID ?? 0)}";

            LoadAvailablePlayers(conn);
            TempData["Debug"] += $" | AvailablePlayers count: {AvailablePlayers.Count}";

            if (CurrentUserID > 0)
            {
                LoadWeekLineup(conn, CurrentUserID, CurrentWeek.WeekID, CurrentWeekLineup, out decimal currentSpent);
                CurrentWeekBudget = 50000 - currentSpent;

                LoadWeekLineup(conn, CurrentUserID, NextWeek.WeekID, NextWeekLineup, out decimal nextSpent);
                NextWeekBudget = 50000 - nextSpent;

                TempData["Debug"] += $" | CurrentWeekLineup: {CurrentWeekLineup.Count}, NextWeekLineup: {NextWeekLineup.Count}";
            }
        }
        catch (Exception ex)
        {
            TempData["Debug"] = "Error loading dashboard: " + ex.Message;
        }
    }
    public IActionResult OnPostAddNextWeekPlayer()
    {
        var discordId = User.FindFirst("urn:discord:id")?.Value;
        var discordName = User.Identity?.Name ?? "Guest";

        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        if (!string.IsNullOrEmpty(discordId))
        {
            CurrentUserID = GetOrCreateUserId(conn, discordId, discordName);
        }

        if (CurrentUserID == 0)
        {
            TempData["Debug"] = "CurrentUserID is 0, cannot add player.";
            return RedirectToPage();
        }

        if (AddPlayerID == 0)
        {
            TempData["Debug"] = "AddPlayerID is 0, no player selected.";
            return RedirectToPage();
        }

        try
        {
            LoadWeeks(conn);

            if (NextWeek == null || NextWeek.WeekID == 0)
            {
                TempData["Debug"] = "Next week not available.";
                return RedirectToPage();
            }

            using var cmd = new SqlCommand(
                "insert into UserPicks (UserID, PlayerID, WeekID) VALUES (@UserID,@PlayerID,@WeekID)", conn);

            cmd.Parameters.AddWithValue("@UserID", CurrentUserID);
            cmd.Parameters.AddWithValue("@PlayerID", AddPlayerID);
            cmd.Parameters.AddWithValue("@WeekID", NextWeek.WeekID);

            cmd.ExecuteNonQuery();
            TempData["Debug"] = $"✅ Added PlayerID {AddPlayerID} for UserID {CurrentUserID} in WeekID {NextWeek.WeekID}";
        }
        catch (Exception ex)
        {
            TempData["Debug"] = $"❌ Exception: {ex.Message}";
        }

        return RedirectToPage();
    }
    public IActionResult OnPostRemoveNextWeekPlayer()
    {
        var discordId = User.FindFirst("urn:discord:id")?.Value;
        var discordName = User.Identity?.Name ?? "Guest";

        using var conn = new SqlConnection(_connectionString);
        conn.Open();

        if (!string.IsNullOrEmpty(discordId))
        {
            CurrentUserID = GetOrCreateUserId(conn, discordId, discordName);
        }

        if (CurrentUserID == 0)
        {
            TempData["Debug"] = "CurrentUserID is 0, cannot add player.";
            return RedirectToPage();
        }

        if (AddPlayerID == 0)
        {
            TempData["Debug"] = "AddPlayerID is 0, no player selected.";
            return RedirectToPage();
        }

        try
        {
            LoadWeeks(conn);

            if (NextWeek == null || NextWeek.WeekID == 0)
            {
                TempData["Debug"] = "Next week not available.";
                return RedirectToPage();
            }

            using var cmd = new SqlCommand(
                "delete from UserPicks where UserID=@UserID and PlayerID=@PlayerID and WeekID=@WeekID", conn);

            cmd.Parameters.AddWithValue("@UserID", CurrentUserID);
            cmd.Parameters.AddWithValue("@PlayerID", AddPlayerID);
            cmd.Parameters.AddWithValue("@WeekID", NextWeek.WeekID);

            cmd.ExecuteNonQuery();
            TempData["Debug"] = $"Removed PlayerID {AddPlayerID} for UserID {CurrentUserID} in WeekID {NextWeek.WeekID}";
        }
        catch (Exception ex)
        {
            TempData["Debug"] = $"❌ Exception: {ex.Message}";
        }

        return RedirectToPage();
    }


    private void LoadWeeks(SqlConnection conn)
    {
        using var cmdCurrent = new SqlCommand("select top 1 * from Weeks where IsCurrent=1", conn);
        using var readerCurrent = cmdCurrent.ExecuteReader();
        if (readerCurrent.Read())
        {
            CurrentWeek = new Week
            {
                WeekID = Convert.ToInt32(readerCurrent["WeekID"]),
                WeekNumber = Convert.ToInt32(readerCurrent["WeekNumber"]),
                StartDate = Convert.ToDateTime(readerCurrent["StartDate"]),
                EndDate = Convert.ToDateTime(readerCurrent["EndDate"])
            };
        }
        readerCurrent.Close();

        using var cmdNext = new SqlCommand("select top 1 * from Weeks where WeekNumber=@NextWeekNumber", conn);
        cmdNext.Parameters.AddWithValue("@NextWeekNumber", CurrentWeek.WeekNumber + 1);
        using var readerNext = cmdNext.ExecuteReader();
        if (readerNext.Read())
        {
            NextWeek = new Week
            {
                WeekID = Convert.ToInt32(readerNext["WeekID"]),
                WeekNumber = Convert.ToInt32(readerNext["WeekNumber"]),
                StartDate = Convert.ToDateTime(readerNext["StartDate"]),
                EndDate = Convert.ToDateTime(readerNext["EndDate"])
            };
        }
        else
        {
            NextWeek = null;
        }
        readerNext.Close();
    }

    private void LoadAvailablePlayers(SqlConnection conn)
    {
        using var cmd = new SqlCommand(@"
            select lp.PlayerID, lp.InGameName, lp.Salary, lp.League, lp.Rating, t.TeamName
            from LeaguePlayers lp
            left join Teams t ON lp.TeamID=t.TeamID
            order by lp.Rating DESC", conn);

        using var reader = cmd.ExecuteReader();
        AvailablePlayers.Clear();
        while (reader.Read())
        {
            AvailablePlayers.Add(new FantasyPlayerViewModel
            {
                PlayerID = Convert.ToInt32(reader["PlayerID"]),
                InGameName = reader["InGameName"].ToString(),
                Salary = reader["Salary"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Salary"]),
                League = reader["League"]?.ToString() ?? "",
                Team = reader["TeamName"]?.ToString() ?? "N/A",
                Rating = reader["Rating"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["Rating"]),
                WeekPoints = 0m
            });
        }
    }
    private void LoadWeekLineup(SqlConnection conn, int userId, int weekId, List<FantasyPlayerViewModel> lineup, out decimal totalSalary)
    {
        lineup.Clear();
        totalSalary = 0;
        if (weekId == 0)
        {
            Console.WriteLine("⚠️ WeekID = 0, skipping lineup load.");
            return;
        }

        Console.WriteLine($"➡️ Loading lineup for User {userId}, Week {weekId}");

        using var cmd = new SqlCommand(@"
        select lp.PlayerID, lp.InGameName, lp.Salary, ISNULL(ps.Points,0) as WeekPoints, lp.TeamID
        from UserPicks up
        JOIN LeaguePlayers lp on up.PlayerID=lp.PlayerID
        left join PlayerStats ps on ps.PlayerID=lp.PlayerID and ps.WeekID=up.WeekID
        where up.UserID=@UserID and up.WeekID=@WeekID", conn);

        cmd.Parameters.AddWithValue("@UserID", userId);
        cmd.Parameters.AddWithValue("@WeekID", weekId);

        using var reader = cmd.ExecuteReader();
        var players = new List<(FantasyPlayerViewModel Player, int TeamID)>();

        while (reader.Read())
        {
            var p = new FantasyPlayerViewModel
            {
                PlayerID = Convert.ToInt32(reader["PlayerID"]),
                InGameName = reader["InGameName"].ToString(),
                Salary = Convert.ToDecimal(reader["Salary"]),
                WeekPoints = Convert.ToDecimal(reader["WeekPoints"])
            };
            int teamId = Convert.ToInt32(reader["TeamID"]);

            Console.WriteLine($"✅ Picked Player: {p.InGameName}, TeamID={teamId}, Salary={p.Salary}, WeekPoints={p.WeekPoints}");

            players.Add((p, teamId));
            lineup.Add(p);
            totalSalary += p.Salary;
        }
        reader.Close();

        if (!players.Any())
        {
            Console.WriteLine("⚠️ No players found for this lineup.");
        }

        // Now lookup opponents for each player
        foreach (var (player, teamId) in players)
        {
            Console.WriteLine($"➡️ Looking up match for Player {player.InGameName} (TeamID={teamId})");

            using var matchCmd = new SqlCommand(@"
            select t1.TeamName as TeamA, t2.TeamName as TeamB, m.TeamAID, m.TeamBID
            from Matches m
            join Teams t1 ON m.TeamAID = t1.TeamID
            join Teams t2 ON m.TeamBID = t2.TeamID
            where m.WeekID=@WeekID and (m.TeamAID=@TeamID OR m.TeamBID=@TeamID)", conn);

            matchCmd.Parameters.AddWithValue("@WeekID", weekId);
            matchCmd.Parameters.AddWithValue("@TeamID", teamId);

            using var matchReader = matchCmd.ExecuteReader();
            if (matchReader.Read())
            {
                int teamAId = Convert.ToInt32(matchReader["TeamAID"]);
                int teamBId = Convert.ToInt32(matchReader["TeamBID"]);

                player.OpponentTeamName = teamId == teamAId
                    ? matchReader["TeamB"].ToString()
                    : matchReader["TeamA"].ToString();

                Console.WriteLine($"   ✅ Opponent for {player.InGameName}: {player.OpponentTeamName}");
            }
            else
            {
                Console.WriteLine($"   ❌ No match found for TeamID={teamId}, WeekID={weekId}");
            }
        }

        Console.WriteLine($"➡️ Total Salary: {totalSalary}");
    }



    private int GetOrCreateUserId(SqlConnection conn, string discordId, string userName)
    {
        using var check = new SqlCommand("select UserID from Users where DiscordID=@DiscordID", conn);
        check.Parameters.AddWithValue("@DiscordID", discordId);
        var res = check.ExecuteScalar();
        if (res != null && res != DBNull.Value)
            return Convert.ToInt32(res);

        using var insert = new SqlCommand(
            "insert into Users (DiscordID, UserName) output inserted.UserID values (@DiscordID,@UserName)", conn);
        insert.Parameters.AddWithValue("@DiscordID", discordId);
        insert.Parameters.AddWithValue("@UserName", userName ?? "");
        return Convert.ToInt32(insert.ExecuteScalar());
    }
}
