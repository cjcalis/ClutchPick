namespace ClutchPick.Models
{
    public class PlayerStats
    {
        public int PlayerStatsID { get; set; }
        public int PlayerID { get; set; }

        public int WeekID { get; set; }

        // Display reference
        public int WeekNumber { get; set; }

        public int Goals { get; set; }
        public int Assists { get; set; }
        public int Saves { get; set; }
        public int Shots { get; set; }
        public int GameWins { get; set; }
        public int SeriesSweepBonus { get; set; }
        public decimal TotalPoints { get; set; }
    }
}
