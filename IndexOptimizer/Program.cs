using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Text;

namespace IndexSuggester
{
    class Program
    {
        static void Main(string[] args)
        {
            var arguments = ParseArgs(args);

            if (arguments.ContainsKey("h") || arguments.ContainsKey("help"))
            {
                ShowHelp();
                return;
            }

            string server = GetArgument(arguments, "s", "server");
            string database = GetArgument(arguments, "d", "database");
            string username = GetArgument(arguments, "u", "username");
            string password = GetArgument(arguments, "p", "password");
            string resultMode = GetArgument(arguments, "r", "result");

            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(database) ||
                string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) ||
                string.IsNullOrEmpty(resultMode))
            {
                Console.WriteLine("Erro: Todos os parâmetros são obrigatórios. Use -h para ajuda.");
                return;
            }

            resultMode = resultMode.ToLower();
            if (resultMode != "s" && resultMode != "i")
            {
                Console.WriteLine("Erro: Parâmetro 'result' deve ser 's' para script ou 'i' para implementação.");
                return;
            }

            string connectionString = $"Server={server};Database={database};User Id={username};Password={password};";
            var scriptBuilder = new StringBuilder();
            var logBuilder = new StringBuilder();
            var comandosParaExecutar = new List<string>();

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string query = @"
                        SELECT 
                            mid.statement AS Tabela,
                            mid.equality_columns AS ColunasIguais,
                            mid.inequality_columns AS ColunasDiferentes,
                            mid.included_columns AS ColunasIncluidas,
                            migs.avg_user_impact AS ImpactoMedio
                        FROM sys.dm_db_missing_index_groups mig
                        JOIN sys.dm_db_missing_index_group_stats migs ON mig.index_group_handle = migs.group_handle
                        JOIN sys.dm_db_missing_index_details mid ON mig.index_handle = mid.index_handle
                        WHERE mid.database_id = DB_ID(@database)
                        AND migs.avg_user_impact > 90
                        ORDER BY migs.avg_user_impact DESC;";

                    using (var command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@database", database);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string tabelaRaw = reader["Tabela"].ToString();
                                var partes = tabelaRaw.Split('.');
                                string tabela = partes[partes.Length - 1].Replace("[", "").Replace("]", "");

                                var colIgualRaw = reader["ColunasIguais"] == DBNull.Value ? null : reader["ColunasIguais"].ToString();
                                var colIncluidasRaw = reader["ColunasIncluidas"] == DBNull.Value ? null : reader["ColunasIncluidas"].ToString();

                                var colIgual = string.IsNullOrEmpty(colIgualRaw) ? new string[0] : colIgualRaw.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                                var colIncluidas = string.IsNullOrEmpty(colIncluidasRaw) ? new string[0] : colIncluidasRaw.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);

                                string nomeIndex = "IDX_" + tabela + "_" + string.Join("_", colIgual).Replace("[", "").Replace("]", "");

                                string sqlIndexCommand = $"CREATE INDEX {nomeIndex} ON {tabela} ({string.Join(", ", colIgual).Replace("[", "").Replace("]", "")})";
                                if (colIncluidas.Length > 0)
                                    sqlIndexCommand += $" INCLUDE ({string.Join(", ", colIncluidas).Replace("[", "").Replace("]", "")})";

                                sqlIndexCommand += ";";

                                string fullSqlCommand = $@"
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = '{nomeIndex}' 
    AND object_id = OBJECT_ID('{tabela}')
)
BEGIN
    {sqlIndexCommand}
END";

                                scriptBuilder.AppendLine(fullSqlCommand);
                                comandosParaExecutar.Add(fullSqlCommand);
                            }
                        }
                    }

                    string maintenanceScript = @"
-- Script de manutenção de índices
DECLARE @objectid INT;
DECLARE @indexid INT;
DECLARE @schemaname NVARCHAR(128);
DECLARE @objectname NVARCHAR(128);
DECLARE @indexname NVARCHAR(128);
DECLARE @frag FLOAT;
DECLARE @command NVARCHAR(MAX);

