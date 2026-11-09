using CareComply.Application.Abstractions.Forms;
using CareComply.Domain.Forms;
using CareComply.Infrastructure.Forms;

namespace CareComply.Api.IntegrationTests;

public sealed class FormTemplatePdfValidationTests
{
    [Fact]
    public async Task RealDhs6893FPassesWithHybridAndAdobeScriptWarnings()
    {
        var validator = new PdfPigFormTemplatePdfValidator();
        await using var source = File.OpenRead(FindSample());

        var result = await validator.ValidateAsync(source, CancellationToken.None);

        Assert.Equal(FormTemplateFileValidationError.None, result.Error);
        var file = Assert.IsAssignableFrom<IValidatedFormTemplateFile>(result.File);
        await using (file)
        {
            Assert.Equal(FormTechnology.HybridXfaAcroForm, file.Technology);
            Assert.Equal(7, file.PageCount);
            Assert.Equal(36, file.Fields.Count);
            Assert.Contains(file.Warnings, warning => warning.Code == "hybrid-xfa");
            Assert.Contains(file.Warnings, warning => warning.Code == "adobe-version-check-scripts");
        }
    }

    [Fact]
    public async Task NonPdfIsRejectedCleanly()
    {
        var validator = new PdfPigFormTemplatePdfValidator();
        await using var source = new MemoryStream("not a pdf"u8.ToArray());

        var result = await validator.ValidateAsync(source, CancellationToken.None);

        Assert.Equal(FormTemplateFileValidationError.NotPdf, result.Error);
        Assert.Null(result.File);
    }

    [Fact]
    public async Task OversizeInputIsRejectedBeforeParsing()
    {
        var validator = new PdfPigFormTemplatePdfValidator();
        await using var source = new MemoryStream(new byte[(10 * 1024 * 1024) + 1]);

        var result = await validator.ValidateAsync(source, CancellationToken.None);

        Assert.Equal(FormTemplateFileValidationError.TooLarge, result.Error);
        Assert.Null(result.File);
    }

    [Fact]
    public void SameNameWithDifferentScriptContentIsRejected()
    {
        var scripts = ApprovedFingerprints().ToArray();
        scripts[0] = scripts[0] with { NormalizedSha256 = new string('0', 64) };
        Assert.False(AdobeVersionCheckScriptPolicy.IsAllowed(true, scripts));
    }

    [Fact]
    public void AllowlistedScriptsAreRejectedWhenPdfIsNotHybrid()
    {
        Assert.False(AdobeVersionCheckScriptPolicy.IsAllowed(false, ApprovedFingerprints()));
    }

    [Fact]
    public void AdditionalJavaScriptIsRejected()
    {
        var scripts = ApprovedFingerprints().Append(
            new PdfJavaScriptFingerprint("Extra", new string('0', 64))).ToArray();
        Assert.False(AdobeVersionCheckScriptPolicy.IsAllowed(true, scripts));
    }

    [Fact]
    public async Task ResetFormActionIsAllowed()
    {
        var validator = new PdfPigFormTemplatePdfValidator();
        await using var source = new MemoryStream(CreateSingleFieldPdf("<< /S /ResetForm >>"));

        var result = await validator.ValidateAsync(source, CancellationToken.None);

        Assert.Equal(FormTemplateFileValidationError.None, result.Error);
        Assert.NotNull(result.File);
        await result.File.DisposeAsync();
    }

    [Fact]
    public async Task JavaScriptWidgetActionIsRejected()
    {
        var validator = new PdfPigFormTemplatePdfValidator();
        await using var source = new MemoryStream(CreateSingleFieldPdf(
            "<< /S /JavaScript /JS (app.alert\\(1\\)) >>"));

        var result = await validator.ValidateAsync(source, CancellationToken.None);

        Assert.Equal(FormTemplateFileValidationError.DangerousContent, result.Error);
        Assert.Null(result.File);
    }

    [Fact]
    public async Task MalformedPdfIsRejected()
    {
        await AssertRejectedAsync("%PDF-this is not a valid PDF"u8.ToArray(),
            FormTemplateFileValidationError.MalformedPdf);
    }

