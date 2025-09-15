using System.Data;
using System.Globalization;
using System.Text;
using CsvHelper;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------- Services ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Toolify API", Version = "v1" });
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

// Connection string via appsettings or user-secrets
// dotnet user-secrets set "ConnectionStrings:Sql" "Server=...,1433;Database=...;User Id=...;Password=...;TrustServerCertificate=True;"
var connStr = builder.Configuration.GetConnectionString("Sql");
// Scoped: a fresh SqlConnection per request
builder.Services.AddScoped<IDbConnection>(_ => new SqlConnection(connStr));

var app = builder.Build();

// ---------- Middleware ----------
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Toolify API v1");
});
app.UseCors();

// Convenience: redirect / to swagger
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// ---------- Helpers ----------
static (string where, DynamicParameters p) BuildCardWhere(string boardId, string status, DateTime? from, DateTime? to)
{
    var sb = new StringBuilder(" where 1=1 ");
    var p = new DynamicParameters();

    if (!string.IsNullOrWhiteSpace(boardId))
    {
        sb.Append(" and c.board_id = @boardId ");
        p.Add("boardId", boardId);
    }
    if (!string.IsNullOrWhiteSpace(status))
    {
        sb.Append(" and c.status = @status ");
        p.Add("status", status);
    }
    if (from.HasValue)
    {
        sb.Append(" and c.due_date is not null and c.due_date >= @from ");
        p.Add("from", from.Value);
    }
    if (to.HasValue)
    {
        sb.Append(" and c.due_date is not null and c.due_date < @to ");
        p.Add("to", to.Value);
    }

    return (sb.ToString(), p);
}

static DateTime? TryDate(IDictionary<string, object> d, string key)
{
    if (!d.TryGetValue(key, out var v) || v is null) return null;
    DateTime dt;
    if (DateTime.TryParse(v.ToString(), out dt)) return dt;
    return null;
}
static decimal? TryDec(IDictionary<string, object> d, string key)
{
    if (!d.TryGetValue(key, out var v) || v is null) return null;
    decimal n;
    if (decimal.TryParse(v.ToString(), out n)) return n;
    return null;
}

// ---------- 1) DB Ping ----------
app.MapGet("/db/ping", async (IDbConnection db) =>
{
    var version = await db.ExecuteScalarAsync<string>("select @@VERSION");
    var dbName = await db.ExecuteScalarAsync<string>("select db_name()");
    return Results.Ok(new { connected = true, version, db = dbName });
});