DECLARE index_cursor CURSOR FOR
SELECT 
    s.[object_id],
    i.index_id,
    OBJECT_SCHEMA_NAME(s.[object_id], DB_ID()) AS SchemaName,
    OBJECT_NAME(s.[object_id], DB_ID()) AS ObjectName,
    i.name AS IndexName,
    ps.avg_fragmentation_in_percent
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ps
JOIN sys.indexes i ON ps.object_id = i.object_id AND ps.index_id = i.index_id
JOIN sys.partitions p ON ps.object_id = p.object_id AND ps.index_id = p.index_id
JOIN sys.objects s ON ps.object_id = s.object_id
WHERE ps.avg_fragmentation_in_percent > 5
  AND i.type > 0
  AND s.is_ms_shipped = 0;

OPEN index_cursor;
FETCH NEXT FROM index_cursor INTO @objectid, @indexid, @schemaname, @objectname, @indexname, @frag;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @command = N'ALTER INDEX ' + QUOTENAME(@indexname) + N' ON ' + QUOTENAME(@schemaname) + N'.' + QUOTENAME(@objectname) + N' REBUILD;';
    EXEC sp_executesql @command;

    SET @command = N'ALTER INDEX ' + QUOTENAME(@indexname) + N' ON ' + QUOTENAME(@schemaname) + N'.' + QUOTENAME(@objectname) + N' REORGANIZE;';
    EXEC sp_executesql @command;

    FETCH NEXT FROM index_cursor INTO @objectid, @indexid, @schemaname, @objectname, @indexname, @frag;
END;

CLOSE index_cursor;
DEALLOCATE index_cursor;
";

                    scriptBuilder.AppendLine();
                    scriptBuilder.AppendLine(maintenanceScript);

                    if (resultMode == "s")
                    {
                        File.WriteAllText("indices.sql", scriptBuilder.ToString());
                        Console.WriteLine("Script SQL gerado com sucesso: indices.sql");
                    }
                    else
                    {
                        foreach (var cmdText in comandosParaExecutar)
                        {
                            try
                            {
                                logBuilder.AppendLine("➡️ Executando: " + cmdText);
                                using (var execCmd = new SqlCommand(cmdText, connection))
                                {
                                    int rowsAffected = execCmd.ExecuteNonQuery();
                                    logBuilder.AppendLine("✅ Sucesso | Linhas afetadas: " + rowsAffected);
                                }
                            }
                            catch (Exception ex)
                            {
                                logBuilder.AppendLine("❌ Erro ao executar: " + cmdText + "\n   -> " + ex.Message);
                            }
                        }

                        try
                        {
                            logBuilder.AppendLine("➡️ Executando script de manutenção de índices...");
                            using (var cmd = new SqlCommand(maintenanceScript, connection))
                            {
                                cmd.ExecuteNonQuery();
                            }
                            logBuilder.AppendLine("✅ Script de manutenção executado com sucesso.");
                        }
                        catch (Exception ex)
                        {
                            logBuilder.AppendLine("❌ Erro ao executar script de manutenção: " + ex.Message);
                        }

                        File.WriteAllText("execucao_log.txt", logBuilder.ToString());
                        Console.WriteLine("Execução finalizada. Log salvo em: execucao_log.txt");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro de conexão ou execução: " + ex.Message);
            }
        }

        static Dictionary<string, string> ParseArgs(string[] args)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                {
                    string key = args[i].TrimStart('-');
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        result[key] = args[i + 1];
                    else
                        result[key] = "";
                }
            }
            return result;
        }

        static string GetArgument(Dictionary<string, string> args, string shortKey, string longKey)
        {
            return args.ContainsKey(shortKey) ? args[shortKey] :
                   args.ContainsKey(longKey) ? args[longKey] : null;
        }

        static void ShowHelp()
        {
            Console.WriteLine("Aplicação para sugestão e aplicação de índices ausentes em SQL Server\n");
            Console.WriteLine("Parâmetros obrigatórios:");
            Console.WriteLine("  -s, --server     : Nome do servidor SQL");
            Console.WriteLine("  -d, --database   : Nome do banco de dados");
            Console.WriteLine("  -u, --username   : Nome do usuário SQL");
            Console.WriteLine("  -p, --password   : Senha do usuário");
            Console.WriteLine("  -r, --result     : Modo de resultado: 's' para gerar script SQL, 'i' para implementar direto e gerar log");
            Console.WriteLine("  -h, --help       : Mostra este menu de ajuda\n");
        }
    }
}
