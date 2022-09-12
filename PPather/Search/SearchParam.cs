using PPather.Graph;

namespace PPather
{
    public sealed class SearchParam
    {
        public string Continent { get; set; }
        public PathGraph.eSearchScoreSpot SearchType { get; set; }
        public SearchLocation From { get; set; }
        public SearchLocation To { get; set; }
    }
}
