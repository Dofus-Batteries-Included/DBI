﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Com.Ankama.Dofus.Server.Game.Protocol.Treasure.Hunt;
using DofusBatteriesIncluded.Core;
using DofusBatteriesIncluded.Core.Protocol;
using Microsoft.Extensions.Logging;

namespace DofusBatteriesIncluded.TreasureSolver.Clues.Listeners;

public class SaveCluesOnDigAnswerEvent : IMessageListener<TreasureHuntEvent>, IMessageListener<TreasureHuntDigAnswerEvent>
{
    static readonly ILogger Log = DBI.Logging.Create<LocalCluesService>();
    int? _currentCheckpoint;
    List<ClueAtPosition> _clues = [];

    public Task HandleAsync(TreasureHuntDigAnswerEvent message)
    {
        if (!TreasureSolver.TryGetCluesService(out ICluesService cluesService))
        {
            Log.LogWarning("Could not get clues service, skipping Treasure Hunt Event.");
            return Task.CompletedTask;
        }

        if (message.Result is TreasureHuntDigAnswerEvent.Types.DigResult.NewHint or TreasureHuntDigAnswerEvent.Types.DigResult.Finished)
        {
            foreach (ClueAtPosition clue in _clues)
            {
                if (!clue.ClueId.HasValue)
                {
                    continue;
                }

                cluesService.RegisterCluesAsync(clue.MapId, new ClueWithStatus(clue.ClueId.Value, true));
            }
        }
        else if (message.Result is TreasureHuntDigAnswerEvent.Types.DigResult.Wrong or TreasureHuntDigAnswerEvent.Types.DigResult.WrongAndYouKnowIt
                 or TreasureHuntDigAnswerEvent.Types.DigResult.Lost)
        {
            foreach (ClueAtPosition clue in _clues)
            {
                if (!clue.ClueId.HasValue)
                {
                    continue;
                }


                if (clue.ValidationState.HasValue)
                {
                    cluesService.RegisterCluesAsync(clue.MapId, new ClueWithStatus(clue.ClueId.Value, clue.ValidationState.Value));
                }
            }
        }

        _currentCheckpoint = null;

        return Task.CompletedTask;
    }

    public Task HandleAsync(TreasureHuntEvent message)
    {
        if (_currentCheckpoint != null && message.CurrentCheckPoint != _currentCheckpoint.Value)
        {
            // sometimes dig events arrive after the hunt event of the next step
            // don't overwrite events of checkpoint until they are processed by the dig event handler
            return Task.CompletedTask;
        }

        _currentCheckpoint = message.CurrentCheckPoint;

        List<ClueAtPosition> newClues = new();
        for (int index = 0; index < message.Flags.Count; index++)
        {
            TreasureHuntEvent.Types.TreasureHuntStep step = message.KnownSteps.array[index];
            TreasureHuntEvent.Types.TreasureHuntFlag flag = message.Flags.array[index];

            bool? validation = flag.State switch
            {

                TreasureHuntEvent.Types.TreasureHuntFlag.Types.FlagState.Unknown => null,
                TreasureHuntEvent.Types.TreasureHuntFlag.Types.FlagState.Ok => true,
                TreasureHuntEvent.Types.TreasureHuntFlag.Types.FlagState.Wrong => false,
                _ => null
            };
            newClues.Add(new ClueAtPosition(flag.MapId, step.FollowDirectionToPoi?.PoiLabelId, validation));
        }

        bool isDigResult = _clues.Count(c => c.ValidationState == true)
                           != message.Flags.array.Take(message.Flags.Count).Count(f => f.State == TreasureHuntEvent.Types.TreasureHuntFlag.Types.FlagState.Ok);
        if (isDigResult && _clues.Count > message.Flags.Count)
        {
            // first unknown clue was wrong
            ClueAtPosition firstUnknownClue = _clues[message.Flags.Count];
            newClues.Add(firstUnknownClue with { ValidationState = false });
        }

        _clues = newClues;

        return Task.CompletedTask;
    }

    record struct ClueAtPosition(long MapId, int? ClueId, bool? ValidationState);
}
