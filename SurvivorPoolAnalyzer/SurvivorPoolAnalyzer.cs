using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace SurvivorPoolAnalyzer
{
    internal class SurvivorPoolAnalyzer : IDisposable
    {

        private static readonly Lazy<SurvivorPoolAnalyzer>  TheInstance = new Lazy<SurvivorPoolAnalyzer>(() => new SurvivorPoolAnalyzer());
        internal static SurvivorPoolAnalyzer Instance {get { return TheInstance.Value; }}

        private readonly WebClient _webClient;
        private readonly List<SurvivorTeam> _teams;
        private readonly List<Matchup> _matchups;

        private const int PrizeTotal = 35000;

        static void Main()
        {
            Instance.Go();
        }

        private SurvivorPoolAnalyzer()
        {
            _webClient = new WebClient();
            _teams = new List<SurvivorTeam>();
            _matchups = new List<Matchup>();
        }

        internal void Go()
        {
            ScrapeWinPercentages();
            FillMatchups();

            OverrideNumPicks("SD", 166);
            OverrideNumPicks("PIT", 76);
            OverrideNumPicks("IND", 71);
            OverrideNumPicks("WAS", 5);
            OverrideNumPicks("ATL", 5);
            OverrideNumPicks("MIA", 3);
            OverrideNumPicks("DET", 1); 
            OverrideNumPicks("SF", 1);
            OverrideNumPicks("BAL", 1);

            CalculateEv();
        }

        internal void OverrideNumPicks(string team, int numPicks)
        {
            _teams.First(x => x.Name == team).NumPicks = numPicks;
        }

        internal void ScrapeWinPercentages()
        {
            _teams.Clear();
            _matchups.Clear();
            string fullHtml = _webClient.DownloadString("http://survivorgrid.com");
            string table = Regex.Match(fullHtml, @"<table id=""grid"".*?</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline).ToString();
            foreach (Match row in Regex.Matches(table, @"<tr id=.*?<td class=""dist"".*?<td class=""dist"">(.*?)%.*?<td class=""teamname"">(.*?)<.*?<td.*?>@?(.*?)<.*?</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
            {
                string name = row.Groups[2].ToString();
                string winPct = row.Groups[1].ToString();
                string opp = row.Groups[3].ToString();
                if (!string.IsNullOrEmpty(winPct))_teams.Add(new SurvivorTeam(name, opp, double.Parse(winPct) / 100));
            }
        }

        internal void FillMatchups()
        {
            foreach (SurvivorTeam team in _teams)
            {
                if (_matchups.Any(x => x.TeamA.Name == team.Name || x.TeamB.Name == team.Name)) continue;
                SurvivorTeam opp = _teams.First(x => x.Name == team.Opp);
                _matchups.Add(new Matchup(team, opp));
            }
        }

        internal void CalculateEv()
        {
            int numPlayers = _teams.Sum(x => x.NumPicks) + 1;
            double expectedSurvivors = _matchups.Sum(x => x.ExpectedSurvivors);
            double expectedElimPct = 100*(numPlayers - expectedSurvivors)/numPlayers;
            Console.WriteLine("Prize = {0}, Entries = {1}, Cur Value = {2}, Expected Elim% = {3}\n", PrizeTotal, numPlayers, Math.Round((double)PrizeTotal / numPlayers,2), Math.Round(expectedElimPct,2));

            foreach (SurvivorTeam team in _teams.OrderByDescending(x => x.WinPercentage))
            {
                team.IsPick = true;

                double expectedRemaining = _matchups.Where(x => !x.TeamA.IsPick && !x.TeamB.IsPick).Sum(x => x.ExpectedSurvivors);
                expectedRemaining += team.NumPicks + 1;
                team.ExpectedValue = team.WinPercentage * PrizeTotal / expectedRemaining;

                team.IsPick = false;
            }

            foreach (SurvivorTeam team in _teams.OrderByDescending(x => x.ExpectedValue)) Console.WriteLine(team);
        }

        public void Dispose()
        {
            if (_webClient != null) _webClient.Dispose();
        }

    }

    internal class SurvivorTeam
    {
        internal string Name { get; set; }
        internal string Opp { get; set; }
        internal double WinPercentage { get; set; }
        internal int NumPicks { get; set; }
        internal bool IsPick { get; set; }
        internal double ExpectedValue { get; set; }

        internal SurvivorTeam(string name, string opp, double winPercentage = 0, int numPicks = 0)
        {
            Name = name;
            Opp = opp;
            WinPercentage = winPercentage;
            NumPicks = numPicks;
            IsPick = false;
        }

        public override string ToString()
        {
            return string.Format("{0}\t{1}\t{2}\t{3}", Name, NumPicks, WinPercentage, ExpectedValue);
        }
    }

    internal class Matchup
    {
        internal SurvivorTeam TeamA { get; set; }
        internal SurvivorTeam TeamB { get; set; }

        internal double ExpectedSurvivors {
            get { return TeamA.WinPercentage*TeamA.NumPicks + TeamB.WinPercentage*TeamB.NumPicks; }
        }

        internal Matchup(SurvivorTeam teamA, SurvivorTeam teamB)
        {
            TeamA = teamA;
            TeamB = teamB;
        }
    }
}
