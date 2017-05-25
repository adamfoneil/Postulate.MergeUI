using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Postulate.Orm.Merge.Action;
using System.Reflection;
using System.Configuration;
using Postulate.Orm.Extensions;
using Postulate.Orm;
using Postulate.Orm.Merge;
using Postulate.Orm.Interfaces;
using System.Data;
using System.Data.SqlClient;
using Dapper;

namespace Postulate.MergeUI
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                Dictionary<string, MergeViewModel> actions = null;
                string fileName = null;

                if (args?.Length == 1)
                {
                    string assembly = args[0];
                    if (File.Exists(assembly))
                    {
                        fileName = assembly;
                        actions = GetMergeActions(assembly);
                        if (!actions.Any())
                        {
                            Console.WriteLine($"Postulate: No model changes to merge for {assembly}.");
                            return;
                        }
                    }
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new frmMain() { MergeActions = actions, AssemblyFilename = fileName });
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        internal static Dictionary<string, MergeViewModel> GetMergeActions(string assemblyFile)
        {
            Dictionary<string, MergeViewModel> results = new Dictionary<string, MergeViewModel>();

            var assembly = Assembly.LoadFile(assemblyFile);
            var config = ConfigurationManager.OpenExeConfiguration(assembly.Location);
            var dbTypes = assembly.GetTypes().Where(t => t.IsDerivedFromGeneric(typeof(SqlServerDb<>)));

            foreach (var dbType in dbTypes)
            {
                Type schemaMergeBaseType = typeof(SchemaMerge<>);
                var schemaMergeGenericType = schemaMergeBaseType.MakeGenericType(dbType);
                var db = Activator.CreateInstance(dbType, config) as IDb;
                using (var cn = OpenOrCreateDb(db, config))
                {
                    cn.Open();
                    var schemaMerge = Activator.CreateInstance(schemaMergeGenericType) as ISchemaMerge;
                    var actions = schemaMerge.Compare(cn);
                    Dictionary<Orm.Merge.Action.MergeAction, LineRange> lineRanges;
                    var script = schemaMerge.GetScript(cn, actions, out lineRanges);
                    results.Add(dbType.Name, new MergeViewModel { Db = db, Merge = schemaMerge, Actions = actions, LineRanges = lineRanges, Script = script, ServerAndDatabase = ParseConnectionInfo(cn) });
                }
            }

            return results;
        }

        private static IDbConnection OpenOrCreateDb(IDb db, Configuration config)
        {
            var result = db.GetConnection();

            try
            {
                result.Open();
            }
            catch (SqlException)
            {
                string dbName;
                using (IDbConnection cnMaster = TryGetMasterDb(config, db.ConnectionName, out dbName))
                {
                    cnMaster.Execute($"CREATE DATABASE [{dbName}]", commandTimeout: 60);
                }
                result = db.GetConnection();
            }

            return result;
        }

        private static IDbConnection TryGetMasterDb(Configuration config, string connectionName, out string dbName)
        {
            var tokens = ParseConnectionTokens(config.ConnectionStrings.ConnectionStrings[connectionName].ConnectionString);
            var dbTokens = new string[] { "Database", "Initial Catalog" };
            dbName = Coalesce(tokens, dbTokens);
            string connectionString = JoinReplace(tokens, dbTokens, "master");
            return new SqlConnection(connectionString);
        }

        private static string JoinReplace(Dictionary<string, string> tokens, string[] lookForKey, string setValue)
        {
            string key = lookForKey.First(item => tokens.ContainsKey(item));
            tokens[key] = setValue;
            return string.Join(";", tokens.Select(keyPair => $"{keyPair.Key}={keyPair.Value}"));
        }

        internal static string ParseConnectionInfo(IDbConnection connection)
        {
            Dictionary<string, string> nameParts = ParseConnectionTokens(connection.ConnectionString);

            return $"{Coalesce(nameParts, "Data Source", "Server")}.{Coalesce(nameParts, "Database", "Initial Catalog")}";
        }

        private static Dictionary<string, string> ParseConnectionTokens(string connectionString)
        {
            return connectionString.Split(';')
                .Select(s =>
                {
                    string[] parts = s.Split('=');
                    return new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim());
                }).ToDictionary(item => item.Key, item => item.Value);
        }

        private static string Coalesce(Dictionary<string, string> dictionary, params string[] keys)
        {
            string key = keys.First(item => dictionary.ContainsKey(item));
            return dictionary[key];
        }

    }
}
