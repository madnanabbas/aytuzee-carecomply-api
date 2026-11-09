using System.Security.Cryptography;
using System.Text;
using CareComply.Application.Abstractions.Forms;
using CareComply.Domain.Forms;
using UglyToad.PdfPig;
using UglyToad.PdfPig.AcroForms;
using UglyToad.PdfPig.AcroForms.Fields;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;
using UglyToad.PdfPig.Tokenization.Scanner;
using UglyToad.PdfPig.Tokens;

namespace CareComply.Infrastructure.Forms;

public sealed class PdfPigFormTemplatePdfValidator : IFormTemplatePdfValidator
{
    internal const long MaximumBytes = 10 * 1024 * 1024;
    internal const int MaximumPages = 20;
    internal const int MaximumFields = 500;
    private const int MaximumTraversalDepth = 128;
    private const int MaximumVisitedObjects = 25_000;
    private const string ScriptWarningCode = "adobe-version-check-scripts";

    // These hashes are of UTF-8 script text after BOM removal, CR/CRLF -> LF,
    // trimming trailing whitespace per line, and trimming the complete body.
    private static readonly HashSet<string> RejectedActionTypes = new(StringComparer.Ordinal)
    {
        "JavaScript", "Launch", "URI", "SubmitForm", "ImportData", "GoToR", "GoToE",
    };

