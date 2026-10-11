using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public partial class League : Node2D
{
    [Export] public int NumTeams = 20;
    [Export] public int RelegateTeams = 3;

    private const string BaseTeamsPath = "C:/Projects/Godot/Proects/football-sim/AI/Training Stage 6";
    private const string LeagueTeamsPath = "C:/Projects/Godot/Proects/football-sim/League/Teams";
    private const string RelegatedTeamsPath = "C:/Projects/Godot/Proects/football-sim/League/RelegatedTeams";
    private const string SeasonsPath = "C:/Projects/Godot/Proects/football-sim/League/Seasons";

    private List<Club> Teams = new();

    [Export] public PackedScene MatchScene;
    [Export] public RichTextLabel TableLabel;
    [Export] public RichTextLabel SeasonHistoryLabel;
    [Export] public RichTextLabel MatchDayLabel;
    [Export] public RichTextLabel PlayerStatsLabel;
    [Export] public RichTextLabel SquadsLabel;
    private int TeamSize;
    [Export] public float MatchTime = 120f;

    private List<Match> ActiveMatches = new();
    private List<MatchResult> RoundResults = new();

    Season season;

    [Export] public float FieldWidth = 90f;
    [Export] public float FieldHeight = 45f;
    [Export] public float GoalHeight = 10f;
    [Export] public float BoundsOffset = 5f;

    [Export] public int RenderFields = 1;
    private int _lastRenderFields;
    [Export] public bool SpeedSim = false;
    private bool _lastSpeedSim;

    [Export] public bool RetainHistory = true;

    [Export] public string FollowTeam = "";
    [Export] public int FollowPlayer = 0;
    [Export] public Camera2D Camera;
    [Export] public float CameraFollowSpeed = 8f;

    public override void _Ready()
    {
        NameGenerator.Load();
        CreateLeagueTeams();
        TeamSize = TrainingParameters.TeamSize;

        season = new Season(Teams, 1);
        TableLabel.Text = season.Table.GetTableString($"Season {season.SeasonNumber}");
        SquadsLabel.Text = GetClubSquads(season);
        
        UpdateSpeedSim();
        _lastSpeedSim = SpeedSim;

        StartNextMatchDay();
    }

    public override void _Process(double delta)
    {
        if (_lastSpeedSim != SpeedSim)
        {
            _lastSpeedSim = SpeedSim;
            UpdateSpeedSim();
        }

        if (ActiveMatches.Count == 0)
            return;

        UpdateRenderFields();

        MatchDayLabel.Text = GetMatchDayString();

        if (!SpeedSim)
            PlayerStatsLabel.Text = GetPlayerStats(Teams);

        UpdateCamera((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (ActiveMatches.Count == 0)
            return;

        bool finished = true;
        foreach (var match in ActiveMatches)
        {
            if (match.TimeLeft > 0)
            {
                finished = false;
                break;
            }
        }

        if (!finished)
            return;

        RoundResults.Clear();

        foreach (var match in ActiveMatches)
        {
            RoundResults.Add(new MatchResult()
            {
                HomeTeam = match.HomeTeam,
                AwayTeam = match.AwayTeam,
                HomeGoals = match.leftGoals,
                AwayGoals = match.rightGoals
            });

            if (match.leftGoals > match.rightGoals)
            {
                foreach (var p in match.HomeTeam.Players)
                    p.Wins++;

                foreach (var p in match.AwayTeam.Players)
                    p.Losses++;
            }
            else if (match.leftGoals < match.rightGoals)
            {
                foreach (var p in match.HomeTeam.Players)
                    p.Losses++;

                foreach (var p in match.AwayTeam.Players)
                    p.Wins++;
            }
            else
            {
                foreach (var p in match.HomeTeam.Players)
                    p.Draws++;

                foreach (var p in match.AwayTeam.Players)
                    p.Draws++;
            }

            match.QueueFree();
        }

        ActiveMatches.Clear();

        season.RegisterRound(RoundResults);
        TableLabel.Text = season.Table.GetTableString($"Season {season.SeasonNumber}");

        if (season.Finished)
        {
            GD.Print($"Season {season.SeasonNumber} Finished");
            if (RetainHistory)
                SaveSeason(season);

            IncrementAge();
            RelegateAndPromote();
            ResetPlayersStats();

            SeasonHistoryLabel.Text += season.Table.GetTableString($"Season {season.SeasonNumber}");

            season = new Season(
                Teams,
                season.SeasonNumber + 1);
            TableLabel.Text = season.Table.GetTableString($"Season {season.SeasonNumber}");
        }

        if (!SpeedSim)
            SquadsLabel.Text = GetClubSquads(season);
        StartNextMatchDay();
    }

    private void StartNextMatchDay()
    {
        foreach (var match in ActiveMatches)
            match.QueueFree();

        ActiveMatches.Clear();

        int cols = Mathf.CeilToInt(Mathf.Sqrt(season.CurrentFixtures.Count));

        for (int i = 0; i < season.CurrentFixtures.Count; i++)
        {
            var fixture = season.CurrentFixtures[i];

            var match = MatchScene.Instantiate<Match>();

            match.Visible = i < RenderFields;

            int row = i / cols;
            int col = i % cols;

            match.Position = WorldParameters.Meters(new Vector2(
                col * (FieldWidth + BoundsOffset * 2 + 4),
                row * (FieldHeight + BoundsOffset * 2 + 4)
            ));

            match.HomeTeam = fixture.Home;
            match.AwayTeam = fixture.Away;

            match.HomeTeamRank = season.Table.GetClubRank(fixture.Home);
            match.AwayTeamRank = season.Table.GetClubRank(fixture.Away);

            match.Width = FieldWidth;
            match.Height = FieldHeight;
            match.GoalHeight = GoalHeight;
            match.BoundsOffset = BoundsOffset;

            match.TeamSize = TeamSize;
            match.MatchTime = MatchTime;
            match.TimeLeft = MatchTime;

            AddChild(match);

            ActiveMatches.Add(match);
        }
    }

    private void CreateLeagueTeams()
    {
        Teams.Clear();

        DirAccess.MakeDirRecursiveAbsolute(LeagueTeamsPath);

        //---------------------------------------
        // STEP 1 : Load existing clubs
        //---------------------------------------

        foreach (string clubDir in Directory.GetDirectories(LeagueTeamsPath))
        {
            if (Teams.Count >= NumTeams)
                break;

            Teams.Add(Club.Load(clubDir, TrainingParameters.MutationChance, TrainingParameters.MutationAmount));
        }

        if (Teams.Count >= NumTeams)
            return;

        //---------------------------------------
        // STEP 2 : Create clubs from Stage 6
        //---------------------------------------

        var baseTeams = Directory.GetDirectories(BaseTeamsPath)
            .OrderBy(_ => GD.Randf())
            .ToList();

        foreach (string teamDir in baseTeams)
        {
            if (Teams.Count >= NumTeams)
                break;

            AgentStage6 agent = AgentStage6.Load(
                teamDir,
                TrainingParameters.MutationChance,
                TrainingParameters.MutationAmount);

            Club club = new Club(agent);

            club.Save(LeagueTeamsPath);

            Teams.Add(club);
        }

        if (Teams.Count >= NumTeams)
            return;

        //---------------------------------------
        // STEP 3 : Fill remaining with mutations
        //---------------------------------------

        while (Teams.Count < NumTeams)
        {
            string teamDir1 = baseTeams[(int)GD.RandRange(0, baseTeams.Count - 1)];
            string teamDir2 = baseTeams[(int)GD.RandRange(0, baseTeams.Count - 1)];

            AgentStage6 agent1 = AgentStage6.Load(
                teamDir1,
                TrainingParameters.MutationChance,
                TrainingParameters.MutationAmount);

            AgentStage6 agent2 = AgentStage6.Load(
                teamDir2,
                TrainingParameters.MutationChance,
                TrainingParameters.MutationAmount);

            AgentStage6 agent = agent1.CopyWithCrossover(agent2);

            Club club = new Club(agent);

            club.Save(LeagueTeamsPath);

            Teams.Add(club);
        }
    }

    private void RelegateAndPromote()
    {
        string seasonFolder = "";

        if (RetainHistory)
        {
            seasonFolder = Path.Combine(
                RelegatedTeamsPath,
                $"Season_{season.SeasonNumber}");

            DirAccess.MakeDirRecursiveAbsolute(seasonFolder);
        }

        //-----------------------------------
        // Build player pool BEFORE relegation
        //-----------------------------------

        var rankedPlayers = Teams
            .SelectMany(c => c.Players)
            .OrderByDescending(p => p.PlayerScore)
            .ToList();

        //-----------------------------------
        // Existing clubs sign one new player
        //-----------------------------------

        foreach (var club in Teams)
        {
            var worstPlayer = club.Players
                .OrderBy(p => p.PlayerScore)
                .First();

            ClubPlayer parent;
            do
            {
                parent = TournamentSelect(rankedPlayers);
            }
            while (parent.OwnerClub == club);

            ClubPlayer replacement = ClubPlayer.CreateMutatedClone(parent);

            club.ReplacePlayer(worstPlayer, replacement);

            club.Save(LeagueTeamsPath);
        }

        //-----------------------------------
        // Create promoted clubs
        //-----------------------------------

        List<Club> promoted = new();

        for (int i = 0; i < RelegateTeams; i++)
        {
            HashSet<ClubPlayer> selected = new();

            while (selected.Count < TeamSize)
                selected.Add(TournamentSelect(rankedPlayers));

            Club club = new Club(selected.ToList());

            club.Save(LeagueTeamsPath);

            promoted.Add(club);
        }

        //-----------------------------------
        // Bottom teams
        //-----------------------------------

        var relegated = season.Table.Entries
            .OrderByDescending(x => x.Points)
            .ThenByDescending(x => x.GoalDifference)
            .ThenByDescending(x => x.GoalsFor)
            .TakeLast(RelegateTeams)
            .Select(x => x.Club)
            .ToList();

        //-----------------------------------
        // Move relegated clubs
        //-----------------------------------

        foreach (var club in relegated)
        {
            string oldFolder = Path.Combine(
                LeagueTeamsPath,
                club.TeamName);

            if (RetainHistory)
            {
                string newFolder = Path.Combine(
                    seasonFolder,
                    club.TeamName);

                if (Directory.Exists(newFolder))
                    Directory.Delete(newFolder, true);

                if (Directory.Exists(oldFolder))
                    Directory.Move(oldFolder, newFolder);
            }
            else
            {
                if (Directory.Exists(oldFolder))
                    Directory.Delete(oldFolder, true);
            }

            Teams.Remove(club);
        }

        //-----------------------------------
        // Add promoted clubs
        //-----------------------------------

        Teams.AddRange(promoted);
    }

    private void IncrementAge()
    {
        foreach (var club in Teams)
        {
            club.IncrementClubAge();
        }
    }

    private void ResetPlayersStats()
    {
        foreach (var club in Teams)
        {
            foreach (var player in club.Players)
            {
                player.ResetSeasonStats();
            }
        }
    }

    private ClubPlayer TournamentSelect(List<ClubPlayer> players, int selection = 3)
    {
        int best = GD.RandRange(0, players.Count - 1);

        for (int i = 1; i < selection; i++)
        {
            int challenger = GD.RandRange(0, players.Count - 1);

            if (players[challenger].PlayerScore > players[best].PlayerScore)
                best = challenger;
        }

        return players[best];
    }

    private void UpdateSpeedSim()
    {
        PlayerStatsLabel.Visible = !SpeedSim;
        SquadsLabel.Visible = !SpeedSim;

        if (SpeedSim)
        {
            PlayerStatsLabel.Text = "";
            SquadsLabel.Text = "";
        }
        else
        {
            PlayerStatsLabel.Text = GetPlayerStats(Teams);
            SquadsLabel.Text = GetClubSquads(season);
        }
    }

    private void UpdateRenderFields()
    {
        if (RenderFields != _lastRenderFields)
        {
            for(int i = 0; i < ActiveMatches.Count; i++)
            {
                ActiveMatches[i].Visible = i+1 <= RenderFields;
            }
        }
        _lastRenderFields = RenderFields;
    }

    private string GetMatchDayString()
    {
        var space = "   ";
        StringBuilder sb = new();

        sb.AppendLine($"[center][b]Matchday {season.CurrentMatchDay + 1}[/b][/center]");
        sb.AppendLine($"[center]{ActiveMatches[0].DisplayTime}[/center]");
        sb.AppendLine();

        sb.AppendLine("[table=9]");

        foreach (var match in ActiveMatches)
        {
            sb.AppendLine($"[cell][right]{match.HomeTeam.TeamName}[/right][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");

            sb.AppendLine($"[cell]{match.leftGoals}[/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");

            sb.AppendLine("[cell]-[/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");

            sb.AppendLine($"[cell]{match.rightGoals}[/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");

            sb.AppendLine($"[cell]{match.AwayTeam.TeamName}[/cell]");
        }

        sb.AppendLine("[/table]");

        return sb.ToString();
    }

    public static string GetPlayerStats(List<Club> clubs)
    {
        const string space = "   ";
        StringBuilder sb = new();

        var players = clubs.SelectMany(c => c.Players).ToList();

        void AddLeaderboard(string title, string statName, Func<ClubPlayer, int> selector, int num = 10)
        {
            sb.AppendLine($"[center][b]{title}[/b][/center]");
            sb.AppendLine();

            sb.AppendLine("[table=7]");

            sb.AppendLine("[cell][b]R[/b][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine("[cell][b]Player[/b][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine("[cell][b]Club[/b][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine($"[cell][b]{statName}[/b][/cell]");

            int rank = 1;

            foreach (var p in players
                .OrderByDescending(selector)
                .ThenBy(p => p.Name)
                .Take(num))
            {
                sb.AppendLine($"[cell]{rank++}[/cell]");
                sb.AppendLine($"[cell]{space}[/cell]");
                sb.AppendLine($"[cell]{p.Number}. {p.Name}[/cell]");
                sb.AppendLine($"[cell]{space}[/cell]");
                sb.AppendLine($"[cell]{p.OwnerClub.TeamName}[/cell]");
                sb.AppendLine($"[cell]{space}[/cell]");
                sb.AppendLine($"[cell]{selector(p)}[/cell]");
            }

            sb.AppendLine("[/table]");
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine();
        }

        AddLeaderboard("Best Players", "Score", p => (int)p.PlayerScore, 20);
        AddLeaderboard("Top Scorers", "G", p => p.GoalsScored);
        AddLeaderboard("Top Assists", "A", p => p.Assists);
        AddLeaderboard("Most Passes", "P", p => p.Passes);
        AddLeaderboard("Pass Distance", "D", p => (int)p.PassDistance);
        AddLeaderboard("Dribble Distance", "DB", p => (int)p.DribbleDistance);
        AddLeaderboard("Most Steals", "S", p => p.Steals);
        AddLeaderboard("Most Touches", "T", p => p.Touches);
        AddLeaderboard("Most Own Goals", "OG", p => p.OwnGoals);
        AddLeaderboard("Most Ball Losses", "BL", p => p.BallLost);

        return sb.ToString();
    }

    public static string GetClubSquads(Season season)
    {
        const string space = "   ";
        StringBuilder sb = new();

        var standings = season.Table.Entries
            .OrderByDescending(x => x.Points)
            .ThenByDescending(x => x.GoalDifference)
            .ThenByDescending(x => x.GoalsFor)
            .ToList();

        for (int rank = 0; rank < standings.Count; rank++)
        {
            var entry = standings[rank];
            var club = entry.Club;

            string color = club.TeamColor.ToHtml();

            sb.AppendLine($"[center][b]{rank + 1}. [color=#{color}]●[/color] {club.TeamName} ({entry.Points} pts)[/b][/center]");
            sb.AppendLine($"[center]Age: {club.Age}[/center]");
            sb.AppendLine();

            sb.AppendLine("[table=21]");

            sb.AppendLine("[cell][b]#[/b][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine("[cell][b]Player[/b][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine("[cell][b]Age[/b][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine("[cell][b]Score[/b][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine("[cell][b]G[/b][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine("[cell][b]A[/b][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine("[cell][b]P[/b][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine("[cell][b]S[/b][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine("[cell][b]T[/b][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine("[cell][b]OG[/b][/cell]");
            sb.AppendLine($"[cell]{space}[/cell]");
            sb.AppendLine("[cell][b]BL[/b][/cell]");

            foreach (var p in club.Players.OrderByDescending(x => x.PlayerScore))
            {
                sb.AppendLine($"[cell]{p.Number}[/cell]");
                sb.AppendLine($"[cell]{space}[/cell]");
                sb.AppendLine($"[cell]{p.Name}[/cell]");
                sb.AppendLine($"[cell]{space}[/cell]");
                sb.AppendLine($"[cell]{p.Age}[/cell]");
                sb.AppendLine($"[cell]{space}[/cell]");
                sb.AppendLine($"[cell]{p.PlayerScore:0}[/cell]");
                sb.AppendLine($"[cell]{space}[/cell]");
                sb.AppendLine($"[cell]{p.GoalsScored}[/cell]");
                sb.AppendLine($"[cell]{space}[/cell]");
                sb.AppendLine($"[cell]{p.Assists}[/cell]");
                sb.AppendLine($"[cell]{space}[/cell]");
                sb.AppendLine($"[cell]{p.Passes}[/cell]");
                sb.AppendLine($"[cell]{space}[/cell]");
                sb.AppendLine($"[cell]{p.Steals}[/cell]");
                sb.AppendLine($"[cell]{space}[/cell]");
                sb.AppendLine($"[cell]{p.Touches}[/cell]");
                sb.AppendLine($"[cell]{space}[/cell]");
                sb.AppendLine($"[cell]{p.OwnGoals}[/cell]");
                sb.AppendLine($"[cell]{space}[/cell]");
                sb.AppendLine($"[cell]{p.BallLost}[/cell]");
            }

            sb.AppendLine("[/table]");
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private void SaveSeason(Season season)
    {
        DirAccess.MakeDirRecursiveAbsolute(SeasonsPath);

        string path = Path.Combine(
            SeasonsPath,
            $"Season {season.SeasonNumber}.csv");

        using StreamWriter writer = new(path);

        writer.WriteLine(
            "Pos,Club,Played,Wins,Draws,Losses,GF,GA,GD,Points");

        var standings = season.Table.Entries
            .OrderByDescending(x => x.Points)
            .ThenByDescending(x => x.GoalDifference)
            .ThenByDescending(x => x.GoalsFor)
            .ToList();

        for (int i = 0; i < standings.Count; i++)
        {
            var e = standings[i];

            writer.WriteLine(
                $"{i + 1}," +
                $"{e.Club.TeamName}," +
                $"{e.Played}," +
                $"{e.Wins}," +
                $"{e.Draws}," +
                $"{e.Losses}," +
                $"{e.GoalsFor}," +
                $"{e.GoalsAgainst}," +
                $"{e.GoalDifference}," +
                $"{e.Points}");
        }

        GD.Print($"Saved season to {path}");
    }

    private void UpdateCamera(float delta)
    {
        if (Camera == null)
            return;

        // Empty = free camera
        if (string.IsNullOrWhiteSpace(FollowTeam))
            return;

        Match match = null;
        bool homeTeam = false;

        //------------------------------------
        // Find the match containing the team
        //------------------------------------

        foreach (var m in ActiveMatches)
        {
            if (string.Equals(m.HomeTeam.TeamName, FollowTeam, StringComparison.OrdinalIgnoreCase))
            {
                match = m;
                homeTeam = true;
                break;
            }

            if (string.Equals(m.AwayTeam.TeamName, FollowTeam, StringComparison.OrdinalIgnoreCase))
            {
                match = m;
                homeTeam = false;
                break;
            }
        }

        if (match == null)
            return;

        Vector2 target;

        //------------------------------------
        // Follow Team
        //------------------------------------

        if (FollowPlayer <= 0)
        {
            target = (match.GlobalPosition + match.Ball.GlobalPosition) * 0.5f;
        }

        //------------------------------------
        // Follow player
        //------------------------------------

        else
        {
            var players = homeTeam
                ? match.HomeTeamPlayers
                : match.AwayTeamPlayers;

            int index = FollowPlayer - 1;

            if (index < 0 || index >= players.Count)
                return;

            target = players[index].Body.GlobalPosition;
        }

        //------------------------------------
        // Smooth movement
        //------------------------------------

        Camera.GlobalPosition = Camera.GlobalPosition.Lerp(
            target,
            CameraFollowSpeed * delta);
    }
}


public class Season
{
    public int SeasonNumber;

    public LeagueTable Table;

    public List<List<Fixture>> MatchDays = new();

    public int CurrentMatchDay;

    public bool Finished =>
        CurrentMatchDay >= MatchDays.Count;

    public Season(List<Club> clubs, int seasonNumber)
    {
        SeasonNumber = seasonNumber;

        Table = new LeagueTable(clubs);

        GenerateFixtures(clubs);
    }

    private void GenerateFixtures(List<Club> clubs)
    {
        MatchDays.Clear();

        var rotation = new List<Club>(clubs);

        if (rotation.Count % 2 == 1)
            rotation.Add(null);

        int n = rotation.Count;
        int rounds = n - 1;

        //-----------------------
        // First half
        //-----------------------

        for (int round = 0; round < rounds; round++)
        {
            List<Fixture> fixtures = new();

            for (int i = 0; i < n / 2; i++)
            {
                Club home = rotation[i];
                Club away = rotation[n - 1 - i];

                if (home != null && away != null)
                {
                    if (round % 2 == 0)
                        fixtures.Add(new Fixture(home, away));
                    else
                        fixtures.Add(new Fixture(away, home));
                }
            }

            MatchDays.Add(fixtures);

            Club last = rotation[n - 1];

            rotation.RemoveAt(n - 1);
            rotation.Insert(1, last);
        }

        //-----------------------
        // Second half
        //-----------------------

        int firstHalf = MatchDays.Count;

        for (int i = 0; i < firstHalf; i++)
        {
            List<Fixture> reverse = new();

            foreach (var fixture in MatchDays[i])
            {
                reverse.Add(new Fixture(
                    fixture.Away,
                    fixture.Home));
            }

            MatchDays.Add(reverse);
        }
    }

    public void RegisterRound(List<MatchResult> results)
    {
        foreach (var result in results)
        {
            Table.RegisterMatch(result);
        }

        CurrentMatchDay++;
    }

    public List<Fixture> CurrentFixtures
    {
        get
        {
            if (Finished)
                return null;

            return MatchDays[CurrentMatchDay];
        }
    }
}

public class Fixture
{
    public Club Home;
    public Club Away;

    public bool Played;

    public Fixture(Club home, Club away)
    {
        Home = home;
        Away = away;
    }
}