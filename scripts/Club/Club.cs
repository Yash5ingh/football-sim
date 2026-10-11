using Godot;
using System;
using System.Collections.Generic;

public class Club
{
	public Guid Id;
	public string TeamName;
	public List<ClubPlayer> Players = new();
	public Color TeamColor;
	public Color NumberColor;

    public Color AwayTeamColor;
    public Color AwayNumberColor;

    public int Age;

    public Club()
    {
    }
    
    public Club(AgentStage6 baseAgent)
	{
		SetFreshTeamIdentity();

        foreach (var player in baseAgent.Players)
		{
			var newPlayer = new ClubPlayer(player);
            newPlayer.OwnerClub = this;
            newPlayer.Number = Players.Count + 1;
            Players.Add(newPlayer);
        }
    }

    public Club(List<ClubPlayer> ExistingPlayers) 
    {
        SetFreshTeamIdentity();

        foreach(var ExistingPlayer in ExistingPlayers)
        {
            var newPlayer = ClubPlayer.CreateMutatedClone(ExistingPlayer);
            newPlayer.OwnerClub = this;
            newPlayer.Number = Players.Count + 1;
            Players.Add(newPlayer);
        }
    }

    public void IncrementClubAge()
    {
        Age++;
        foreach (var player in Players)
        {
            player.Age++;
        }
    }

    private void SetFreshTeamIdentity()
    {
        Id = Guid.NewGuid();
        TeamName = NameGenerator.RandomTeamName();
        Age = 1;
        TeamColor = TeamColorGenerator.GenerateRandomColor();
        NumberColor = TeamColorGenerator.GenerateDifferentColor(TeamColor);
        AwayTeamColor = TeamColorGenerator.GenerateDifferentColor(TeamColor);
        AwayNumberColor = TeamColorGenerator.GenerateDifferentColor(AwayTeamColor);
    }

    public void ReplacePlayer(ClubPlayer oldPlayer, ClubPlayer newPlayer)
    {
        int index = Players.IndexOf(oldPlayer);

        if (index == -1)
            throw new Exception("Player not found.");

        newPlayer.OwnerClub = this;
        newPlayer.Number = oldPlayer.Number;

        Players[index] = newPlayer;
    }

    public void Save(string folder)
    {
        DirAccess.MakeDirRecursiveAbsolute($"{folder}/{TeamName}");

        string clubFolder = $"{folder}/{TeamName}";

        using var file = FileAccess.Open(
            $"{clubFolder}/ClubInfo.txt",
            FileAccess.ModeFlags.Write);

        file.StoreLine($"ID={Id}");
        file.StoreLine($"NAME={TeamName}");
        file.StoreLine($"TEAM_COLOR={TeamColor.ToHtml()}");
        file.StoreLine($"NUMBER_COLOR={NumberColor.ToHtml()}");
        file.StoreLine($"AWAY_TEAM_COLOR={AwayTeamColor.ToHtml()}");
        file.StoreLine($"AWAY_NUMBER_COLOR={AwayNumberColor.ToHtml()}");
        file.StoreLine($"PLAYERS={Players.Count}");
        file.StoreLine($"AGE={Age}");

        for (int i = 0; i < Players.Count; i++)
            Players[i].Save($"{clubFolder}/Player{i + 1}");
    }

    public static Club Load(string folder, float mutationChance , float mutationAmt)
    {
        Guid id = Guid.Empty;
        string name = "";
        Color teamColor = Colors.White;
        Color numberColor = Colors.Black;
        Color awayTeamColor = Colors.White;
        Color awayNumberColor = Colors.Black;

        int numPlayers = 0;
        int age = 0;
        using var file = FileAccess.Open(
            $"{folder}/ClubInfo.txt",
            FileAccess.ModeFlags.Read);

        while (!file.EofReached())
        {
            var line = file.GetLine();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var split = line.Split('=');

            if (split.Length != 2)
                continue;

            switch (split[0])
            {
                case "ID":
                    id = Guid.Parse(split[1]);
                    break;

                case "NAME":
                    name = split[1];
                    break;

                case "TEAM_COLOR":
                    teamColor = Color.FromString(split[1], Colors.White);
                    break;

                case "NUMBER_COLOR":
                    numberColor = Color.FromString(split[1], Colors.Black);
                    break;

                case "AWAY_TEAM_COLOR":
                    awayTeamColor = Color.FromString(split[1], Colors.White);
                    break;

                case "AWAY_NUMBER_COLOR":
                    awayNumberColor = Color.FromString(split[1], Colors.Black);
                    break;

                case "PLAYERS":
                    numPlayers = int.Parse(split[1]);
                    break;

                case "AGE":
                    age = int.Parse(split[1]);
                    break;
            }
        }

        Club club = new Club();

        club.Id = id;
        club.TeamName = name;
        club.TeamColor = teamColor;
        club.NumberColor = numberColor;
        club.AwayTeamColor = awayTeamColor;
        club.AwayNumberColor = awayNumberColor;
        club.Age = age;
        club.Players = new();

        for (int i = 0; i < numPlayers; i++)
        {
            club.Players.Add(
                ClubPlayer.Load($"{folder}/Player{i + 1}", mutationChance, mutationAmt)
            );
        }
        foreach (var player in club.Players)
        {
            player.OwnerClub = club;
            player.Number = club.Players.IndexOf(player) + 1;
        }
        return club;
    }
}
