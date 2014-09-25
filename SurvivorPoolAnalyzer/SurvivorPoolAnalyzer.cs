﻿using System;
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
            Random rand = new Random();
            ScrapeWinPercentages();
            FillMatchups();

            //foreach (SurvivorTeam team in _teams) team.NumPicks = rand.Next(0, 100);
            OverrideNumPicks("SD", 588);
            OverrideNumPicks("PIT", 192);
            OverrideNumPicks("IND", 162);
            OverrideNumPicks("ATL", 16);
            OverrideNumPicks("MIA", 10);
            OverrideNumPicks("WAS", 10);
            OverrideNumPicks("SF", 6);
            OverrideNumPicks("BAL", 3);
            OverrideNumPicks("HOU", 3);
            OverrideNumPicks("DET", 3);
            OverrideNumPicks("NE", 2);
            OverrideNumPicks("NO", 2);
            OverrideNumPicks("GB", 1);
            OverrideNumPicks("NYG", 1);
            OverrideNumPicks("JAX", 1);
            OverrideNumPicks("OAK", 1);

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
            foreach (SurvivorTeam team in _teams.OrderByDescending(x => x.WinPercentage))
            {
                team.IsPick = true;

                double expectedRemaining = 0;
                foreach (Matchup matchup in _matchups.Where(x => !x.TeamA.IsPick && !x.TeamB.IsPick))
                {
                    double eremAWins = matchup.TeamA.WinPercentage * matchup.TeamA.NumPicks;
                    double eremBWins = matchup.TeamB.WinPercentage * matchup.TeamB.NumPicks;
                    expectedRemaining += eremAWins + eremBWins;
                }

                Matchup pickMatchup = _matchups.First(x => x.TeamA.IsPick || x.TeamB.IsPick);
                SurvivorTeam opp = pickMatchup.TeamA == team ? pickMatchup.TeamB : pickMatchup.TeamA;
                expectedRemaining += team.NumPicks + 1;
                double ev = team.WinPercentage*numPlayers/expectedRemaining;
                team.ExpectedValue = ev;

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

        internal Matchup(SurvivorTeam teamA, SurvivorTeam teamB)
        {
            TeamA = teamA;
            TeamB = teamB;
        }
    }
}
