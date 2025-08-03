#!/usr/bin/env dotnet

// Hey! This file is NOT a source code of the game, but a tool/script to generate soundmap.json file.
// You should run this file using ' dotnet run ./GenerateSoundMap.cs ' command in the terminal from the /Assets folder.
// :)
// Btw this requires .NET 10(preview 6+), but if you can run the game, you should have it installed already.

#:property JsonSerializerIsReflectionEnabledByDefault=true
#pragma warning disable

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

if (!Directory.Exists("Audio"))
{
    Console.WriteLine("No Audio folder. Put this file in [game root]/Assets folder");
    Thread.Sleep(5000);
    return;
}

var path = "Audio";
var files = Directory.GetFiles(path, "*.wav", System.IO.SearchOption.AllDirectories);
Dictionary<string, List<string>> map = new();

foreach (var file in files)
{
    // #IfItWorksDontTouchIt
    var f = Path.GetFileNameWithoutExtension(file).Replace(path, "").Replace(".wav", "").Reverse()
        .SkipWhile(char.IsDigit).Reverse().ToArray();
    var fileName = new string(Path.GetDirectoryName(file) + "\\" + new string(f)).Replace(path, "").Replace("\\", "/")[1..].TrimEnd('_').TrimEnd('-');
    if (!map.ContainsKey(fileName))
    {
        map[fileName] = new();
    }
    var assetsAudio = "Assets/Audio" + file.Replace(path, "").Replace("\\", "/");
    map[fileName].Add(assetsAudio);
    Console.WriteLine("{0,-30} {1,10}", fileName, assetsAudio);
}

var comparer = new NaturalComparer();

foreach (var sound in map)
{
    sound.Value.Sort(comparer);
}

File.WriteAllText("soundmap.json", JsonSerializer.Serialize(map));

class NaturalComparer : IComparer<string>
{
    private static readonly Regex _regex = new(@"\d+");

    public int Compare(string x, string y)
    {
        if (x == y)
            return 0;
        if (x == null)
            return -1;
        if (y == null)
            return 1;

        int ix = 0, iy = 0;

        while (ix < x.Length && iy < y.Length)
        {
            if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
            {
                var matchX = _regex.Match(x, ix);
                var matchY = _regex.Match(y, iy);

                if (!matchX.Success || !matchY.Success)
                    break;

                int numX = int.Parse(matchX.Value);
                int numY = int.Parse(matchY.Value);

                if (numX != numY)
                    return numX.CompareTo(numY);

                ix = matchX.Index + matchX.Length;
                iy = matchY.Index + matchY.Length;
            }
            else
            {
                int cmp = char.ToLowerInvariant(x[ix]).CompareTo(char.ToLowerInvariant(y[iy]));
                if (cmp != 0)
                    return cmp;

                ix++;
                iy++;
            }
        }

        return x.Length.CompareTo(y.Length);
    }
}