﻿using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DofusBatteriesIncluded.Plugins.Core;
using DofusBatteriesIncluded.Plugins.TreasureSolver.Clients;
using Microsoft.Extensions.Logging;
using Direction = DofusBatteriesIncluded.Plugins.Core.Maps.Direction;

namespace DofusBatteriesIncluded.Plugins.TreasureSolver.Clues;

public class RemoteCluesService : ICluesService
{
    readonly ILogger _logger = DBI.Logging.Create<RemoteCluesService>();

    public async Task<long?> FindMapOfNextClue(long startMapId, Direction direction, int clueId, int cluesMaxDistance)
    {
        TreasureSolverClient treasureSolverClient = new(new HttpClient { Timeout = TimeSpan.FromSeconds(10) });
        Clients.Direction? mappedDirection = direction switch
        {
            Direction.Top => Clients.Direction.North,
            Direction.Right => Clients.Direction.East,
            Direction.Left => Clients.Direction.West,
            Direction.Bottom => Clients.Direction.South,
            _ => null
        };

        if (!mappedDirection.HasValue)
        {
            return null;
        }

        FindNextMapResponse result = await treasureSolverClient.FindNextClueAsync(
            new FindNextClueRequest { Start = new FindNodeByMap { MapId = startMapId }, Direction = mappedDirection.Value, ClueId = clueId }
        );
        return result.Found ? result.Map.MapId : null;
    }

    public async Task RegisterCluesAsync(long mapId, params ClueWithStatus[] clues)
    {
        try
        {
            await RegisterCluesAsync(mapId, clues, false);
        }
        catch (ApiException exn)
        {
            if (exn.StatusCode is 401 or 403)
            {
                _logger.LogWarning(exn, "Bad API token, will register again and retry.");

                try
                {
                    await RegisterCluesAsync(mapId, clues, true);
                }
                catch (Exception innerExn)
                {
                    _logger.LogError(innerExn, "Error while registering clues with remote server.");
                }
            }
            else
            {
                _logger.LogError(exn, "Error while registering clues with remote server.");
            }
        }
        catch (Exception exn)
        {
            _logger.LogError(exn, "Error while registering clues with remote server.");
        }
    }

    async Task RegisterCluesAsync(long mapId, ClueWithStatus[] clues, bool resetApiKey)
    {
        string apiKey = await GetApiKeyAsync(resetApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("Cannot register clues without API key.");
            return;
        }

        CluesClient cluesClient = new(new HttpClient { DefaultRequestHeaders = { { "Authorization", apiKey } }, Timeout = TimeSpan.FromSeconds(10) });
        await cluesClient.RegisterCluesAsync(
            new RegisterCluesRequest { Clues = clues.Select(c => new RegisterClueRequest { MapId = mapId, ClueId = c.ClueId, Found = c.IsPresent }).ToArray() }
        );
    }

    async Task<string> GetApiKeyAsync(bool resetApiKey)
    {
        const string storeKey = "treasure-solver-api-key";

        if (!resetApiKey)
        {
            string storedKey = await DBI.Store.GetValueAsync(MyPluginInfo.PLUGIN_NAME, storeKey);
            if (!string.IsNullOrWhiteSpace(storedKey))
            {
                return storedKey;
            }
        }

        if (DBI.Player.Account == null || DBI.Player.Account.AccountId == default || string.IsNullOrWhiteSpace(DBI.Player.Account.AccountNickname))
        {
            _logger.LogWarning("Account not set, cannot register new Treasure Hunt API account.");
            return null;
        }

        RegistrationClient registration = new(new HttpClient { Timeout = TimeSpan.FromSeconds(10) });
        Guid keyId = await registration.RegisterAsync(DBI.Player.Account.AccountId, DBI.Player.Account.AccountNickname);
        string key = keyId.ToString();
        await DBI.Store.SetValueAsync(MyPluginInfo.PLUGIN_NAME, storeKey, key);

        _logger.LogInformation("Registered current account ");

        return key;
    }
}
