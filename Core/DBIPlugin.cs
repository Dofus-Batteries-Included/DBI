﻿using System;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Microsoft.Extensions.Logging;

namespace DofusBatteriesIncluded.Core;

// ReSharper disable once InconsistentNaming
public abstract class DBIPlugin : BasePlugin
{
    protected new ILogger Log { get; } = DBI.Logging.Create(typeof(DBIPlugin));

    public DBIPlugin()
    {
        BepInPlugin pluginAttribute = GetType().GetCustomAttribute<BepInPlugin>();
        if (pluginAttribute == null)
        {
            throw new InvalidOperationException("Expected plugin to have a [BepInPlugin] attrribute.");
        }

        Id = pluginAttribute.GUID;
        Name = pluginAttribute.Name;
        Version = pluginAttribute.Version?.ToString();
    }

    public string Id { get; }
    public string Name { get; }
    public string Version { get; }

    public virtual bool CanBeDisabled => true;

    public override sealed void Load()
    {
        if (!DBI.Enabled)
        {
            Log.LogInformation("Dofus Batteries Included is disabled.");
            return;
        }

        DBI.Plugins.Register(this);

        if (CanBeDisabled)
        {
            bool enabled = DBI.Configuration.Configure(Name, "Enabled", true).WithDescription($"Enable plugin {Name}").Hide().Bind();

            if (!enabled)
            {
                Log.LogInformation("{Name} is disabled.", Name);
                return;
            }
        }

        Start();
    }

    protected abstract void Start();
}
