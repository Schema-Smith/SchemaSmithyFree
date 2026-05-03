// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Schema.Utility;
using Schema.Delivery;

namespace Schema.Domain
{
    public class Table : DynamicBase
    {
        [SchemaProperty(Required = true)]
        [JsonProperty(Order = 2)]
        public string Name { get; set; } = "";

        [JsonProperty(Order = 3)]
        public List<Column> Columns { get; set; } = [];

        [JsonProperty(Order = 4)]
        public List<Index> Indexes { get; set; } = [];

        [JsonProperty(Order = 5)]
        public List<ForeignKey> ForeignKeys { get; set; } = [];

        [JsonProperty(Order = 6)]
        public List<CheckConstraint> CheckConstraints { get; set; } = [];

        [JsonProperty(Order = 80)]
        public string ShouldApplyExpression { get; set; }

        [JsonProperty(Order = 81)]
        public DataDelivery DataDelivery { get; set; }

        [JsonProperty(Order = 90)]
        public string OldName { get; set; }

        /// <summary>
        /// Loads a Table from a JSON file using platform-aware deserialization.
        /// Returns the correct platform subclass (SqlServerTable, PostgreSqlTable, MySqlTable).
        /// </summary>
        public static Table Load(string filePath, Platform platform)
        {
            try
            {
                return JsonHelper.TableLoad(filePath, platform);
            }
            catch (Exception e)
            {
                throw new Exception($"Error loading table from {filePath}\r\n{e.Message}", e);
            }
        }

        public virtual void ResolveScriptTokensInTableComponentScripts(List<KeyValuePair<string, string>> tokens)
        {
            ShouldApplyExpression = TableTokenReplace(ShouldApplyExpression, tokens.Concat(GetCustomTokens(Extensions)).ToList());
            var tableTokens = tokens.Concat(GetCustomTokens(Extensions, "Table.")).ToList();
            foreach (var check in CheckConstraints)
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(check.Extensions)).ToList();
                check.ShouldApplyExpression = TableTokenReplace(check.ShouldApplyExpression, scriptTokens);
                check.Expression = TableTokenReplace(check.Expression, scriptTokens);
            }
            foreach (var column in Columns)
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(column.Extensions)).ToList();
                column.ShouldApplyExpression = TableTokenReplace(column.ShouldApplyExpression, scriptTokens);
                column.Default = TableTokenReplace(column.Default, scriptTokens);
            }
            foreach (var key in ForeignKeys)
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(key.Extensions)).ToList();
                key.ShouldApplyExpression = TableTokenReplace(key.ShouldApplyExpression, scriptTokens);
            }
            foreach (var index in Indexes)
            {
                var scriptTokens = tableTokens.Concat(GetCustomTokens(index.Extensions)).ToList();
                index.ShouldApplyExpression = TableTokenReplace(index.ShouldApplyExpression, scriptTokens);
            }
        }

        internal static string TableTokenReplace(string script, List<KeyValuePair<string, string>> scriptTokens)
        {
            var tokensToReplace = TokenHelper.GetTokensFromString(script);
            if (tokensToReplace.Count == 0) return script;
            var replacableTokens = scriptTokens
                .Where(t => tokensToReplace.Any(tr => tr.EqualsIgnoringCase(t.Key)))
                .ToList();
            replacableTokens.ForEach(token => { script = Regex.Replace(script, $@"\{{\{{{token.Key}\}}\}}", token.Value, RegexOptions.IgnoreCase); });
            return script;
        }

        internal static List<KeyValuePair<string, string>> GetCustomTokens(JToken extensions, string baseName = "")
        {
            var tokens = new List<KeyValuePair<string, string>>();
            if (extensions is not JObject jObj) return tokens;

            foreach (var prop in jObj.Properties())
            {
                if (prop.Value.Type == JTokenType.Object)
                    ProcessJObject(prop.Value.ToObject<JObject>(), tokens, $"{baseName}{prop.Name}.");
                else if (prop.Value.Type == JTokenType.Array)
                    tokens.Add(new KeyValuePair<string, string>($"{baseName}{prop.Name}", string.Join(",", prop.Value.ToObject<JArray>().Select(x => x.ToString()))));
                else
                    tokens.Add(new KeyValuePair<string, string>($"{baseName}{prop.Name}", prop.Value.ToString()));
            }
            return tokens;
        }

        private static void ProcessJObject(JObject jObj, List<KeyValuePair<string, string>> tokens, string baseName)
        {
            foreach (var jProp in jObj.Properties())
            {
                if (jProp.Value.Type == JTokenType.Object)
                    ProcessJObject(jProp.Value.ToObject<JObject>(), tokens, $"{baseName}{jProp.Name}.");
                else if (jProp.Value.Type == JTokenType.Array)
                    tokens.Add(new KeyValuePair<string, string>($"{baseName}{jProp.Name}", string.Join(",", jProp.Value.ToObject<JArray>().Select(x => x.ToString()))));
                else
                    tokens.Add(new KeyValuePair<string, string>($"{baseName}{jProp.Name}", jProp.Value.ToString()));
            }
        }
    }
}
