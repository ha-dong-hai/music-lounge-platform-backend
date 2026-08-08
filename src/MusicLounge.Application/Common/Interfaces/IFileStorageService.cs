namespace MusicLounge.Application.Common.Interfaces;

public interface IFileStorageService
{
    /// <summary>Saves the stream and returns a URL path (e.g. "/uploads/xxxx.jpg") the frontend can render directly.</summary>
    Task<string> SaveImageAsync(Stream content, string originalFileName, CancellationToken ct = default);

    /// <summary>Saves a .glb/.gltf 3D model file, returns a URL path the Three.js loader can fetch directly.</summary>
    Task<string> SaveModel3DAsync(Stream content, string originalFileName, CancellationToken ct = default);
}
