# Remote DB Access Blocker

- API URL: http://localhost:5130
- DB target: SQL Server @ 54.193.197.193 (port 1433)
- Current error: "Login failed for user 'AuthApiUser' (Error 18456)"
- Local verification: API & UI work with LocalDB; remote login needs DBA action.


1) it says inbound TCP/1433 is open to my IP in the security group / firewall.
2) i need help create or enable server login: AuthApiUser (SQL Auth) with the provided password.
3) Map login to database `toolify` with db_datareader + db_datawriter (and db_ddladmin if DDL required).
4) Confirm SQL Server is in "SQL Server and Windows Authentication mode".

Once done, I’ll switch ConnectionStrings:Sql back to the remote server and retest `/db/ping`.
