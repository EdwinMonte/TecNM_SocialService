using Npgsql;

internal static class AccountMigrationChecks
{
    public static async Task RunAsync()
    {
        var value=Environment.GetEnvironmentVariable("DOCUMENT_TEST_CONNECTION");
        if(string.IsNullOrEmpty(value)) return;
        if(new NpgsqlConnectionStringBuilder(value).Database!="document_checks") throw new Exception("Isolated database required.");
        await using var db=new NpgsqlConnection(value); await db.OpenAsync();
        async Task Execute(string sql) { await using var cmd=new NpgsqlCommand(sql,db); await cmd.ExecuteNonQueryAsync(); }
        var migration=File.ReadAllText("database/migrations/20261107_student_accounts.sql").Replace("BEGIN;", "").Replace("COMMIT;", "");
        await Execute("CREATE SCHEMA qa_account_migration; SET search_path TO qa_account_migration");
        try
        {
            await Execute("""
                CREATE TABLE users(id bigint PRIMARY KEY,username varchar(64) NOT NULL,email varchar(254) NOT NULL,password_hash text NOT NULL,deleted_at timestamptz,updated_at timestamptz);
                CREATE UNIQUE INDEX uq_users__username ON users(username) WHERE deleted_at IS NULL;
                CREATE UNIQUE INDEX uq_users__email ON users(email) WHERE deleted_at IS NULL;
                CREATE TABLE roles(id bigint PRIMARY KEY,name text NOT NULL,deleted_at timestamptz);
                CREATE TABLE user_roles(id bigint PRIMARY KEY,user_id bigint REFERENCES users(id),role_id bigint REFERENCES roles(id),deleted_at timestamptz);
                CREATE TABLE students(id bigint PRIMARY KEY,user_id bigint REFERENCES users(id),control_number text,institutional_email text,deleted_at timestamptz);
                CREATE TABLE student_import_batches(id bigint PRIMARY KEY);
                INSERT INTO users VALUES(1,'admin','admin@college.mx','admin-hash',NULL,NULL),(2,'old.student','old@college.mx','unchanged-hash',NULL,NULL);
                INSERT INTO roles VALUES(1,'admin',NULL),(2,'student',NULL);
                INSERT INTO user_roles VALUES(1,1,1,NULL),(2,2,2,NULL);
                INSERT INTO students VALUES(1,2,'TEST001',' Student@college.mx ',NULL);
                """);
            foreach(var conflict in new[]{"INSERT INTO users VALUES(3,'student@college.mx','other@college.mx','other-hash',NULL,NULL)","UPDATE students SET institutional_email=NULL", "INSERT INTO user_roles VALUES(3,2,1,NULL)", "INSERT INTO students VALUES(2,2,'TEST002','different@college.mx',NULL)"})
            {
                await using var tx=await db.BeginTransactionAsync();
                try
                {
                    await Execute(conflict);
                    try { await Execute(migration); throw new Exception("Inconsistent account migration succeeded."); }
                    catch(PostgresException error) when(error.SqlState=="P0001") { }
                }
                finally { await tx.RollbackAsync(); }
            }
            await using(var tx=await db.BeginTransactionAsync()) { await Execute(migration); await tx.CommitAsync(); }
            await using var verify=new NpgsqlCommand("SELECT username='student@college.mx' AND email='student@college.mx' AND password_hash='unchanged-hash' AND session_version=1 FROM users WHERE id=2",db);
            if((bool?)await verify.ExecuteScalarAsync()!=true) throw new Exception("Account migration changed password or lost identity.");
            await using(var tx=await db.BeginTransactionAsync())
            {
                try { await Execute("UPDATE users SET email='STUDENT@college.mx' WHERE id=1"); throw new Exception("Case-insensitive email uniqueness missing."); }
                catch(PostgresException error) when(error.SqlState=="23505") { }
                finally { await tx.RollbackAsync(); }
            }
        }
        finally { await Execute("SET search_path TO public; DROP SCHEMA qa_account_migration CASCADE"); }
        Console.WriteLine("Student account migration preserves credentials and rolls back conflicting identities/roles.");
    }
}
