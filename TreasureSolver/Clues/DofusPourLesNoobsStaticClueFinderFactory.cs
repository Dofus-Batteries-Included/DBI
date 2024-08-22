﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Core.DataCenter;
using Core.DataCenter.Metadata.Quest.TreasureHunt;
using DofusBatteriesIncluded.Core;
using DofusBatteriesIncluded.Core.Maps;
using Microsoft.Extensions.Logging;

namespace DofusBatteriesIncluded.TreasureSolver.Clues;

public static class DofusPourLesNoobsStaticClueFinderFactory
{
    static readonly ILogger Log = DBI.Logging.Create(typeof(DofusPourLesNoobsStaticClueFinderFactory));

    public static async Task<StaticClueFinder> Create(string dplbFilePath)
    {
        List<PointOfInterest> gamePois = [];
        foreach (PointOfInterest poi in DataCenterModule.pointOfInterestRoot.GetObjects())
        {
            gamePois.Add(poi);
        }

        await using FileStream stream = File.OpenRead(dplbFilePath);
        DPLBFile parsedFile = await JsonSerializer.DeserializeAsync<DPLBFile>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Dictionary<int, int> dplbClueToGameClueMapping = new();
        foreach (PointOfInterest gameClue in gamePois)
        {
            ClueNames dplbClue = parsedFile.Clues.FirstOrDefault(c => gameClue.name == c.HintFr || gameClue.name == c.HintEn || gameClue.name == c.HintEs);
            if (dplbClue is null)
            {
                Log.LogWarning("Could not find clue {Name} ({Id}) in DPLB file.", gameClue.name, gameClue.id);
                continue;
            }

            dplbClueToGameClueMapping[dplbClue.ClueId] = gameClue.id;
        }

        Dictionary<Position, IReadOnlyCollection<int>> clues = parsedFile.Maps.ToDictionary(
            m => new Position(m.X, m.Y),
            IReadOnlyCollection<int> (m) => m.Clues.Where(c => dplbClueToGameClueMapping.ContainsKey(c)).Select(c => dplbClueToGameClueMapping[c]).ToHashSet()
        );

        return new StaticClueFinder(clues);
    }

    // ReSharper disable once InconsistentNaming
    class DPLBFile
    {
        public ClueNames[] Clues { get; set; }
        public MapClues[] Maps { get; set; }
    }

    class ClueNames
    {
        public int ClueId { get; set; }
        public string HintFr { get; set; }
        public string HintEn { get; set; }
        public string HintEs { get; set; }
    }

    class MapClues
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int[] Clues { get; set; }
    }
}