    public async Task<FormTemplateFileValidationResult> ValidateAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var path = Path.Combine(Path.GetTempPath(), $"carecomply-form-{Guid.CreateVersion7():N}.pdf");
        try
        {
            var staged = await StageAsync(source, path, cancellationToken);
            if (staged.Error != FormTemplateFileValidationError.None)
                return FormTemplateFileValidationResult.Invalid(staged.Error);

            return Inspect(path, staged.SizeBytes, staged.Sha256);
        }
        catch (OperationCanceledException)
        {
            TryDelete(path);
            throw;
        }
        catch
        {
            TryDelete(path);
            return FormTemplateFileValidationResult.Invalid(FormTemplateFileValidationError.MalformedPdf);
        }
    }

    private static async Task<StagedFile> StageAsync(
        Stream source,
        string path,
        CancellationToken cancellationToken)
    {
        long size = 0;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        await using (var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, buffer.Length, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            while (true)
            {
                var count = await source.ReadAsync(buffer, cancellationToken);
                if (count == 0) break;
                size += count;
                if (size > MaximumBytes)
                {
                    await destination.DisposeAsync();
                    TryDelete(path);
                    return new(0, string.Empty, FormTemplateFileValidationError.TooLarge);
                }
                hash.AppendData(buffer.AsSpan(0, count));
                await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }
            await destination.FlushAsync(cancellationToken);
        }

        if (size < 5)
        {
            TryDelete(path);
            return new(0, string.Empty, FormTemplateFileValidationError.NotPdf);
        }
        var header = new byte[5];
        await using (var headerStream = File.OpenRead(path))
        {
            _ = await headerStream.ReadAsync(header, cancellationToken);
        }
        if (!header.AsSpan().SequenceEqual("%PDF-"u8))
        {
            TryDelete(path);
            return new(0, string.Empty, FormTemplateFileValidationError.NotPdf);
        }

        return new(size, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            FormTemplateFileValidationError.None);
    }

    private static FormTemplateFileValidationResult Inspect(string path, long size, string sha256)
    {
        try
        {
            using var document = PdfDocument.Open(path, ParsingOptions.LenientParsingOff);
            if (document.IsEncrypted) return InvalidAndDelete(path, FormTemplateFileValidationError.Encrypted);
            if (document.NumberOfPages > MaximumPages)
                return InvalidAndDelete(path, FormTemplateFileValidationError.TooManyPages);

            var catalog = document.Structure.Catalog.CatalogDictionary;
            var scanner = document.Structure.TokenScanner;
            if (catalog.Data.ContainsKey("OpenAction"))
                return InvalidAndDelete(path, FormTemplateFileValidationError.DangerousContent);
            var acroFormDictionary = ResolveDictionary(catalog, "AcroForm", scanner);
            var isHybrid = acroFormDictionary?.Data.ContainsKey("XFA") == true;
            if (!document.TryGetForm(out var form))
            {
                return InvalidAndDelete(path, isHybrid
                    ? FormTemplateFileValidationError.PureXfa
                    : FormTemplateFileValidationError.NoAcroFormFields);
            }

            var leaves = form.GetFields().ToArray();
            if (leaves.Length == 0)
                return InvalidAndDelete(path, isHybrid
                    ? FormTemplateFileValidationError.PureXfa
                    : FormTemplateFileValidationError.NoAcroFormFields);
            if (leaves.Length > MaximumFields)
                return InvalidAndDelete(path, FormTemplateFileValidationError.TooManyFields);

            var fields = leaves.Select(field => new DiscoveredFormField(
                    GetFullyQualifiedName(field, scanner), MapFieldType(field.FieldType)))
                .ToArray();
            if (fields.Any(field => string.IsNullOrWhiteSpace(field.Name))
                || fields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() != fields.Length)
            {
                return InvalidAndDelete(path, FormTemplateFileValidationError.DuplicateFieldNames);
            }

            var approvedNameTreeReferences = new HashSet<IndirectReference>();
            var scriptEntries = ReadJavaScriptNameTree(catalog, document, approvedNameTreeReferences);
            if (scriptEntries.Count > 0)
            {
                var fingerprints = scriptEntries.Select(entry =>
                    new PdfJavaScriptFingerprint(entry.Name, HashNormalizedScript(entry.Body))).ToArray();
                if (!AdobeVersionCheckScriptPolicy.IsAllowed(isHybrid, fingerprints))
                    return InvalidAndDelete(path, FormTemplateFileValidationError.DangerousContent);
            }

            var traversal = ValidateReachableObjects(catalog, scanner, approvedNameTreeReferences);
            if (traversal != FormTemplateFileValidationError.None)
                return InvalidAndDelete(path, traversal);

            var warnings = new List<FormTemplateValidationWarning>();
            if (isHybrid)
                warnings.Add(new("hybrid-xfa", "Hybrid XFA/AcroForm content is present; only AcroForm fields are supported."));
            if (scriptEntries.Count > 0)
                warnings.Add(new(ScriptWarningCode,
                    "Standard Adobe version-check scripts are present; they run only in Adobe viewers."));

            return FormTemplateFileValidationResult.Valid(new ValidatedFile(path, size, sha256,
                document.NumberOfPages,
                isHybrid ? FormTechnology.HybridXfaAcroForm : FormTechnology.AcroForm,
                fields,
                warnings));
        }
        catch (Exception exception) when (ContainsEncryptedDocumentException(exception))
        {
            return InvalidAndDelete(path, FormTemplateFileValidationError.Encrypted);
        }
        catch
        {
            return InvalidAndDelete(path, FormTemplateFileValidationError.MalformedPdf);
        }
    }

    private static List<ScriptEntry> ReadJavaScriptNameTree(
        DictionaryToken catalog,
        PdfDocument document,
        HashSet<IndirectReference> allowedReferences)
    {
        var scanner = document.Structure.TokenScanner;
        var names = ResolveDictionary(catalog, "Names", scanner);
        if (names is null || !names.Data.TryGetValue("JavaScript", out var javascriptToken)) return [];
        if (javascriptToken is IndirectReferenceToken nameTreeReference)
            allowedReferences.Add(nameTreeReference.Data);
        var javascript = Resolve(javascriptToken, scanner) as DictionaryToken;
        if (javascript is null) return [];
        var results = new List<ScriptEntry>();
        ReadNameTreeNode(javascript, document, results, 0);
        return results;
    }

    private static void ReadNameTreeNode(
        DictionaryToken node,
        PdfDocument document,
        List<ScriptEntry> results,
        int depth)
    {
        if (depth > MaximumTraversalDepth) throw new InvalidDataException();
        var scanner = document.Structure.TokenScanner;
        if (node.Data.TryGetValue("Names", out var namesToken)
            && Resolve(namesToken, scanner) is ArrayToken pairs)
        {
            for (var index = 0; index + 1 < pairs.Length; index += 2)
            {
                var name = ReadText(Resolve(pairs[index], scanner));
                var actionToken = pairs[index + 1];
                if (Resolve(actionToken, scanner) is not DictionaryToken action
                    || ReadName(action, "S", scanner) != "JavaScript"
                    || !action.Data.TryGetValue("JS", out var bodyToken))
                    throw new InvalidDataException();
                results.Add(new(name, ReadScriptBody(Resolve(bodyToken, scanner), document)));
            }
        }
        if (node.Data.TryGetValue("Kids", out var kidsToken)
            && Resolve(kidsToken, scanner) is ArrayToken kids)
        {
            foreach (var kid in kids.Data)
            {
                if (Resolve(kid, scanner) is not DictionaryToken child) throw new InvalidDataException();
                ReadNameTreeNode(child, document, results, depth + 1);
            }
        }
    }

    private static FormTemplateFileValidationError ValidateReachableObjects(
        DictionaryToken root,
        IPdfTokenScanner scanner,
        HashSet<IndirectReference> allowedScriptReferences)
    {
        var visited = new HashSet<IndirectReference>();
        var count = 0;
        return Visit(root, 0);

        FormTemplateFileValidationError Visit(IToken token, int depth)
        {
            if (depth > MaximumTraversalDepth || ++count > MaximumVisitedObjects)
                return FormTemplateFileValidationError.TraversalLimitExceeded;
            if (token is IndirectReferenceToken reference)
            {
                if (allowedScriptReferences.Contains(reference.Data)) return FormTemplateFileValidationError.None;
                if (!visited.Add(reference.Data)) return FormTemplateFileValidationError.None;
                var resolved = scanner.Get(reference.Data)?.Data;
                return resolved is null
                    ? FormTemplateFileValidationError.DangerousContent
                    : Visit(resolved, depth + 1);
            }
            if (token is StreamToken stream) return Visit(stream.StreamDictionary, depth + 1);
            if (token is ArrayToken array)
            {
                foreach (var item in array.Data)
                {
                    var result = Visit(item, depth + 1);
                    if (result != FormTemplateFileValidationError.None) return result;
                }
                return FormTemplateFileValidationError.None;
            }
            if (token is not DictionaryToken dictionary) return FormTemplateFileValidationError.None;

            if (dictionary.Data.TryGetValue("S", out var actionTypeToken)
                && Resolve(actionTypeToken, scanner) is NameToken actionType
                && RejectedActionTypes.Contains(actionType.Data))
                return FormTemplateFileValidationError.DangerousContent;
            if (dictionary.Data.ContainsKey("EmbeddedFiles"))
                return FormTemplateFileValidationError.DangerousContent;

            foreach (var item in dictionary.Data.Values)
            {
                var result = Visit(item, depth + 1);
                if (result != FormTemplateFileValidationError.None) return result;
            }
            return FormTemplateFileValidationError.None;
        }
    }

    private static string GetFullyQualifiedName(AcroFieldBase field, IPdfTokenScanner scanner)
    {
        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(field.Information.PartialName))
            names.Add(field.Information.PartialName);
        var parent = field.Information.Parent;
        var visited = new HashSet<IndirectReference>();
        while (parent.HasValue && visited.Add(parent.Value))
        {
            if (scanner.Get(parent.Value)?.Data is not DictionaryToken dictionary) break;
            if (dictionary.Data.TryGetValue("T", out var nameToken))
            {
                var name = ReadText(Resolve(nameToken, scanner));
                if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
            }
            parent = dictionary.Data.TryGetValue("Parent", out var parentToken)
                && parentToken is IndirectReferenceToken reference ? reference.Data : null;
        }
        names.Reverse();
        return string.Join('.', names);
    }

    private static FormFieldType MapFieldType(AcroFieldType type) => type switch
    {
        AcroFieldType.Text => FormFieldType.Text,
        AcroFieldType.Checkbox or AcroFieldType.Checkboxes => FormFieldType.CheckBox,
        AcroFieldType.RadioButton or AcroFieldType.RadioButtons => FormFieldType.RadioButton,
        AcroFieldType.ComboBox or AcroFieldType.ListBox => FormFieldType.Choice,
        AcroFieldType.PushButton => FormFieldType.Button,
        AcroFieldType.Signature => FormFieldType.Signature,
        _ => FormFieldType.Unknown,
    };

    private static DictionaryToken? ResolveDictionary(
        DictionaryToken dictionary,
        string key,
        IPdfTokenScanner scanner) =>
        dictionary.Data.TryGetValue(key, out var token) ? Resolve(token, scanner) as DictionaryToken : null;

    private static IToken Resolve(IToken token, IPdfTokenScanner scanner) =>
        token is IndirectReferenceToken reference
            ? scanner.Get(reference.Data)?.Data ?? throw new InvalidDataException()
            : token;

    private static string ReadName(DictionaryToken dictionary, string key, IPdfTokenScanner scanner) =>
        dictionary.Data.TryGetValue(key, out var token) && Resolve(token, scanner) is NameToken name
            ? name.Data : string.Empty;

    private static string ReadText(IToken token) => token switch
    {
        StringToken text => text.Data,
        HexToken hex => hex.Data,
        _ => string.Empty,
    };

    private static string ReadScriptBody(IToken token, PdfDocument document) => token switch
    {
        StringToken text => text.Data,
        HexToken hex => hex.Data,
        StreamToken stream => Encoding.UTF8.GetString(stream.Decode(document.Structure.FilterProvider).Span),
        _ => throw new InvalidDataException(),
    };

    internal static string HashNormalizedScript(string script)
    {
        var normalized = script.TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        normalized = string.Join('\n', normalized.Split('\n').Select(line => line.TrimEnd())).Trim();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private static FormTemplateFileValidationResult InvalidAndDelete(
        string path,
        FormTemplateFileValidationError error)
    {
        TryDelete(path);
        return FormTemplateFileValidationResult.Invalid(error);
    }

    private static bool ContainsEncryptedDocumentException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PdfDocumentEncryptedException) return true;
        }
        return false;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed record StagedFile(
        long SizeBytes,
        string Sha256,
        FormTemplateFileValidationError Error);

    private sealed record ScriptEntry(string Name, string Body);

    private sealed class ValidatedFile(
        string path,
        long sizeBytes,
        string sha256,
        int pageCount,
        FormTechnology technology,
        IReadOnlyList<DiscoveredFormField> fields,
        IReadOnlyList<FormTemplateValidationWarning> warnings) : IValidatedFormTemplateFile
    {
        public long SizeBytes { get; } = sizeBytes;
        public string Sha256 { get; } = sha256;
        public int PageCount { get; } = pageCount;
        public FormTechnology Technology { get; } = technology;
        public IReadOnlyList<DiscoveredFormField> Fields { get; } = fields;
        public IReadOnlyList<FormTemplateValidationWarning> Warnings { get; } = warnings;
        public Stream OpenRead() => new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        public ValueTask DisposeAsync()
        {
            TryDelete(path);
            return ValueTask.CompletedTask;
        }
    }
}

