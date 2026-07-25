namespace PersonelYonetim.Application.Common.Interfaces;

/// <summary>DB’deki AppSetting değerlerini tip güvenli okur.</summary>
public interface IAppSettingsService
{
    Task<string> GetStringAsync(string key, string fallback, CancellationToken cancellationToken = default);
    Task<int> GetIntAsync(string key, int fallback, CancellationToken cancellationToken = default);
    Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken cancellationToken = default);
}
