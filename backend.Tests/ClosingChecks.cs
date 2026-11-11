using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TecNM.Api.Data;

internal static class ClosingChecks
{
    public static async Task RunAsync(HttpClient student, HttpClient admin, TecNMDbContext db, long ssId, long ppId, byte[] bytes, DateOnly date)
    {
        async Task<long> Upload(HttpClient client, string type, long id, string code, bool approve = true, HttpStatusCode expected = HttpStatusCode.Created)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(bytes), "file", code + ".pdf");
            form.Add(new StringContent(type), "tramiteType");
            form.Add(new StringContent(id.ToString()), "tramiteId");
            form.Add(new StringContent(code), "documentCode");
            var response = await client.PostAsync("/api/students/1/documents", form);
            Expect(response, expected);
            if (expected != HttpStatusCode.Created) return 0;
            var docId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
            if (approve) Expect(await admin.PostAsJsonAsync($"/api/documents/{docId}/review", new { status = "approved" }), HttpStatusCode.OK);
            return docId;
        }
        async Task<int> Progress() => (await student.GetFromJsonAsync<JsonElement>($"/api/social-services/{ssId}"))
            .GetProperty("documentProgressPercentage").GetInt32();
        async Task CheckTracking(int progress, bool finalEvaluation)
        {
            var rows = await admin.GetFromJsonAsync<JsonElement>("/api/reports/tracking?tramiteType=social_service");
            var row = rows.EnumerateArray().Single(r => r.GetProperty("tramiteId").GetInt64() == ssId);
            if (row.GetProperty("progressPercentage").GetInt32() != progress || row.GetProperty("documents").GetProperty("ev3").GetBoolean() != finalEvaluation)
                throw new Exception("Tracking report and expediente progress disagree.");
        }
        async Task<int> Sequence(string type) => await db.DocumentSeries.AsNoTracking()
            .Where(s => s.DocumentType == type && s.CurrentYear == DateTime.UtcNow.Year)
            .Select(s => (int?)s.CurrentSequence).SingleOrDefaultAsync() ?? 0;
        async Task ReleaseOnce(string endpoint, string series, object payload)
        {
            var before = await Sequence(series);
            var responses = await Task.WhenAll(admin.PostAsJsonAsync(endpoint, payload), admin.PostAsJsonAsync(endpoint, payload));
            if (responses.Count(r => r.StatusCode == HttpStatusCode.OK) != 1 || responses.Any(r => r.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Conflict)))
                throw new Exception("Concurrent release did not produce one successful release.");
            if (await Sequence(series) != before + 1) throw new Exception("Concurrent release consumed extra folios.");
        }
        var completion = new { terminationLetterDate = date };
        await Upload(student, "social_service", ssId, "EV1", false, HttpStatusCode.Forbidden);
        Expect(await admin.PostAsJsonAsync($"/api/social-services/{ssId}/activate", new { }), HttpStatusCode.OK);
        Expect(await admin.PostAsJsonAsync($"/api/social-services/{ssId}/complete", completion), HttpStatusCode.UnprocessableEntity);
        foreach (var code in new[] { "CA", "R2", "R3", "CT", "EV_SUP1", "EV_SELF1", "EV_SUP2", "EV_SELF2", "EV_SUP3", "EV_SELF3" })
            await Upload(student, "social_service", ssId, code);
        if (await Progress() != 80) throw new Exception("Incomplete seven-document evaluation block counted as complete.");
        await CheckTracking(80, false);
        var missingFinal = await admin.PostAsJsonAsync($"/api/social-services/{ssId}/complete", completion);
        Expect(missingFinal, HttpStatusCode.UnprocessableEntity);
        if (!(await missingFinal.Content.ReadAsStringAsync()).Contains("EV_ACT")) throw new Exception("Missing final activity evidence was not identified.");
        var finalId = await Upload(student, "social_service", ssId, "EV_ACT", false);
        Expect(await admin.PostAsJsonAsync($"/api/social-services/{ssId}/complete", completion), HttpStatusCode.UnprocessableEntity);
        Expect(await admin.PostAsJsonAsync($"/api/documents/{finalId}/review", new { status = "approved" }), HttpStatusCode.OK);
        if (await Progress() != 90) throw new Exception("All seven evaluations did not complete the evaluation block.");
        await CheckTracking(90, true);
        Expect(await admin.PostAsJsonAsync($"/api/social-services/{ssId}/complete", completion), HttpStatusCode.OK);
        await ReleaseOnce($"/api/social-services/{ssId}/release", "social_service_release_certificate", new { finalGradeLevel = "good", finalScore = 85, originalTerminationLetterReceived = true });
        if (await Progress() != 100) throw new Exception("Released SS did not reach 100 percent.");
        Expect(await student.GetAsync($"/api/social-services/{ssId}/pdf/individual_act"), HttpStatusCode.Forbidden);
        var actBefore = await admin.GetStringAsync($"/api/social-services/{ssId}");
        foreach (var attempt in Enumerable.Range(0, 2))
        {
            var act = await admin.GetAsync($"/api/social-services/{ssId}/pdf/individual_act");
            Expect(act, HttpStatusCode.OK);
            if (act.Content.Headers.ContentType?.MediaType != "application/pdf") throw new Exception("Act PDF MIME");
            File.WriteAllBytes("tmp/pdf-qa/ss-individual-act-http.pdf", await act.Content.ReadAsByteArrayAsync());
        }
        if (actBefore != await admin.GetStringAsync($"/api/social-services/{ssId}")) throw new Exception("Act download changed the process");

        Expect(await student.GetAsync($"/api/social-services/{ssId}/pdf/commitment"), HttpStatusCode.OK);
        await Upload(student, "social_service", ssId, "CC", false, HttpStatusCode.Conflict);

        Expect(await admin.PostAsJsonAsync($"/api/internships/{ppId}/complete", completion), HttpStatusCode.UnprocessableEntity);
        await Upload(student, "internship", ppId, "EV");
        await Upload(student, "internship", ppId, "CT");
        var caId = await Upload(admin, "internship", ppId, "CA", false);
        Expect(await admin.PostAsJsonAsync($"/api/internships/{ppId}/complete", completion), HttpStatusCode.UnprocessableEntity);
        Expect(await admin.PostAsJsonAsync($"/api/documents/{caId}/review", new { status = "approved" }), HttpStatusCode.OK);
        Expect(await admin.PostAsJsonAsync($"/api/internships/{ppId}/complete", completion), HttpStatusCode.OK);
        await ReleaseOnce($"/api/internships/{ppId}/release", "internship_release_certificate", new { releaseDate = date });
        var pp = await student.GetFromJsonAsync<JsonElement>($"/api/internships/{ppId}");
        if (pp.GetProperty("status").GetString() != "released" || !pp.GetProperty("isExpedientComplete").GetBoolean())
            throw new Exception("Released PP did not complete its expediente.");
        Expect(await student.GetAsync($"/api/internships/{ppId}/pdf/evaluation"), HttpStatusCode.OK);
        await Upload(student, "internship", ppId, "EV", false, HttpStatusCode.Conflict);
        Expect(await student.PutAsJsonAsync($"/api/internships/{ppId}/evaluation-format", new { evaluationStartDate = date, evaluationEndDate = date.AddMonths(2) }), HttpStatusCode.Conflict);
        Console.WriteLine("HTTP SS seven-annex gate, PP closure and concurrent release folio checks passed.");
    }

    private static void Expect(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected) throw new Exception($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.AbsolutePath}: expected {(int)expected}, got {(int)response.StatusCode}");
    }
}