// ---------- 2) Dashboard KPIs ----------
app.MapGet("/reports/dashboard", async (IDbConnection db, string boardId, string status, DateTime? from, DateTime? to) =>
{
    var q = BuildCardWhere(boardId, status, from, to);
    var where = q.where; var p = q.p;

    var kpis = await db.QueryFirstAsync(@$"
        select
          (select count(*) from dbo.cards c {where}) as total_cards,
          (select count(*) from dbo.cards c {where} and c.due_date < sysutcdatetime()) as overdue,
          (select count(*) from dbo.cards c {where} and c.due_date between sysutcdatetime() and dateadd(day,7,sysutcdatetime())) as due_next_7,
          (select count(*) from dbo.cards c {where} and c.status in ('Open','InProgress')) as wip
    ", p);

    var byStatus = await db.QueryAsync(@$"
        select coalesce(c.status,'(none)') as status, count(*) as count
        from dbo.cards c {where}
        group by c.status
        order by count(*) desc
    ", p);

    return Results.Ok(new { kpis, byStatus });
});

// ---------- 3) Cards Deep-Dive (paged) ----------
app.MapGet("/reports/cards", async (IDbConnection db, string boardId, string status, DateTime? from, DateTime? to, int page, int pageSize) =>
{
    if (page <= 0) page = 1;
    if (pageSize <= 0) pageSize = 20;
    var skip = (page - 1) * pageSize;

    var q = BuildCardWhere(boardId, status, from, to);
    var where = q.where; var p = q.p;
    p.Add("skip", skip);
    p.Add("take", pageSize);

    var rows = await db.QueryAsync(@$"
        select c.card_id, c.title, c.status, c.board_id, c.list_id, c.start_date, c.due_date,
               c.estimate_hours, c.worked_hours, c.members, c.updated_at
        from dbo.cards c {where}
        order by c.updated_at desc
        offset @skip rows fetch next @take rows only;
    ", p);

    var total = await db.ExecuteScalarAsync<int>(@$"
        select count(*) from dbo.cards c {where};
    ", p);

    return Results.Ok(new { page, pageSize, total, rows });
});

// ---------- 4) CSV Download (filtered) ----------
app.MapGet("/exports/cards.csv", async (IDbConnection db, string boardId, string status, DateTime? from, DateTime? to) =>
{
    var q = BuildCardWhere(boardId, status, from, to);
    var where = q.where; var p = q.p;

    var result = await db.QueryAsync(@$"
        select c.card_id as [Card ID], c.title as [Title], c.status as [Status],
               c.board_id as [Board ID], c.list_id as [List ID],
               c.start_date as [Start Date], c.due_date as [Due Date],
               c.estimate_hours as [Estimate Hours], c.worked_hours as [Worked Hours],
               c.members as [Members]
        from dbo.cards c {where}
        order by c.updated_at desc;
    ", p);

    // Use CsvHelper to write (no tricky escaping)
    var ms = new MemoryStream();
    using (var writer = new StreamWriter(ms, new UTF8Encoding(false), 1024, true))
    using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
    {
        // header
        csv.WriteField("Card ID");
        csv.WriteField("Title");
        csv.WriteField("Status");
        csv.WriteField("Board ID");
        csv.WriteField("List ID");
        csv.WriteField("Start Date");
        csv.WriteField("Due Date");
        csv.WriteField("Estimate Hours");
        csv.WriteField("Worked Hours");
        csv.WriteField("Members");
        csv.NextRecord();

        foreach (var row in result)
        {
            var d = (IDictionary<string, object>)row;
            object Get(IDictionary<string, object> map, string key)
            {
                object value;
                if (map.TryGetValue(key, out value) && value != null) return value;
                return "";
            }

            csv.WriteField(Get(d, "Card ID"));
            csv.WriteField(Get(d, "Title"));
            csv.WriteField(Get(d, "Status"));
            csv.WriteField(Get(d, "Board ID"));
            csv.WriteField(Get(d, "List ID"));
            csv.WriteField(Get(d, "Start Date"));
            csv.WriteField(Get(d, "Due Date"));
            csv.WriteField(Get(d, "Estimate Hours"));
            csv.WriteField(Get(d, "Worked Hours"));
            csv.WriteField(Get(d, "Members"));
            csv.NextRecord();
        }
        writer.Flush();
    }
    ms.Position = 0;
    return Results.File(ms, "text/csv", "cards.csv");
});

// ---------- 5) Trello CSV Import (multipart/form-data) ----------
app.MapPost("/import/trello", async (HttpRequest req, IDbConnection db) =>
{
    if (!req.HasFormContentType) return Results.BadRequest(new { error = "Expecting multipart/form-data" });
    var form = await req.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file == null || file.Length == 0) return Results.BadRequest(new { error = "No CSV uploaded." });

    using (var stream = file.OpenReadStream())
    using (var reader = new StreamReader(stream))
    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
    {
        var rows = csv.GetRecords<dynamic>();
        var seenBoards = new HashSet<string>();
        var seenLists  = new HashSet<string>();

        foreach (var r in rows)
        {
            var d = (IDictionary<string, object>)r;

            string boardId   = d.ContainsKey("Board ID") ? (d["Board ID"]?.ToString() ?? "") : "";
            string boardName = d.ContainsKey("Board Name") ? (d["Board Name"]?.ToString() ?? "") : "";
            string listId    = d.ContainsKey("List ID") ? (d["List ID"]?.ToString() ?? "") : "";
            string listName  = d.ContainsKey("List Name") ? (d["List Name"]?.ToString() ?? "") : "";
            string cardId    = d.ContainsKey("Card ID") ? (d["Card ID"]?.ToString() ?? "") : "";
            string title     = d.ContainsKey("Card Name") ? (d["Card Name"]?.ToString() ?? "") : "";
            string desc      = d.ContainsKey("Card Description") ? (d["Card Description"]?.ToString() ?? "") : "";
            string labels    = d.ContainsKey("Labels") ? (d["Labels"]?.ToString() ?? "") : "";
            string members   = d.ContainsKey("Members") ? (d["Members"]?.ToString() ?? "") : "";
            string status    = d.ContainsKey("Status") ? (d["Status"]?.ToString() ?? "") : null;

            DateTime? start = TryDate(d, "Start Date");
            DateTime? due   = TryDate(d, "Due Date");
            decimal?  est   = TryDec(d,  "Estimate Hours");
            decimal?  worked= TryDec(d,  "Worked Hours");

            if (!string.IsNullOrWhiteSpace(boardId) && !seenBoards.Contains(boardId))
            {
                await db.ExecuteAsync(
                    "if not exists(select 1 from dbo.boards where board_id=@boardId) insert into dbo.boards(board_id, board_name) values(@boardId, @boardName)",
                    new { boardId, boardName });
                seenBoards.Add(boardId);
            }
            if (!string.IsNullOrWhiteSpace(listId) && !seenLists.Contains(listId))
            {
                await db.ExecuteAsync(
                    "if not exists(select 1 from dbo.lists where list_id=@listId) insert into dbo.lists(list_id, board_id, list_name, position) values(@listId, @boardId, @listName, null)",
                    new { listId, boardId, listName });
                seenLists.Add(listId);
            }
            if (!string.IsNullOrWhiteSpace(cardId))
            {
                await db.ExecuteAsync(@"
merge dbo.cards as t
using (select @cardId as card_id) s on t.card_id = s.card_id
when matched then update set
  list_id=@listId, board_id=@boardId, title=@title, description=@desc, status=@status,
  start_date=@start, due_date=@due, estimate_hours=@est, worked_hours=@worked, members=@members,
  updated_at=sysutcdatetime()
when not matched then insert(card_id, list_id, board_id, title, description, status, start_date, due_date, estimate_hours, worked_hours, members)
values(@cardId, @listId, @boardId, @title, @desc, @status, @start, @due, @est, @worked, @members);",
                    new { cardId, listId, boardId, title, desc, status, start, due, est, worked, members });
            }
            if (!string.IsNullOrWhiteSpace(labels))
            {
                var parts = labels.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var raw in parts)
                {
                    var tag = raw.Trim();
                    if (tag.Length == 0) continue;

                    await db.ExecuteAsync(
                        "if not exists(select 1 from dbo.tags where tag_name=@tag and board_id=@boardId) insert into dbo.tags(tag_name,board_id) values(@tag,@boardId)",
                        new { tag, boardId });

                    await db.ExecuteAsync(
                        "if not exists(select 1 from dbo.card_tags where card_id=@cardId and tag_name=@tag) insert into dbo.card_tags(card_id, tag_name, board_id) values(@cardId, @tag, @boardId)",
                        new { cardId, tag, boardId });
                }
            }
        }
    }

    return Results.Ok(new { imported = true });
});

// ---------- 6) Power BI Reports CRUD ----------
app.MapGet("/pbi-reports", async (IDbConnection db) =>
{
    var rows = await db.QueryAsync("select * from dbo.pbi_reports order by serial_no");
    return Results.Ok(rows);
});

app.MapPost("/pbi-reports", async (IDbConnection db, PbiReportDto dto) =>
{
    var id = await db.ExecuteScalarAsync<int>(@"
insert into dbo.pbi_reports(serial_no, stream, workspace, report_title, report_link, pages, sources, semantic_layer, active_last30, schedule, frequency_time, lob, stakeholder)
values(@SerialNo,@Stream,@Workspace,@ReportTitle,@ReportLink,@Pages,@Sources,@SemanticLayer,@ActiveLast30,@Schedule,@FrequencyTime,@Lob,@Stakeholder);
select cast(scope_identity() as int);", dto);
    return Results.Created($"/pbi-reports/{id}", new { id });
});

app.MapPut("/pbi-reports/{id:int}", async (int id, IDbConnection db, PbiReportDto dto) =>
{
    var rows = await db.ExecuteAsync(@"
update dbo.pbi_reports set
 serial_no=@SerialNo, stream=@Stream, workspace=@Workspace, report_title=@ReportTitle, report_link=@ReportLink,
 pages=@Pages, sources=@Sources, semantic_layer=@SemanticLayer, active_last30=@ActiveLast30,
 schedule=@Schedule, frequency_time=@FrequencyTime, lob=@Lob, stakeholder=@Stakeholder
where id=@Id",
        new
        {
            Id = id,
            dto.SerialNo, dto.Stream, dto.Workspace, dto.ReportTitle, dto.ReportLink,
            dto.Pages, dto.Sources, dto.SemanticLayer, dto.ActiveLast30, dto.Schedule,
            dto.FrequencyTime, dto.Lob, dto.Stakeholder
        });
    return rows == 0 ? Results.NotFound() : Results.NoContent();
});

app.MapDelete("/pbi-reports/{id:int}", async (int id, IDbConnection db) =>
{
    var rows = await db.ExecuteAsync("delete from dbo.pbi_reports where id=@id", new { id });
    return rows == 0 ? Results.NotFound() : Results.NoContent();
});

app.Run();

// ---------- DTOs ----------
public record PbiReportDto(
    int SerialNo, string Stream, string Workspace, string ReportTitle, string ReportLink,
    string Pages, string Sources, string SemanticLayer, string ActiveLast30, string Schedule,
    string FrequencyTime, string Lob, string Stakeholder
);
