using JaxaRainmap.Models;

namespace JaxaRainmap.Services;

public interface IGsmapService
{
    Task<List<PrecipitationFrame>> GetFramesAsync(string collectionType, DateTime startDate, DateTime endDate);
    Task<PrecipitationFrame?> GetLatestFrameAsync(string collectionType);
    Task<StacCollection?> GetCollectionMetadataAsync(string collectionId);
}
