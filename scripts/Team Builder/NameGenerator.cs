using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class NameGenerator
{
    private static readonly Dictionary<string, List<string>> FirstNames = new();
    private static readonly Dictionary<string, List<string>> LastNames = new();

    private static List<string> TeamAdjectives = new();
    private static List<string> TeamNouns = new();

    public static void Load(string root = "C:/Projects/Godot/Proects/football-sim/scripts/Team Builder/Names/")
    {
        FirstNames.Clear();
        LastNames.Clear();

        // Teams
        TeamAdjectives = File.ReadAllLines(Path.Combine(root, "Teams", "adjectives.txt"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        TeamNouns = File.ReadAllLines(Path.Combine(root, "Teams", "nouns.txt"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        // Players
        string playersRoot = Path.Combine(root, "Players");

        foreach (string cultureDir in Directory.GetDirectories(playersRoot))
        {
            string culture = Path.GetFileName(cultureDir);

            FirstNames[culture] = File.ReadAllLines(Path.Combine(cultureDir, "first.txt"))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            LastNames[culture] = File.ReadAllLines(Path.Combine(cultureDir, "last.txt"))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }
    }

    public static string RandomTeamName()
    {
        return $"{Random(TeamAdjectives)} {Random(TeamNouns)}";
    }

    public static string RandomPlayerName(string culture)
    {
        if (!FirstNames.ContainsKey(culture))
            throw new Exception($"Unknown culture '{culture}'");

        return $"{Random(FirstNames[culture])} {Random(LastNames[culture])}";
    }

    public static string RandomPlayerName()
    {
        string culture = Random(FirstNames.Keys.ToList());
        return RandomPlayerName(culture);
    }

    private static T Random<T>(List<T> list)
    {
        return list[GD.RandRange(0, list.Count - 1)];
    }
}

public static class TeamColorGenerator
{
    public static Color GenerateRandomColor()
    {
        return new Color(GD.Randf(), GD.Randf(), GD.Randf());
    }

    public static Color GenerateDifferentColor(Color baseColor, float minDistance = .4f)
    {
        var newColor = new Color();
        float dist;
        do
        {
            newColor = GenerateRandomColor();

            dist = ColorDistance(baseColor, newColor);
        }
        while (dist < minDistance);

        return newColor;
    }

    public static float ColorDistance(Color a, Color b)
    {
        float dr = a.R - b.R;
        float dg = a.G - b.G;
        float db = a.B - b.B;

        return Mathf.Sqrt(dr * dr + dg * dg + db * db) / Mathf.Sqrt(3f);
    }
}