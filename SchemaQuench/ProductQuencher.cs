using log4net;
using Schema.DataAccess;
using Schema.Domain;
using Schema.Isolators;
using Schema.Utility;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace SchemaQuench;

public class ProductQuencher
{
    private readonly IConfigurationRoot _config = FactoryContainer.ResolveOrCreate<IConfigurationRoot>();
    private readonly ILog _errorLog = LogFactory.GetLogger("ErrorLog");
    private readonly ILog _progressLog = LogFactory.GetLogger("ProgressLog");
    private readonly Product _product = Product.Load();

    private readonly string _whatIfOnly;
    private readonly string _primaryServer;

    public ProductQuencher()
    {
        _whatIfOnly = _config["WhatIfONLY"]?.ToLower() == "true" ? "1" : "0";
        _primaryServer = _config["Target:Server"] ?? "localhost";
    }

    private IDbConnection GetConnection(string dbName)
    {
        var connectionString = ConnectionString.Build(_config["Target:Server"], dbName, _config["Target:User"], _config["Target:Password"]);
        var connection = SqlConnectionFactory.GetFromFactory().GetSqlConnection(connectionString);

        connection.Open();
        return connection;
    }

    public void Quench(bool suppressKindlingForgeForTesting = false)
    {
        LogProductInfo();

        TestServerConnections();

        RemoveOldQuenchTablesScripts();

        using var connection = GetConnection("master");
        using var command = connection.CreateCommand();
        command.CommandTimeout = 0;

        try
        {
            if (!string.IsNullOrWhiteSpace(_product.ValidationScript))
            {
                _progressLog.Info("Validate Server");
                command.CommandText = _product.ValidationScript;
                if (!((bool?)command.ExecuteScalar() ?? false))
                    throw new Exception("Invalid server for this product");
            }

            _product.TemplateOrder.ForEach(templateName => QuenchTemplate(command, templateName, _product, suppressKindlingForgeForTesting));
        }
        finally
        {
            connection.Close();
        }

        _progressLog.Info($"Completed quench of {_product.Name}");
    }

    private void LogProductInfo()
    {
        _progressLog.Info($"ProductName: {_product.Name}, TemplateOrder: [{string.Join(",", _product.TemplateOrder)}], ValidationScript: {_product.ValidationScript}");
        if (_product.ScriptTokens.Count == 0) return;

        _progressLog.Info("  Product Script Tokens:");
        _product.ScriptTokens.ToList().ForEach(token => _progressLog.Info($"    {token.Key}: {token.Value}"));

        _progressLog.Info("");
    }

    public void TestServerConnections()
    {
        _progressLog.Info("Testing connection to configured server");
        try
        {
            var connStr = ConnectionString.Build(_primaryServer, "master", _config["Target:User"], _config["Target:Password"]);
            using var conn = SqlConnectionFactory.GetFromFactory().GetSqlConnection(connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = 0;

            cmd.CommandText = "SELECT @@SERVERNAME";
            var serverName = cmd.ExecuteScalar()?.ToString() ?? "UNKNOWN";

            _progressLog.Info($"  {_primaryServer} ({serverName}) connection succeeded");
        }
        catch (Exception e)
        {
            _progressLog.Error($"  {_primaryServer}: {e.Message} **CONNECTION FAILED**");
            _errorLog.Error($"Unable to connect to {_primaryServer}:\r\n{e}");
            throw new Exception("Error validating configured servers");
        }

        _progressLog.Info("");
        _progressLog.Info("");
    }

    private static void RemoveOldQuenchTablesScripts()
    {
        var dir = DirectoryInfoFactory.GetFromFactory().GetDirectoryInfoWrapper(".");
        foreach (var file in dir.GetFiles("SchemaQuench - Quench Tables*.sql", SearchOption.TopDirectoryOnly))
            file.Delete();
    }

    private void QuenchTemplate(IDbCommand command, string templateName, Product product, bool suppressKindligForgeForTesting)
    {
        var dbList = new List<DatabaseQuencher>();
        _progressLog.Info($"Load Template Schema: {templateName}");
        var template = Template.Load(templateName, product);
        if (string.IsNullOrWhiteSpace(template.DatabaseIdentificationScript)) return;

        _progressLog.Info("Locate Databases To Quench");
        command.CommandText = template.DatabaseIdentificationScript;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var quencher = new DatabaseQuencher(_product.Name, template, $"{reader["name"]}", suppressKindligForgeForTesting, product.DropUnknownIndexes ? "1" : "0", _whatIfOnly);
            dbList.Add(quencher);
        }

        dbList.ForEach(d => d.Quench());
        if (!dbList.All(d => d.QuenchSuccessful))
        {
            _progressLog.Error("One or more database quenches FAILED");
            LogBackup.BackupLogsAndExit("SchemaQuench", 2);
        }
    }
}
