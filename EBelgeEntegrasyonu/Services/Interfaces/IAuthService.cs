using System.Xml.Linq;
using EBelgeAPI.Models.DTOs;
using EBelgeAPI.Models.ELogo;
using EBelgeAPI.Models.Entities;
using EBelgeAPI.Models.Requests;
using EBelgeAPI.Models.Responses;

namespace EBelgeAPI.Services.Interfaces;

public interface IAuthService
{
    Task<(bool Success, LoginResponse? Response, string? Error)> LoginAsync(LoginRequest request);
}
public interface ILogoTokenService
{
    Task<LogoTokenDto?> GetOrFetchTokenAsync();
}
public interface ILogService
{
    Task InfoAsync(string message, string source = "API", string? path = null,
        string? method = null, int? statusCode = null, string? username = null,
        string? ip = null, int? duration = null, string? detail = null);
    Task WarningAsync(string message, string source = "API", string? path = null,
        string? method = null, int? statusCode = null, string? username = null,
        string? ip = null, int? duration = null, string? detail = null);
    Task ErrorAsync(string message, string source = "API", string? path = null,
        string? method = null, int? statusCode = null, string? username = null,
        string? ip = null, int? duration = null, string? detail = null);
}
public interface ISatisElemaniService
{
    Task<List<SatisElemaniDto>> GetListAsync();
}
public interface IELogoService
{
    Task<(bool Success, string? Session, string? Error)> LoginAsync();
    Task LogoutAsync(string session);

    // List<ELogoInvoiceItem> dönüyor → DocumentType bilgisi item içinde taşınıyor
    Task<(bool Success, List<ELogoInvoiceItem>? Items, string? Error)>
        GetInvoiceListByDateAsync(
            string session,
            DateTime begin,
            DateTime end,
            ELogoDocumentType documentType = ELogoDocumentType.EInvoice);

    Task<(bool Success, XDocument? Ubl, string? Error)>
        GetInvoiceUblAsync(
            string session,
            string uuid,
            ELogoDocumentType documentType = ELogoDocumentType.EInvoice);

    Task<(bool Success, byte[]? Data, string? Error)>
        GetInvoiceVisualAsync(
            string session,
            string uuid,
            VisualFormat format,
            ELogoDocumentType documentType = ELogoDocumentType.EInvoice);

    Task<(bool Success, ELogoDocumentStatus? Status, string? Error)>
        GetInvoiceStatusAsync(
            string session,
            string uuid,
            ELogoDocumentType documentType = ELogoDocumentType.EInvoice);
}
public interface ILogoTransferService
{
    Task<bool> FaturaLogodaVarMiAsync(string faturaNo);
    Task<(bool Success, SalesTransferResultDto? Result, string? Error)>
        TransferAsync(SalesInvoiceDto dto, string ambarKodu, string satisElemaniKodu);
    Task<List<SalesTransferResultDto>> TopluTransferAsync(
        List<SalesInvoiceDto> dtos, string ambarKodu, string satisElemaniKodu);
}
public interface ISalesInvoiceService
{
    Task<(bool Success, string? Xml, string? Error)>
        GetSalesInvoiceUblRawAsync(
            string uuid,
            ELogoDocumentType docType = ELogoDocumentType.EInvoice);

    Task<(bool Success, List<SalesInvoiceDto>? Data, int TotalCount, string? Error)>
        GetSalesInvoicesAsync(SalesInvoiceListRequest request);

    Task<(bool Success, SalesInvoiceDto? Data, string? Error)>
        GetSalesInvoiceDetailAsync(
            string uuid,
            ELogoDocumentType docType = ELogoDocumentType.EInvoice);

    Task<(bool Success, byte[]? Data, string ContentType, string? Error)>
        GetSalesInvoiceVisualAsync(
            string uuid,
            VisualFormat format,
            ELogoDocumentType docType = ELogoDocumentType.EInvoice);
}