namespace ClutchPick.Models
{
    namespace ClutchPick.Models
    {
        public class Week
        {
            public int WeekID { get; set; }      
            public int WeekNumber { get; set; }   
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public bool IsCurrent { get; set; }
        }
    }

}