public sealed record PdfJavaScriptFingerprint(string Name, string NormalizedSha256);

public static class AdobeVersionCheckScriptPolicy
{
    private static readonly Dictionary<string, string> AllowedScripts =
        new(StringComparer.Ordinal)
        {
            ["!ADBE::0100_VersChkStrings"] = "168e223e7aa69cf0de4a7097b3216464785e0e8953821c14ba7657dca31a23b7",
            ["!ADBE::0100_VersChkVars"] = "6deda4f4f5e4ca2a74b161992be866434822c124b158fb41a5062935cf0550c2",
            ["!ADBE::0200_VersChkCode_XFACheck"] = "af812ab099b4ba8477914c96eebf84df42bd8e3594d1255acec77c253d3121dd",
        };

    public static bool IsAllowed(
        bool isHybridXfaAcroForm,
        IReadOnlyCollection<PdfJavaScriptFingerprint> scripts)
    {
        if (!isHybridXfaAcroForm || scripts.Count != AllowedScripts.Count) return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var script in scripts)
        {
            if (!seen.Add(script.Name)
                || !AllowedScripts.TryGetValue(script.Name, out var expectedHash)
                || !IsExpectedHash(expectedHash, script.NormalizedSha256)) return false;
        }
        return true;
    }

    private static bool IsExpectedHash(string expected, string actual)
    {
        if (actual.Length != 64 || actual.Any(character => !Uri.IsHexDigit(character))) return false;
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expected), Convert.FromHexString(actual));
    }
}
