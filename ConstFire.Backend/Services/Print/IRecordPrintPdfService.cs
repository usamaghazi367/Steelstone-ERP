namespace ConstFire.Backend.Services.Print;

public interface IRecordPrintPdfService
{
    Task<(byte[] Pdf, string FileName)?> GenerateAsync(
        string moduleCode,
        int recordId,
        CancellationToken cancellationToken = default);

    Task<(byte[] Pdf, string FileName)?> GenerateFirstRecordSampleAsync(
        string moduleCode,
        CancellationToken cancellationToken = default);
}