    [Fact]
    public async Task PdfWithoutAcroFormFieldsIsRejected()
    {
        await AssertRejectedAsync(CreatePdf(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
        ]), FormTemplateFileValidationError.NoAcroFormFields);
    }

    [Fact]
    public async Task PureXfaWithoutAcroFormFieldsIsRejected()
    {
        await AssertRejectedAsync(CreatePdf(
        [
            "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            "<< /Fields [] /XFA (synthetic-xfa) >>",
        ]), FormTemplateFileValidationError.PureXfa);
    }

    [Fact]
    public async Task DuplicateFieldNamesAreRejected()
    {
        await AssertRejectedAsync(CreatePdf(
        [
            "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R 6 0 R] >>",
            "<< /Fields [5 0 R 6 0 R] >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Duplicate) /Rect [0 0 100 20] /P 3 0 R >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Duplicate) /Rect [0 30 100 50] /P 3 0 R >>",
        ]), FormTemplateFileValidationError.DuplicateFieldNames);
    }

    [Fact]
    public async Task MoreThanTwentyPagesIsRejected()
    {
        var objects = new List<string>
        {
            $"<< /Type /Catalog /Pages 2 0 R /AcroForm 24 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(' ', Enumerable.Range(3, 21).Select(number => $"{number} 0 R"))}] /Count 21 >>",
        };
        objects.AddRange(Enumerable.Range(3, 21).Select(number =>
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792]{(number == 3 ? " /Annots [25 0 R]" : string.Empty)} >>"));
        objects.Add("<< /Fields [25 0 R] >>");
        objects.Add("<< /Type /Annot /Subtype /Widget /FT /Tx /T (Field1) /Rect [0 0 100 20] /P 3 0 R >>");

        await AssertRejectedAsync(CreatePdf(objects), FormTemplateFileValidationError.TooManyPages);
    }

    [Fact]
    public async Task MoreThanFiveHundredFieldsIsRejected()
    {
        const int fieldCount = 501;
        var firstFieldObject = 5;
        var references = string.Join(' ', Enumerable.Range(firstFieldObject, fieldCount)
            .Select(number => $"{number} 0 R"));
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [{references}] >>",
            $"<< /Fields [{references}] >>",
        };
        objects.AddRange(Enumerable.Range(0, fieldCount).Select(index =>
            $"<< /Type /Annot /Subtype /Widget /FT /Tx /T (Field{index}) /Rect [0 0 100 20] /P 3 0 R >>"));

        await AssertRejectedAsync(CreatePdf(objects), FormTemplateFileValidationError.TooManyFields);
    }

    [Theory]
    [InlineData("JavaScript")]
    [InlineData("Launch")]
    [InlineData("URI")]
    [InlineData("SubmitForm")]
    [InlineData("ImportData")]
    [InlineData("GoToR")]
    [InlineData("GoToE")]
    public async Task DangerousWidgetActionsAreRejected(string actionType)
    {
        await AssertRejectedAsync(CreateSingleFieldPdf($"<< /S /{actionType} >>"),
            FormTemplateFileValidationError.DangerousContent);
    }

    [Fact]
    public async Task OpenActionIsRejected()
    {
        await AssertRejectedAsync(CreatePdf(
        [
            "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R /OpenAction 6 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R] >>",
            "<< /Fields [5 0 R] >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Field1) /Rect [0 0 100 20] /P 3 0 R >>",
            "<< /S /ResetForm >>",
        ]), FormTemplateFileValidationError.DangerousContent);
    }

    [Fact]
    public async Task EmbeddedFilesAreRejected()
    {
        await AssertRejectedAsync(CreatePdf(
        [
            "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R /Names << /EmbeddedFiles << >> >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R] >>",
            "<< /Fields [5 0 R] >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Field1) /Rect [0 0 100 20] /P 3 0 R >>",
        ]), FormTemplateFileValidationError.DangerousContent);
    }

    [Fact]
    public async Task ExcessiveObjectTraversalIsRejected()
    {
        const int chainLength = 130;
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R /Synthetic 6 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R] >>",
            "<< /Fields [5 0 R] >>",
            "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Field1) /Rect [0 0 100 20] /P 3 0 R >>",
        };
        objects.AddRange(Enumerable.Range(0, chainLength).Select(index =>
            index == chainLength - 1 ? "<< /End true >>" : $"<< /Next {7 + index} 0 R >>"));

        await AssertRejectedAsync(CreatePdf(objects), FormTemplateFileValidationError.TraversalLimitExceeded);
    }

    [Fact]
    public async Task EncryptedPdfIsRejected()
    {
        const string encryptedPdf =
            "JVBERi0xLjMKJeLjz9MKMSAwIG9iago8PAovUHJvZHVjZXIgPDI0ZjdiNzEyZGY+Cj4+CmVuZG9iagoyIDAgb2JqCjw8Ci9UeXBlIC9QYWdlcwovQ291bnQgMQovS2lkcyBbIDQgMCBSIF0KPj4KZW5kb2JqCjMgMCBvYmoKPDwKL1R5cGUgL0NhdGFsb2cKL1BhZ2VzIDIgMCBSCj4+CmVuZG9iago0IDAgb2JqCjw8Ci9UeXBlIC9QYWdlCi9SZXNvdXJjZXMgPDwKPj4KL01lZGlhQm94IFsgMC4wIDAuMCA2MTIgNzkyIF0KL1BhcmVudCAyIDAgUgo+PgplbmRvYmoKNSAwIG9iago8PAovViAyCi9SIDMKL0xlbmd0aCAxMjgKL1AgNDI5NDk2NzI5MgovRmlsdGVyIC9TdGFuZGFyZAovTyA8MGU1MjI5MjVhM2U0ZTg3NGMzY2ZhY2JlZjUxMWE3M2FjNGVjMmJkODY1ZGNkM2Q0NjI3NjE0OTE3YWJmZDdlND4KL1UgPDAxODBmY2VkMTZhNjA0MjJmNDJjNDhhNTMzZjMzYjRlMjhiZjRlNWU0ZTc1OGE0MTY0MDA0ZTU2ZmZmYTAxMDg+Cj4+CmVuZG9iagp4cmVmCjAgNgowMDAwMDAwMDAwIDY1NTM1IGYgCjAwMDAwMDAwMTUgMDAwMDAgbiAKMDAwMDAwMDA1OSAwMDAwMCBuIAowMDAwMDAwMTE4IDAwMDAwIG4gCjAwMDAwMDAxNjcgMDAwMDAgbiAKMDAwMDAwMDI2MSAwMDAwMCBuIAp0cmFpbGVyCjw8Ci9TaXplIDYKL1Jvb3QgMyAwIFIKL0luZm8gMSAwIFIKL0lEIFsgPDM1NjEzMTMyNjIzNzY0MzczODM1NjEzNjY0MzUzNTM3MzUzNjM5NjI2MjM3MzA2NDMyMzQzMjMyNjEzNzMwMzk+IDwzNTYxMzEzMjYyMzc2NDM3MzgzNTYxMzY2NDM1MzUzNzM1MzYzOTYyNjIzNzMwNjQzMjM0MzIzMjYxMzczMDM5PiBdCi9FbmNyeXB0IDUgMCBSCj4+CnN0YXJ0eHJlZgo0NzYKJSVFT0YK";
        await AssertRejectedAsync(Convert.FromBase64String(encryptedPdf),
            FormTemplateFileValidationError.Encrypted);
    }

    private static IReadOnlyList<PdfJavaScriptFingerprint> ApprovedFingerprints() =>
    [
        new("!ADBE::0100_VersChkStrings", "168e223e7aa69cf0de4a7097b3216464785e0e8953821c14ba7657dca31a23b7"),
        new("!ADBE::0100_VersChkVars", "6deda4f4f5e4ca2a74b161992be866434822c124b158fb41a5062935cf0550c2"),
        new("!ADBE::0200_VersChkCode_XFACheck", "af812ab099b4ba8477914c96eebf84df42bd8e3594d1255acec77c253d3121dd"),
    ];

    private static async Task AssertRejectedAsync(
        byte[] bytes,
        FormTemplateFileValidationError expected)
    {
        var validator = new PdfPigFormTemplatePdfValidator();
        await using var source = new MemoryStream(bytes);
        var result = await validator.ValidateAsync(source, CancellationToken.None);
        Assert.Equal(expected, result.Error);
        Assert.Null(result.File);
    }

    internal static byte[] CreateSingleFieldPdf(string action, string fieldName = "Field1")
    {
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R /AcroForm 4 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R] >>",
            "<< /Fields [5 0 R] >>",
            $"<< /Type /Annot /Subtype /Widget /FT /Tx /T ({fieldName}) /Rect [0 0 100 20] /P 3 0 R /A {action} >>",
        };
        return CreatePdf(objects);
    }

    internal static byte[] CreatePdf(IReadOnlyList<string> objects, string trailerEntries = "")
    {
        var builder = new System.Text.StringBuilder("%PDF-1.7\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(System.Text.Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(index + 1).Append(" 0 obj\n").Append(objects[index])
                .Append("\nendobj\n");
        }
        var xrefOffset = System.Text.Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 ").Append(objects.Count + 1).Append('\n')
            .Append("0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
            builder.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" 00000 n \n");
        builder.Append("trailer\n<< /Size ").Append(objects.Count + 1)
            .Append(" /Root 1 0 R ").Append(trailerEntries).Append(" >>\nstartxref\n").Append(xrefOffset)
            .Append("\n%%EOF\n");
        return System.Text.Encoding.ASCII.GetBytes(builder.ToString());
    }

    private static string FindSample()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "form-templates", "DHS_6893F.pdf");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("The synthetic DHS_6893F.pdf test fixture was not found.");
    }
}
