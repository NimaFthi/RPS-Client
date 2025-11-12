using System.Collections.Generic;

namespace DTOs
{
    public class InitData : BaseDto
    {
        public List<MatchData> AvailableMatchData { get; set; } = new();
    }
}