using System.Text;

namespace TicketFlow.Application.Attachments;

public static class AttachmentPolicy
{
    public const int MaxFileBytes = 10 * 1024 * 1024;

    public static (string FileName, string ContentType) Validate(string name, byte[] bytes)
    {
        if (bytes.Length is 0 or > MaxFileBytes)
            throw Invalid("Files must contain 1 byte to 10 MiB.");
        name = name.Replace('\\', '/').Split('/').Last().Normalize(NormalizationForm.FormC);
        var safe = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or ' ').ToArray()).Trim(' ', '.');
        if (safe.Length is 0 or > 180) throw Invalid("Use a file name up to 180 characters.");
        var extension = Path.GetExtension(safe).ToLowerInvariant();
        var valid = extension switch
        {
            ".png" => bytes.Length >= 33 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
                && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8),
            ".jpg" or ".jpeg" => bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255
                && bytes[^2] == 255 && bytes[^1] == 217,
            ".pdf" => bytes.Length >= 12 && bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8)
                && Encoding.ASCII.GetString(bytes.AsSpan(Math.Max(0, bytes.Length - 1024))).Contains("%%EOF", StringComparison.Ordinal),
            ".txt" => IsText(bytes),
            _ => false
        };
        if (!valid) throw Invalid("Upload a valid PDF, PNG, JPEG, or UTF-8 text file with its matching extension.");
        return (safe, extension switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".pdf" => "application/pdf", _ => "text/plain" });
    }

    private static bool IsText(byte[] bytes)
    {
        try
        {
            var text = new UTF8Encoding(false, true).GetString(bytes);
            return !text.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t');
        }
        catch (DecoderFallbackException) { return false; }
    }

    private static RuleException Invalid(string message) => new(ErrorKind.Validation, message);
}
