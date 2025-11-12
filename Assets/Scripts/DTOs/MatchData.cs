using System;

namespace DTOs
{
    [Serializable]
    public class MatchData : BaseDto
    {
        public int MatchId {get; set;}
        public string MatchName { get; set; }
        public int Entry { get; set; }
        public int Prize { get; set; }
    }
}