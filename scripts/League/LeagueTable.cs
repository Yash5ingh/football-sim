using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public class LeagueTable
{
    public List<LeagueEntry> Entries = new();

    public LeagueTable(IEnumerable<Club> clubs)
    {
        foreach (var club in clubs)
        {
            Entries.Add(new LeagueEntry()
            {
                Club = club
            });
        }
    }

    public void RegisterMatch(MatchResult result)
    {
        var home = Entries.First(e => e.Club == result.HomeTeam);
        var away = Entries.First(e => e.Club == result.AwayTeam);

        home.Played++;
        away.Played++;

        home.GoalsFor += result.HomeGoals;
        home.GoalsAgainst += result.AwayGoals;

        away.GoalsFor += result.AwayGoals;
        away.GoalsAgainst += result.HomeGoals;

        if (result.HomeGoals > result.AwayGoals)
        {
            home.Wins++;
            home.Points += 3;

            away.Losses++;
        }
        else if (result.HomeGoals < result.AwayGoals)
        {
            away.Wins++;
            away.Points += 3;

            home.Losses++;
        }
        else
        {
            home.Draws++;
            away.Draws++;

            home.Points++;
            away.Points++;
        }
    }

    public int GetClubRank(Club club)
    {
        var standings = Entries
            .OrderByDescending(x => x.Points)
            .ThenByDescending(x => x.GoalDifference)
            .ThenByDescending(x => x.GoalsFor)
            .ToList();

        for (int i = 0; i < standings.Count; i++)
        {
            if (standings[i].Club == club)
                return i + 1;
        }

        return -1;
    }

    public void Sort()
    {
        Entries = Entries
            .OrderByDescending(e => e.Points)
            .ThenByDescending(e => e.GoalDifference)
            .ThenByDescending(e => e.GoalsFor)
            .ToList();
    }

    public string GetTableString(string title = null)
    {
        Sort();
        var space = "   ";
        StringBuilder sb = new();

        if (!string.IsNullOrEmpty(title))
        {
            sb.AppendLine($"[center][b]{title}[/b][/center]");
            sb.AppendLine();
        }

        sb.AppendLine("[table=17]");

        // Header
        sb.AppendLine("[cell][b]R[/b][/cell]");
        sb.AppendLine($"[cell]{space}[/cell]");
        sb.AppendLine("[cell][b]C[/b][/cell]");
        sb.AppendLine($"[cell]{space}[/cell]");
        sb.AppendLine("[cell][b]Team[/b][/cell]");
        sb.AppendLine($"[cell]{space}[/cell]");
        sb.AppendLine("[cell][b]Pts[/b][/cell]");
        sb.AppendLine($"[cell]{space}[/cell]");
        sb.AppendLine("[cell][b]MP[/b][/cell]");
        sb.AppendLine($"[cell]{space}[/cell]");
        sb.AppendLine("[cell][b]W[/b][/cell]");
        sb.AppendLine($"[cell]{space}[/cell]");
        sb.AppendLine("[cell][b]D[/b][/cell]");
        sb.AppendLine($"[cell]{space}[/cell]");
        sb.AppendLine("[cell][b]L[/b][/cell]");
        sb.AppendLine($"[cell]{space}[/cell]");
        sb.AppendLine("[cell][b]GD[/b][/cell]");

        // Rows
        for (int i = 0; i < Entries.Count; i++)
        {
            var e = Entries[i];
            string color = e.Club.TeamColor.ToHtml();

            sb.AppendLine($"[cell]{i + 1}[/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine($"[cell][color=#{color}]●[/color][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine($"[cell]{e.Club.TeamName}[/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine($"[cell]{e.Points}[/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine($"[cell]{e.Played}[/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine($"[cell]{e.Wins}[/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine($"[cell]{e.Draws}[/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine($"[cell]{e.Losses}[/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine($"[cell]{e.GoalDifference}[/cell]");
        }

        sb.AppendLine("[/table]");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine();

        return sb.ToString();
    }
}

public class LeagueEntry
{
    public Club Club;

    public int Points;
    public int Played;

    public int Wins;
    public int Draws;
    public int Losses;

    public int GoalsFor;
    public int GoalsAgainst;

    public int GoalDifference => GoalsFor - GoalsAgainst;
}

public class MatchResult
{
    public Club HomeTeam;
    public Club AwayTeam;

    public int HomeGoals;
    public int AwayGoals;
}
