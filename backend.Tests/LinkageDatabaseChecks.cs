using Npgsql;

internal static class LinkageDatabaseChecks
{
    public static async Task RunAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("DOCUMENT_TEST_CONNECTION");
        if (string.IsNullOrEmpty(connectionString)) return;
        if (new NpgsqlConnectionStringBuilder(connectionString).Database != "document_checks") throw new Exception("Isolated database required.");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using var prepare = new NpgsqlCommand("ALTER TABLE companies ALTER COLUMN company_type DROP NOT NULL; UPDATE companies SET company_type=NULL WHERE id=1", connection, transaction);
            await prepare.ExecuteNonQueryAsync();
            var sql = File.ReadAllText("database/migrations/20261106_company_type_finalize.sql").Replace("BEGIN;", "").Replace("COMMIT;", "");
            try
            {
                await using var finalize = new NpgsqlCommand(sql, connection, transaction);
                await finalize.ExecuteNonQueryAsync();
                throw new Exception("Incomplete classification migration succeeded.");
            }
            catch (PostgresException e) when (e.SqlState == "P0001" && e.MessageText.Contains("Clasifica")) { }
            await transaction.RollbackAsync();
        }
        await using (var check = new NpgsqlCommand("SELECT is_nullable FROM information_schema.columns WHERE table_schema='public' AND table_name='companies' AND column_name='company_type'", connection))
            if ((string?)await check.ExecuteScalarAsync() != "NO") throw new Exception("Migration rollback lost NOT NULL.");
        await using (var explain = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS) SELECT * FROM vw_linkage_company_metrics WHERE tramite_type='internship' AND period_id=1", connection))
        await using (var reader = await explain.ExecuteReaderAsync())
        {
            var plan = new List<string>();
            while (await reader.ReadAsync()) plan.Add(reader.GetString(0));
            if (!plan.Any(line => line.Contains("Aggregate"))) throw new Exception("Linkage aggregation missing from SQL plan.");
            Directory.CreateDirectory("tmp/pdf-qa");
            await File.WriteAllLinesAsync("tmp/pdf-qa/linkage-query-plan.txt", plan);
        }
        // Fresh-schema validation uses a separate namespace inside the disposable database.
        await using (var schema = new NpgsqlCommand("CREATE SCHEMA qa_fresh; SET search_path TO qa_fresh", connection)) await schema.ExecuteNonQueryAsync();
        try
        {
            await using var create = new NpgsqlCommand(File.ReadAllText("database/design/schema.sql"), connection);
            await create.ExecuteNonQueryAsync();
            await using var empty = new NpgsqlCommand("SELECT COUNT(*) FROM vw_linkage_company_type_metrics", connection);
            if ((long)(await empty.ExecuteScalarAsync())! != 0) throw new Exception("Fresh linkage view should be empty.");
        }
        finally
        {
            await using var cleanup = new NpgsqlCommand("SET search_path TO public; DROP SCHEMA qa_fresh CASCADE", connection);
            await cleanup.ExecuteNonQueryAsync();
        }
        Console.WriteLine("Incomplete classification rollback, fresh schema and SQL EXPLAIN checks passed.");
    }
}
