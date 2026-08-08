namespace MusicLounge.Application.Common.Interfaces;

public interface IFileStorageService
{
    /// <summary>Saves the stream and returns a URL path (e.g. "/uploads/xxxx.jpg") the frontend can render directly.</summary>
    Task<string> SaveImageAsync(Stream content, string originalFileName, CancellationToken ct = default);

    /// <summary>Saves a .glb/.gltf 3D model file, returns a URL path the Three.js loader can fetch directly.</summary>
    Task<string> SaveModel3DAsync(Stream content, string originalFileName, CancellationToken ct = default);

    /// <summary>
    /// Moves a file previously saved via SaveImageAsync (still sitting in the publicly-served
    /// wwwroot/uploads tree at that point) into a private, non-static-served location, and
    /// returns an opaque reference to it — NOT a public URL. Used for citizen-card ID images:
    /// the client uploads through the same generic /uploads/images endpoint as any other image,
    /// then this relocates the file the moment it's actually claimed as a citizen-card image, so
    /// it's never reachable by a guessed/leaked static-file URL.
    /// </summary>
    Task<string> RelocateToPrivateAsync(string publicUrl, CancellationToken ct = default);

    /// <summary>Opens a file previously relocated via RelocateToPrivateAsync for streaming back to an authorized caller.</summary>
    Task<(Stream Content, string ContentType)> OpenPrivateFileAsync(string privateRef, CancellationToken ct = default);
}
