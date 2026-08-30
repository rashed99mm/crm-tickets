using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CustomerSupport.Application.Contracts;
using CustomerSupport.Application.Messages;
using CustomerSupport.Domain.Common;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.Tests.Integration;

/// <summary>
/// MVP-06 — customer attachments, against a real database and a real directory on disk.
///
/// <b>Three of these tests assert the filesystem, not the status code.</b> `AC-23` and `AC-24` say
/// "nothing is written to disk"; a handler that writes the bytes and then deletes them satisfies a
/// status-only assertion and fails the criterion as written. So those two count the files under the
/// storage root before and after and assert the count is unchanged, and `AC-25` asserts that what
/// landed is a GUID sitting <em>directly</em> under the root.
///
/// Each test gets its own <see cref="CrmApiFactory"/> — xUnit constructs the class per test — and
/// therefore its own storage root, so the counts below are not measuring another test's uploads.
/// </summary>
public class CustomerAttachmentEndpointTests : IAsyncLifetime
{
    /// <summary>A byte or two of a real PNG header. Content is never inspected; the type is.</summary>
    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03];

    private readonly CrmApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.EnsureDatabaseAsync();
        var (client, _) = await _factory.CreateAuthenticatedClientAsync();
        _client = client;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    // --- helpers -----------------------------------------------------------------------------------

    private async Task<Guid> CreateCustomerAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/Customers", new
        {
            name = "Nadia Farouk",
            email = $"attach-{Guid.NewGuid():N}@example.com",
            phone = "+20 100 000 0000",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        return body!.Data!;
    }

    private async Task<HttpResponseMessage> UploadAsync(
        Guid customerId,
        string fileName,
        string contentType,
        byte[] bytes,
        HttpClient? client = null)
    {
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(part, "file", fileName);

        return await (client ?? _client).PostAsync($"/api/Customers/{customerId}/attachments", form);
    }

    private async Task<PagedData<AttachmentRow>> ListAsync(Guid customerId)
    {
        var response = await _client.GetFromJsonAsync<Response<PagedData<AttachmentRow>>>(
            $"/api/Customers/{customerId}/attachments");

        return response!.Data!;
    }

    /// <summary>
    /// Every file anywhere beneath the storage root, subdirectories included — a traversal that
    /// created a folder would still be counted, which a top-level-only count would miss.
    /// </summary>
    private string[] FilesUnderRoot() =>
        Directory.Exists(_factory.StorageRoot)
            ? Directory.GetFiles(_factory.StorageRoot, "*", SearchOption.AllDirectories)
            : [];

    // --- AC-22 — the upload succeeds and the metadata comes back ------------------------------------

    [Fact]
    [Trait("AC", "22")]
    public async Task AC22_Upload_PermittedFile_Returns201WithStoredMetadata()
    {
        var customerId = await CreateCustomerAsync();

        var response = await UploadAsync(customerId, "screenshot.png", "image/png", PngBytes);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await response.Content.ReadFromJsonAsync<Response<Guid>>();
        created!.Success.Should().BeTrue();
        created.Data.Should().NotBeEmpty();

        // The criterion is about the file being *listed afterwards*. A 201 over a write that stored
        // nothing satisfies neither half.
        var page = await ListAsync(customerId);
        page.TotalCount.Should().Be(1);

        var row = page.Items.Single();
        row.Id.Should().Be(created.Data!);
        row.OriginalFileName.Should().Be("screenshot.png");
        row.ContentType.Should().Be("image/png");
        row.SizeBytes.Should().Be(PngBytes.Length);
        row.UploadedByName.Should().Be("Test User");
        row.CreatedAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-5));

        // And the bytes really did land — exactly one of them, under the configured root.
        FilesUnderRoot().Should().HaveCount(1);
    }

    // --- AC-23 — over the size limit, and nothing reaches the disk -----------------------------------

    /// <summary>
    /// Deliberately not a status-code test. The size check has to happen from the declared length
    /// *before* the stream is consumed, and the only assertion that can tell that apart from a
    /// write-then-delete is a file count.
    /// </summary>
    [Fact]
    [Trait("AC", "23")]
    public async Task AC23_Upload_OverTheSizeLimit_Returns413AndWritesNothingToDisk()
    {
        var customerId = await CreateCustomerAsync();
        var before = FilesUnderRoot().Length;

        // One byte past 10 MB (A20). ASP.NET buffers a part this large to its own temp file, which
        // is a framework concern and lands in the system temp directory — never under our root.
        var oversized = new byte[(10 * 1024 * 1024) + 1];

        var response = await UploadAsync(customerId, "huge.png", "image/png", oversized);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);

        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be(SystemCode.ERR042);
        body.Message.Should().NotBeNullOrWhiteSpace();

        FilesUnderRoot().Should().HaveCount(before, "nothing may be written to disk (AC-23)");
        (await ListAsync(customerId)).TotalCount.Should().Be(0);
    }

    // --- AC-24 — outside the allowlist, and nothing reaches the disk ---------------------------------

    [Fact]
    [Trait("AC", "24")]
    public async Task AC24_Upload_TypeOutsideTheAllowlist_Returns415AndWritesNothingToDisk()
    {
        var customerId = await CreateCustomerAsync();
        var before = FilesUnderRoot().Length;

        // An allowlist, not a blocklist — so this is refused for not being on the list, not for
        // being recognised as dangerous. Nobody has to have thought of it first.
        var response = await UploadAsync(
            customerId, "payload.exe", "application/x-msdownload", [0x4D, 0x5A, 0x90, 0x00]);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);

        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Code.Should().Be(SystemCode.ERR043);
        body.Message.Should().NotBeNullOrWhiteSpace();

        FilesUnderRoot().Should().HaveCount(before, "nothing may be written to disk (AC-24)");
        (await ListAsync(customerId)).TotalCount.Should().Be(0);
    }

    // --- AC-25 — a hostile filename cannot escape the directory --------------------------------------

    /// <summary>
    /// Both separator flavours, because Windows honours <c>/</c> as well as <c>\</c> and a defence
    /// written against one of them is not a defence.
    /// </summary>
    [Fact]
    [Trait("AC", "25")]
    public async Task AC25_Upload_HostileFilename_StoresAGuidInsideTheRoot()
    {
        var customerId = await CreateCustomerAsync();
        var before = FilesUnderRoot().Length;

        (await UploadAsync(customerId, "../../etc/passwd", "text/plain", "root:x:0:0"u8.ToArray()))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        (await UploadAsync(customerId, @"..\..\windows\system32\config\sam", "text/plain", "SAM"u8.ToArray()))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var after = FilesUnderRoot();
        after.Should().HaveCount(before + 2);

        var topLevel = Directory.GetFiles(_factory.StorageRoot, "*", SearchOption.TopDirectoryOnly);
        topLevel.Should().HaveCount(
            after.Length, "every stored file must sit directly under the root, in no subdirectory");

        Directory.GetDirectories(_factory.StorageRoot).Should().BeEmpty();

        foreach (var path in after)
        {
            // The stored name is server-generated. Neither "passwd" nor "sam" nor any part of the
            // path the client sent survives into it.
            var name = Path.GetFileNameWithoutExtension(path);
            Guid.TryParseExact(name, "N", out _).Should()
                .BeTrue($"'{name}' must be a server-generated GUID, not anything the client sent");
        }

        // And the original name is still readable as metadata — it is kept, just never used as a path.
        var page = await ListAsync(customerId);
        page.Items.Select(i => i.OriginalFileName).Should()
            .Contain("../../etc/passwd")
            .And.Contain(@"..\..\windows\system32\config\sam");
    }

    // --- AC-26 — download carries the type and the name, and requires a session -----------------------

    [Fact]
    [Trait("AC", "26")]
    public async Task AC26_Download_ReturnsTheContentTypeAndOriginalFilename()
    {
        var customerId = await CreateCustomerAsync();
        var bytes = "ticket 4711: connection reset"u8.ToArray();

        (await UploadAsync(customerId, "agent log.txt", "text/plain", bytes))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var attachmentId = (await ListAsync(customerId)).Items.Single().Id;

        var response = await _client.GetAsync(
            $"/api/Customers/{customerId}/attachments/{attachmentId}/content");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");

        // Content-Disposition carries a name a human recognises, not the GUID on disk.
        var disposition = response.Content.Headers.ContentDisposition;
        disposition.Should().NotBeNull();
        (disposition!.FileNameStar ?? disposition.FileName)!.Trim('"')
            .Should().Contain("agent log.txt");

        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
    }

    /// <summary>
    /// The reason the bytes are streamed by a handler rather than served from a static path: a
    /// static path is a public URL, and the criterion says download requires authentication.
    /// </summary>
    [Fact]
    [Trait("AC", "26")]
    public async Task AC26_Download_WithoutAToken_Returns401()
    {
        var customerId = await CreateCustomerAsync();

        (await UploadAsync(customerId, "invoice.pdf", "application/pdf", "%PDF-1.4"u8.ToArray()))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var attachmentId = (await ListAsync(customerId)).Items.Single().Id;

        using var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync(
            $"/api/Customers/{customerId}/attachments/{attachmentId}/content");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- AC-27 — the customer is in the path, so an unknown one is absent ----------------------------

    [Fact]
    [Trait("AC", "27")]
    public async Task AC27_Upload_UnknownCustomer_Returns404()
    {
        var before = FilesUnderRoot().Length;

        var response = await UploadAsync(Guid.NewGuid(), "screenshot.png", "image/png", PngBytes);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Code.Should().Be(SystemCode.ERR007);

        // The cheapest check runs first, so the bytes were never a cost this request paid.
        FilesUnderRoot().Should().HaveCount(before);
    }

    // --- AC-28 — removing an attachment takes the row and the file ------------------------------------

    [Fact]
    [Trait("AC", "28")]
    public async Task AC28_Remove_DeletesTheRowAndTheFile()
    {
        var customerId = await CreateCustomerAsync();

        (await UploadAsync(customerId, "screenshot.png", "image/png", PngBytes))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        FilesUnderRoot().Should().HaveCount(1);
        var attachmentId = (await ListAsync(customerId)).Items.Single().Id;

        var response = await _client.DeleteAsync(
            $"/api/Customers/{customerId}/attachments/{attachmentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The row is gone from the list…
        (await ListAsync(customerId)).TotalCount.Should().Be(0);

        // …and so are the bytes, which is the half a soft delete would otherwise leave behind.
        FilesUnderRoot().Should().BeEmpty();

        (await _client.GetAsync($"/api/Customers/{customerId}/attachments/{attachmentId}/content"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("AC", "27")]
    public async Task AC27_Remove_UnknownAttachment_Returns404()
    {
        var customerId = await CreateCustomerAsync();

        var response = await _client.DeleteAsync(
            $"/api/Customers/{customerId}/attachments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<Response<object>>();
        body!.Code.Should().Be(SystemCode.ERR041);
    }

    /// <summary>One row of the attachments list, exactly as the spec's contract fixes it.</summary>
    public sealed record AttachmentRow(
        Guid Id,
        string OriginalFileName,
        string ContentType,
        long SizeBytes,
        Guid UploadedById,
        string UploadedByName,
        DateTime CreatedAt);
}
