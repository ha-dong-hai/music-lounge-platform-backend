namespace MusicLounge.Application.Common.Models;

public sealed record ExportedFileDto(byte[] Content, string FileName, string ContentType);
