namespace ClutchPick.Models
{
    public class FantasyPlayerViewModel
    {
        public int PlayerID { get; set; }
        public string InGameName { get; set; }
        public decimal Salary { get; set; }

        // Week references
        public int WeekID { get; set; }
        public int WeekNumber { get; set; }

        public decimal WeekPoints { get; set; }
        public int Slot { get; set; } = 0;
        public string Team { get; set; }
        public string League { get; set; }
        public decimal Rating { get; set; }

        // Stats
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int Saves { get; set; }
        public int Shots { get; set; }
        public int Passes { get; set; }
        public decimal MinutesPlayed { get; set; }
        public int GameWins { get; set; }
        public int SeriesSweepBonus { get; set; }
        public int TotalPoints { get; set; }


        public string OpponentTeamName { get; set; }
    }
}
